using System.Globalization;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage;
using Windows.Storage.FileProperties;

namespace ScreenTime;

// ---- persisted data ----------------------------------------------------------------

public class DayData
{
    public Dictionary<string, int[]> Apps { get; set; } = new();   // process name -> seconds per hour (24 slots), in front only

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

    // First run follows the system clock format; after that it's whatever the toggle says.
    public bool Use24Hour { get; set; } = CultureInfo.CurrentCulture.DateTimeFormat.ShortTimePattern.Contains('H');

    public double LabelWidth { get; set; } = 170;   // width of the app-name column in the timeline
}

// ---- view models (plain records, bound with x:Bind) ----------------------------------

public record Bar(double Height, double Width, string Label);

// ---- small helpers -----------------------------------------------------------------

public static class Ui
{
    static readonly Dictionary<string, BitmapImage> _icons = new();

    public static string Name(string key) => Tracker.Instance.Settings.Apps.GetValueOrDefault(key)?.Name ?? key;

    public static string Time(int s) => s >= 3600 ? $"{s / 3600}h {s % 3600 / 60}m" : s >= 60 ? $"{s / 60}m" : $"{s}s";

    /// <summary>Time of day for a second-of-day value, in the user's 12 or 24 hour format.</summary>
    public static string Clock(int sec, bool withSeconds = false)
    {
        sec = Math.Clamp(sec, 0, 86400);
        int h = sec / 3600 % 24, m = sec % 3600 / 60, s = sec % 60;   // 86400 wraps to 00:00
        if (Tracker.Instance.Settings.Use24Hour)
            return withSeconds ? $"{h:00}:{m:00}:{s:00}" : $"{h:00}:{m:00}";
        int h12 = h % 12 == 0 ? 12 : h % 12;
        string ampm = h < 12 ? "AM" : "PM";
        return withSeconds ? $"{h12}:{m:00}:{s:00} {ampm}" : $"{h12}:{m:00} {ampm}";
    }

    /// <summary>Labels for midnight, 6am, noon and 6pm.</summary>
    public static string[] AxisLabels() => Tracker.Instance.Settings.Use24Hour
        ? new[] { "00:00", "06:00", "12:00", "18:00" }
        : new[] { "12a", "6a", "12p", "6p" };

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
