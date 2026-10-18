using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace ScreenTime;

public sealed partial class ActivityPage : Page
{
    readonly Tracker _t = Tracker.Instance;
    bool _week;
    int _offset;   // 0 = today / this week, -1 = previous, ...

    public ActivityPage()
    {
        InitializeComponent();
        Loaded += (_, _) => { _t.Changed += Refresh; Refresh(); };
        Unloaded += (_, _) => _t.Changed -= Refresh;
    }

    void OnRange(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
    {
        if (!IsLoaded) return;
        _week = sender.SelectedItem == WeekItem;
        _offset = 0;
        Refresh();
    }

    void OnPrev(object sender, RoutedEventArgs e) { _offset--; Refresh(); }
    void OnNext(object sender, RoutedEventArgs e) { _offset++; Refresh(); }

    static DateOnly Monday(DateOnly d) => d.AddDays(-(((int)d.DayOfWeek + 6) % 7));

    void Refresh()
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        var first = _week ? Monday(today).AddDays(7 * _offset) : today.AddDays(_offset);
        var days = Enumerable.Range(0, _week ? 7 : 1).Select(i => _t.Get(first.AddDays(i))).ToList();
        int elapsed = Math.Max(1, Enumerable.Range(0, days.Count).Count(i => first.AddDays(i) <= today));

        Next.IsEnabled = _offset < 0;
        DateText.Text = _week
            ? $"{first:MMM d} – {first.AddDays(6):MMM d}"
            : first == today ? $"Today, {first:MMM d}" : $"{first:dddd, MMM d}";

        int total = days.Sum(d => d.Total);
        int pickups = days.Sum(d => d.Pickups);
        TotalLabel.Text = _week ? "DAILY AVERAGE" : "SCREEN TIME";
        TotalText.Text = Ui.Time(_week ? total / elapsed : total);
        PickupsLabel.Text = _week ? "PICKUPS (DAILY AVERAGE)" : "PICKUPS";
        PickupsText.Text = (_week ? pickups / elapsed : pickups).ToString();

        // Bar chart: 24 hours for Day, 7 days for Week.
        var values = _week ? days.Select(d => d.Total).ToArray() : days[0].ByHour();
        int max = Math.Max(1, values.Max());
        string[] hourLabels = { "12a", "6a", "12p", "6p" };
        Chart.ItemsSource = values.Select((v, i) => new Bar(
            v == 0 ? 0 : Math.Max(3, 140.0 * v / max),
            _week ? 60 : 22,
            _week ? first.AddDays(i).ToString("ddd") : i % 6 == 0 ? hourLabels[i / 6] : "")).ToList();
        PeakText.Text = values.Max() > 0 ? $"Peak {Ui.Time(values.Max())}" : "";

        // Most used apps across the visible range.
        var apps = days.SelectMany(d => d.Apps)
            .GroupBy(kv => kv.Key)
            .Select(g => (Key: g.Key, Sec: g.Sum(kv => kv.Value.Sum())))
            .OrderByDescending(x => x.Sec).Take(10).ToList();
        int top = apps.Count > 0 ? Math.Max(1, apps[0].Sec) : 1;
        AppsList.ItemsSource = apps.Select(x =>
            new AppUsage(x.Key, Ui.Name(x.Key), Ui.Time(x.Sec), 100.0 * x.Sec / top, Ui.Icon(x.Key))).ToList();
    }
}
