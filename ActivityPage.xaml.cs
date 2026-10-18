using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;

namespace ScreenTime;

public sealed partial class ActivityPage : Page
{
    const double LaneHeight = 28, BarHeight = 12;
    const int DaySeconds = 86400;

    readonly Tracker _t = Tracker.Instance;
    readonly List<Lane> _lanes = new();
    DayData? _shown;   // the day the timeline is currently drawing
    bool _week;
    int _offset;   // 0 = today / this week, -1 = previous, ...

    sealed record Lane(string Key, Canvas Canvas);

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

        // Timeline lanes only make sense for a single day.
        TimelineCard.Visibility = _week ? Visibility.Collapsed : Visibility.Visible;
        if (!_week) UpdateLanes(days[0]);

        // Most used apps across the visible range.
        var apps = days.SelectMany(d => d.Apps)
            .GroupBy(kv => kv.Key)
            .Select(g => (Key: g.Key, Sec: g.Sum(kv => kv.Value.Sum())))
            .OrderByDescending(x => x.Sec).Take(10).ToList();
        int top = apps.Count > 0 ? Math.Max(1, apps[0].Sec) : 1;
        AppsList.ItemsSource = apps.Select(x =>
            new AppUsage(x.Key, Ui.Name(x.Key), Ui.Time(x.Sec), 100.0 * x.Sec / top, Ui.Icon(x.Key))).ToList();
    }

    // ---- timeline: one horizontal lane per app ------------------------------------------

    void UpdateLanes(DayData day)
    {
        _shown = day;

        // Lanes ordered by when each app first opened, so the day reads top to bottom.
        var keys = day.Open
            .Where(kv => kv.Value.Count > 0)
            .OrderBy(kv => kv.Value[0][0]).ThenBy(kv => kv.Key)
            .Select(kv => kv.Key).ToList();

        if (!keys.SequenceEqual(_lanes.Select(l => l.Key))) BuildLanes(keys);
        foreach (var lane in _lanes) Draw(lane);

        LaneHint.Text = keys.Count == 0
            ? "Nothing recorded for this day yet."
            : "Solid = app in front. Light = open in the background.";
    }

    void BuildLanes(List<string> keys)
    {
        Lanes.Children.Clear();
        _lanes.Clear();
        foreach (var key in keys)
        {
            var canvas = new Canvas { Height = LaneHeight, HorizontalAlignment = HorizontalAlignment.Stretch };
            var lane = new Lane(key, canvas);
            canvas.SizeChanged += (_, _) => Draw(lane);   // width is only known after layout

            var label = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center };
            label.Children.Add(new Image { Source = Ui.Icon(key), Width = 20, Height = 20 });
            label.Children.Add(new TextBlock
            {
                Text = Ui.Name(key), Width = 140, VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
            });

            var row = new Grid { ColumnSpacing = 12 };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(170) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.Children.Add(label);
            Grid.SetColumn(canvas, 1);
            row.Children.Add(canvas);

            Lanes.Children.Add(row);
            _lanes.Add(lane);
        }
    }

    void Draw(Lane lane)
    {
        double w = lane.Canvas.ActualWidth;
        if (w <= 0 || _shown is null) return;

        var c = lane.Canvas;
        c.Children.Clear();

        var lineBrush = (Brush)Resources["LineBrush"];
        var laneBrush = (Brush)Resources["LaneBrush"];

        // The lane's horizontal line, plus faint ticks every 6 hours to line up with the axis labels.
        Add(c, new Rectangle { Width = w, Height = 1, Fill = lineBrush }, 0, LaneHeight / 2);
        for (int i = 1; i < 4; i++)
            Add(c, new Rectangle { Width = 1, Height = LaneHeight, Fill = lineBrush, Opacity = 0.5 }, w * i / 4, 0);

        AddSpans(c, _shown.Open, lane.Key, w, laneBrush, 0.35);
        AddSpans(c, _shown.Focus, lane.Key, w, laneBrush, 1.0);
    }

    static void AddSpans(Canvas c, Dictionary<string, List<int[]>> spans, string key, double w, Brush fill, double opacity)
    {
        if (!spans.TryGetValue(key, out var list)) return;
        foreach (var s in list.ToArray())   // copy: the tracker may append while we draw
        {
            double x = w * s[0] / DaySeconds;
            double width = Math.Max(2, w * (s[1] - s[0]) / DaySeconds);
            Add(c, new Rectangle { Width = width, Height = BarHeight, RadiusX = 3, RadiusY = 3, Fill = fill, Opacity = opacity },
                x, (LaneHeight - BarHeight) / 2);
        }
    }

    static void Add(Canvas c, UIElement e, double x, double y)
    {
        Canvas.SetLeft(e, x);
        Canvas.SetTop(e, y);
        c.Children.Add(e);
    }
}
