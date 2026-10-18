using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.UI.Xaml;
using Windows.Win32;
using Windows.Win32.UI.Input.KeyboardAndMouse;

namespace ScreenTime;

/// <summary>
/// Samples the foreground app once a second (screen time) and, every few seconds, which apps have a window open
/// (so the timeline can show overlapping apps on separate lanes).
/// </summary>
public sealed class Tracker
{
    static readonly TimeSpan IdleLimit = TimeSpan.FromMinutes(5);   // no input for this long = screen not in use
    const int OpenSampleEvery = 5;                                   // seconds between "which windows are open" scans
    static readonly string Dir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ScreenTime");
    static readonly string SettingsFile = Path.Combine(Dir, "settings.json");
    static string DayFile(DateOnly d) => Path.Combine(Dir, $"{d:yyyy-MM-dd}.json");

    // Shell / system processes: never counted.
    static readonly HashSet<string> Protected = new(StringComparer.OrdinalIgnoreCase)
    {
        "explorer", "ShellExperienceHost", "StartMenuExperienceHost", "SearchHost", "LockApp",
        "LogonUI", "TextInputHost", "ApplicationFrameHost", "dwm", "ScreenTime",
    };

    // Must stay BELOW the static fields above: static initializers run top to bottom, and the
    // constructor reads Dir / SettingsFile. Declared first, it crashed the app on startup.
    public static Tracker Instance { get; } = new();

    readonly Dictionary<DateOnly, DayData> _past = new();
    DateOnly _day = DateOnly.FromDateTime(DateTime.Now);
    bool _wasIdle = true;
    int _ticks;

    public Settings Settings { get; }
    public DayData Today { get; private set; }
    public event Action? Changed;

    Tracker()
    {
        Directory.CreateDirectory(Dir);
        Settings = Read<Settings>(SettingsFile) ?? new();
        Today = Read<DayData>(DayFile(_day)) ?? new();
    }

    public void Start()
    {
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        timer.Tick += (_, _) => Tick();
        timer.Start();
    }

    public DayData Get(DateOnly d)
    {
        if (d == _day) return Today;
        if (d > _day) return new();
        if (!_past.TryGetValue(d, out var data)) _past[d] = data = Read<DayData>(DayFile(d)) ?? new();
        return data;
    }

    public void Save()
    {
        File.WriteAllText(SettingsFile, JsonSerializer.Serialize(Settings));
        File.WriteAllText(DayFile(_day), JsonSerializer.Serialize(Today));
    }

    // ---- the one-second loop ----------------------------------------------------------

    // A bad sample (process exited mid-read, access denied...) must never take the app down.
    void Tick()
    {
        try { TickCore(); }
        catch (Exception ex) { Diag.Log(ex); }
    }

    void TickCore()
    {
        var now = DateTime.Now;
        var date = DateOnly.FromDateTime(now);
        if (date != _day)   // midnight rollover
        {
            Save();
            _past[_day] = Today;
            _day = date;
            Today = new();
        }

        int sec = (int)now.TimeOfDay.TotalSeconds;
        bool idle = IdleTime() >= IdleLimit;
        if (_wasIdle && !idle) Today.Pickups++;   // coming back from idle = a "pickup"
        _wasIdle = idle;

        if (!idle)
        {
            using var p = Foreground();
            if (p is not null && !Protected.Contains(p.ProcessName))
            {
                var key = p.ProcessName;
                Learn(p, key);
                if (!Today.Apps.TryGetValue(key, out var hours)) Today.Apps[key] = hours = new int[24];
                hours[now.Hour]++;
                Mark(Today.Focus, key, sec, 1, 2);
                Mark(Today.Open, key, sec, 1, 2);   // the front app is open by definition
            }

            if (_ticks % OpenSampleEvery == 0) SampleOpenApps(sec);
        }

        if (++_ticks % 30 == 0) Save();
        Changed?.Invoke();
    }

    /// <summary>Extends the app's last span if this sample continues it, otherwise starts a new span.</summary>
    static void Mark(Dictionary<string, List<int[]>> spans, string key, int sec, int width, int tolerance)
    {
        if (!spans.TryGetValue(key, out var list)) spans[key] = list = new();
        int end = Math.Min(86400, sec + width);
        if (list.Count > 0 && sec - list[^1][1] <= tolerance) list[^1][1] = Math.Max(list[^1][1], end);
        else list.Add(new[] { sec, end });
    }

    // Every app with a visible top-level window counts as open (minimized windows included).
    void SampleOpenApps(int sec)
    {
        foreach (var p in Process.GetProcesses())
        {
            try
            {
                if (p.MainWindowHandle == IntPtr.Zero || p.MainWindowTitle.Length == 0) continue;
                var key = p.ProcessName;
                if (Protected.Contains(key)) continue;
                Learn(p, key);
                Mark(Today.Open, key, sec, OpenSampleEvery, OpenSampleEvery + 1);
            }
            catch { /* exited or access denied */ }
            finally { p.Dispose(); }
        }
    }

    // ---- Win32 + Windows helpers ------------------------------------------------------

    static Process? Foreground()
    {
        uint pid;
        unsafe { PInvoke.GetWindowThreadProcessId(PInvoke.GetForegroundWindow(), &pid); }
        try { return pid == 0 ? null : Process.GetProcessById((int)pid); }
        catch { return null; }   // exited, or access denied
    }

    static TimeSpan IdleTime()
    {
        var info = new LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf<LASTINPUTINFO>() };
        PInvoke.GetLastInputInfo(ref info);
        return TimeSpan.FromMilliseconds(unchecked((uint)Environment.TickCount - info.dwTime));
    }

    // Remember the friendly name (exe's FileDescription) and path (for the icon) of each app.
    void Learn(Process p, string key)
    {
        if (Settings.Apps.ContainsKey(key)) return;
        string? path = null, name = null;
        try
        {
            path = p.MainModule?.FileName;
            if (path is not null) name = FileVersionInfo.GetVersionInfo(path).FileDescription;
        }
        catch { }
        Settings.Apps[key] = new() { Name = string.IsNullOrWhiteSpace(name) ? key : name.Trim(), Path = path };
    }

    static T? Read<T>(string path)
    {
        try { return JsonSerializer.Deserialize<T>(File.ReadAllText(path)); }
        catch { return default; }
    }
}
