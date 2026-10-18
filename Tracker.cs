using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;
using Windows.Win32;
using Windows.Win32.UI.Input.KeyboardAndMouse;

namespace ScreenTime;

/// <summary>Samples the foreground app once a second, stores usage per hour, and enforces limits / downtime.</summary>
public sealed class Tracker
{
    static readonly TimeSpan IdleLimit = TimeSpan.FromMinutes(5);   // no input for this long = screen not in use
    static readonly string Dir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ScreenTime");
    static readonly string SettingsFile = Path.Combine(Dir, "settings.json");
    static string DayFile(DateOnly d) => Path.Combine(Dir, $"{d:yyyy-MM-dd}.json");

    // Shell / system processes: never counted, never closed.
    static readonly HashSet<string> Protected = new(StringComparer.OrdinalIgnoreCase)
    {
        "explorer", "ShellExperienceHost", "StartMenuExperienceHost", "SearchHost", "LockApp",
        "LogonUI", "TextInputHost", "ApplicationFrameHost", "dwm", "ScreenTime",
    };

    // Must stay BELOW the static fields above: static initializers run top to bottom, and the
    // constructor reads Dir / SettingsFile. Declared first, it crashed the app on startup.
    public static Tracker Instance { get; } = new();

    readonly Dictionary<DateOnly, DayData> _past = new();
    readonly HashSet<string> _notified = new();
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

    public bool InDowntime(TimeSpan t) =>
        Settings.DowntimeEnabled &&
        (Settings.DowntimeStart <= Settings.DowntimeEnd
            ? t >= Settings.DowntimeStart && t < Settings.DowntimeEnd
            : t >= Settings.DowntimeStart || t < Settings.DowntimeEnd);   // overnight, e.g. 22:00 -> 07:00

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
            _notified.Clear();
        }

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
                Enforce(p, key, now);
            }
        }

        if (++_ticks % 30 == 0) Save();
        Changed?.Invoke();
    }

    void Enforce(Process p, string key, DateTime now)
    {
        var name = Ui.Name(key);

        if (InDowntime(now.TimeOfDay) && !Settings.Allowed.Contains(key))
        {
            p.CloseMainWindow();
            if (_notified.Add("dt:" + key)) Notify("Downtime", $"{name} isn't available during downtime.");
            return;
        }

        var limit = Settings.Limits.FirstOrDefault(l => l.App == key);
        if (limit is null || Today.Ignored.Contains(key)) return;

        int left = limit.Minutes * 60 - Today.Seconds(key);
        if (left > 0 && left <= 300 && _notified.Add("5:" + key))
            Notify("App Limit", $"{name} has 5 minutes left today.");
        if (left <= 0)
        {
            p.CloseMainWindow();
            if (_notified.Add("0:" + key)) Notify("Time Limit", $"You've reached your limit for {name}.");
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

    static void Notify(string title, string text)
    {
        try { AppNotificationManager.Default.Show(new AppNotificationBuilder().AddText(title).AddText(text).BuildNotification()); }
        catch { }
    }

    static T? Read<T>(string path)
    {
        try { return JsonSerializer.Deserialize<T>(File.ReadAllText(path)); }
        catch { return default; }
    }
}
