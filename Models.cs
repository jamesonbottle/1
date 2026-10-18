using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage;
using Windows.Storage.FileProperties;

namespace ScreenTime;

// ---- persisted data ----------------------------------------------------------------

public class DayData
{
    public Dictionary<string, int[]> Apps { get; set; } = new();   // process name -> seconds per hour (24 slots), in front only
    public int Pickups { get; set; }

    // process name -> list of [startSecond, endSecond] within the day (0..86400).
    public Dictionary<string, List<int[]>> Open { get; set; } = new();    // app had a window open (front or back)
    public Dictionary<string, List<int[]>> Focus { get; set; } = new();   // app was the foreground window

    public int Total => Apps.Values.Sum(h => h.Sum());

    public int[] ByHour()
    {
        var r = new int[24];
        foreach (var h in Apps.Values) for (int i = 0; i < 24 && i < h.Length; i++) r[i] += h[i];
        return r;
    }
}

public class AppMeta { public string Name { get; set; } = ""; public string? Path { get; set; } }

public class Settings
{
    public Dictionary<string, AppMeta> Apps { get; set; } = new();  // every app we've seen
}

// ---- view models (plain records, bound with x:Bind) ----------------------------------

public record Bar(double Height, double Width, string Label);
public record AppUsage(string Key, string Name, string Time, double Percent, BitmapImage Icon);

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
