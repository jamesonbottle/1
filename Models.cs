using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage;
using Windows.Storage.FileProperties;

namespace ScreenTime;

// ---- persisted data ----------------------------------------------------------------

public class DayData
{
    public Dictionary<string, int[]> Apps { get; set; } = new();   // process name -> seconds per hour (24 slots)
    public int Pickups { get; set; }
    public HashSet<string> Ignored { get; set; } = new();           // limits ignored for this day

    public int Seconds(string key) => Apps.TryGetValue(key, out var h) ? h.Sum() : 0;
    public int Total => Apps.Values.Sum(h => h.Sum());

    public int[] ByHour()
    {
        var r = new int[24];
        foreach (var h in Apps.Values) for (int i = 0; i < 24 && i < h.Length; i++) r[i] += h[i];
        return r;
    }
}

public class AppMeta { public string Name { get; set; } = ""; public string? Path { get; set; } }
public class Limit { public string App { get; set; } = ""; public int Minutes { get; set; } }

public class Settings
{
    public Dictionary<string, AppMeta> Apps { get; set; } = new();  // every app we've seen
    public List<Limit> Limits { get; set; } = new();
    public bool DowntimeEnabled { get; set; }
    public TimeSpan DowntimeStart { get; set; } = TimeSpan.FromHours(22);
    public TimeSpan DowntimeEnd { get; set; } = TimeSpan.FromHours(7);
    public HashSet<string> Allowed { get; set; } = new();           // always-allowed apps
}

// ---- view models (plain records, bound with x:Bind) ----------------------------------

public record Bar(double Height, double Width, string Label);
public record AppUsage(string Key, string Name, string Time, double Percent, BitmapImage Icon);
public record LimitRow(string Key, string Name, string Detail, BitmapImage Icon, bool CanIgnore);
public record AppChoice(string Key, string Name);

public class AllowedRow(string key, Settings s)
{
    public string Name => Ui.Name(key);
    public BitmapImage Icon => Ui.Icon(key);
    public bool On
    {
        get => s.Allowed.Contains(key);
        set { if (value) s.Allowed.Add(key); else s.Allowed.Remove(key); }
    }
}

// ---- small helpers -----------------------------------------------------------------

public static class Ui
{
    static readonly Dictionary<string, BitmapImage> _icons = new();

    public static string Name(string key) => Tracker.Instance.Settings.Apps.GetValueOrDefault(key)?.Name ?? key;

    public static string Time(int s) => s >= 3600 ? $"{s / 3600}h {s % 3600 / 60}m" : s >= 60 ? $"{s / 60}m" : $"{s}s";

    // The icon is whatever Windows shows for the .exe in Explorer (StorageFile thumbnail API, no extra packages).
    public static BitmapImage Icon(string key)
    {
        if (_icons.TryGetValue(key, out var img)) return img;
        img = _icons[key] = new BitmapImage();
        if (Tracker.Instance.Settings.Apps.GetValueOrDefault(key)?.Path is { } path) _ = Load(img, path);
        return img;
    }

    static async Task Load(BitmapImage img, string path)
    {
        try
        {
            var file = await StorageFile.GetFileFromPathAsync(path);
            using var thumb = await file.GetThumbnailAsync(ThumbnailMode.SingleItem, 64);
            await img.SetSourceAsync(thumb);
        }
        catch { /* no icon, leave blank */ }
    }
}
