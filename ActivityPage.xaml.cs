using Microsoft.UI;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;

namespace ScreenTime;

/// <summary>A transparent strip that shows the west-east resize cursor, used as the timeline's column divider.</summary>
public sealed class ResizeHandle : Grid
{
    public ResizeHandle() => ProtectedCursor = InputSystemCursor.Create(InputSystemCursorShape.SizeWestEast);
}

public sealed partial class ActivityPage : Page
{
    const double LaneHeight = 36, BarHeight = 12;   // 36 + the 4px StackPanel spacing = 40px per row, like the Settings nav list
    const double LaneGap = 12;                 // spacing between the name column and the lanes
    const double MinLabelWidth = 90;
    const int DaySeconds = 86400;

    readonly Tracker _t = Tracker.Instance;
    readonly List<Lane> _lanes = new();
    DayData? _shown;   // the day the timeline is currently drawing
    bool _week;
    int _offset;   // 0 = today / this week, -1 = previous, ...

    double _labelWidth;
    bool _dragging;
    double _dragStartX, _dragStartWidth;

    bool _suppressRange;      // true while we change Day/Week from code, so OnRange doesn't reset the date
    string _stripKey = "";    // what the day buttons currently show; they're only rebuilt when this changes

    sealed record Lane(string Key, Canvas Canvas, Grid Row);

    public ActivityPage()
    {
        InitializeComponent();
        NavigationCacheMode = NavigationCacheMode.Required;   // keep Day/Week and the date when switching to Settings and back
        _labelWidth = Math.Max(MinLabelWidth, _t.Settings.LabelWidth);
        ApplyLabelWidth();
        Loaded += (_, _) => { _t.Changed += Refresh; Refresh(); };
        Unloaded += (_, _) => _t.Changed -= Refresh;
    }

    void OnRange(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
    {
        if (!IsLoaded || _suppressRange) return;
        _week = sender.SelectedItem == WeekItem;
        _offset = 0;
        Refresh();
    }

    void OnPrev(object sender, RoutedEventArgs e) { _offset--; Refresh(); }
    void OnNext(object sender, RoutedEventArgs e) { _offset++; Refresh(); }

    // ---- day buttons: click a day to see what you were doing then ------------------------

    /// <summary>Jump to a single day. From the Week view this also switches to the Day view.</summary>
    void SelectDay(DateOnly day)
    {
        _offset = day.DayNumber - DateOnly.FromDateTime(DateTime.Now).DayNumber;
        if (_week)
        {
            _week = false;
            _suppressRange = true;
            Range.SelectedItem = DayItem;
            _suppressRange = false;
        }
        Refresh();
    }

    // Day view: the 7 days ending today (or, if you've gone further back, the 7 days starting at the selected one).
    // Week view: the 7 days of the shown week; clicking one opens that day.
    void UpdateDayStrip(DateOnly today, DateOnly first)
    {
        DateOnly start;
        DateOnly? selected = null;
        if (_week) start = first;
        else
        {
            var sel = today.AddDays(_offset);
            selected = sel;
            start = (_offset >= -6 ? today : sel.AddDays(6)).AddDays(-6);
        }

        string key = $"{start.DayNumber}|{selected?.DayNumber}|{today.DayNumber}|{_week}";
        if (key == _stripKey) return;   // Refresh runs every second; rebuilding would swallow clicks
        _stripKey = key;

        DayStrip.Children.Clear();
        DayStrip.ColumnDefinitions.Clear();
        for (int i = 0; i < 7; i++)
        {
            var day = start.AddDays(i);
            DayStrip.ColumnDefinitions.Add(new ColumnDefinition());   // equal widths

            var name = new TextBlock
            {
                Text = day == today ? "Today" : day == today.AddDays(-1) ? "Yesterday" : day.ToString("ddd"),
                FontSize = 12, HorizontalAlignment = HorizontalAlignment.Center,
            };
            var date = new TextBlock { Text = day.ToString("d MMM"), FontSize = 11, Opacity = 0.7, HorizontalAlignment = HorizontalAlignment.Center };

            var button = new Button
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Padding = new Thickness(4, 8, 4, 8),
                IsEnabled = day <= today,   // can't open the future
                Content = new StackPanel { Spacing = 2, Children = { name, date } },
            };
            if (day == selected) button.Style = (Style)Application.Current.Resources["AccentButtonStyle"];
            button.Click += (_, _) => SelectDay(day);

            Grid.SetColumn(button, i);
            DayStrip.Children.Add(button);
        }
    }

    static DateOnly Monday(DateOnly d) => d.AddDays(-(((int)d.DayOfWeek + 6) % 7));

    void Refresh()
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        var first = _week ? Monday(today).AddDays(7 * _offset) : today.AddDays(_offset);

        UpdateDayStrip(today, first);
        Next.IsEnabled = _offset < 0;
        DateText.Text = _week
            ? $"{first:MMM d} – {first.AddDays(6):MMM d}"
            : first == today ? $"Today, {first:MMM d}" : $"{first:dddd, MMM d}";

        // Hour labels follow the 12 / 24 hour setting.
        var hourLabels = Ui.AxisLabels();
        Ax0.Text = hourLabels[0]; Ax1.Text = hourLabels[1]; Ax2.Text = hourLabels[2]; Ax3.Text = hourLabels[3];

        // Timeline lanes only make sense for a single day.
        TimelineCard.Visibility = _week ? Visibility.Collapsed : Visibility.Visible;
        if (!_week) UpdateLanes(_t.Get(first));
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
            : "Solid = app in front. Light = open in the background. Drag the divider to resize the names.";
    }

    void BuildLanes(List<string> keys)
    {
        Lanes.Children.Clear();
        _lanes.Clear();
        foreach (var key in keys)
        {
            var canvas = new Canvas
            {
                Height = LaneHeight,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Background = new SolidColorBrush(Colors.Transparent),   // needed so the canvas receives pointer events
            };

            var name = new TextBlock { Text = Ui.Name(key), VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis };
            ToolTipService.SetToolTip(name, Ui.Name(key));
            var icon = new Image { Source = Ui.Icon(key), Width = 20, Height = 20 };
            var label = new Grid { ColumnSpacing = 8, VerticalAlignment = VerticalAlignment.Center };
            label.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            label.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            label.Children.Add(icon);
            Grid.SetColumn(name, 1);
            label.Children.Add(name);

            var row = new Grid { ColumnSpacing = LaneGap };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(_labelWidth) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.Children.Add(label);
            Grid.SetColumn(canvas, 1);
            row.Children.Add(canvas);

            var lane = new Lane(key, canvas, row);
            canvas.SizeChanged += (_, _) => Draw(lane);   // width is only known after layout
            canvas.PointerMoved += (_, e) => ShowHover(lane, e);
            canvas.PointerExited += (_, _) => HoverCard.Visibility = Visibility.Collapsed;

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

        // The lane's horizontal line, then the open (light) and in-front (solid) bars on top of it.
        Add(c, new Rectangle { Width = w, Height = 1, Fill = (Brush)Resources["LineBrush"] }, 0, LaneHeight / 2);

        var laneBrush = (Brush)Resources["LaneBrush"];
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
        e.IsHitTestVisible = false;   // otherwise moving across a bar raises PointerExited on the canvas and flickers the hover card
        Canvas.SetLeft(e, x);
        Canvas.SetTop(e, y);
        c.Children.Add(e);
    }

    // ---- hover card ---------------------------------------------------------------------

    static int[]? FindSpan(Dictionary<string, List<int[]>> spans, string key, int sec, int tolerance) =>
        spans.TryGetValue(key, out var list)
            ? list.ToArray().FirstOrDefault(s => sec >= s[0] - tolerance && sec <= s[1] + tolerance)
            : null;

    void ShowHover(Lane lane, PointerRoutedEventArgs e)
    {
        var c = lane.Canvas;
        if (c.ActualWidth <= 0 || _shown is null) return;

        int sec = (int)Math.Clamp(e.GetCurrentPoint(c).Position.X / c.ActualWidth * DaySeconds, 0, DaySeconds - 1);
        int tolerance = (int)Math.Ceiling(3 / c.ActualWidth * DaySeconds);   // a few pixels of slack so thin bars are easy to hit

        var span = FindSpan(_shown.Focus, lane.Key, sec, tolerance);
        bool front = span is not null;
        span ??= FindSpan(_shown.Open, lane.Key, sec, tolerance);

        HoverTitle.Text = Ui.Name(lane.Key);
        HoverTime.Text = Ui.Clock(sec, withSeconds: true);
        HoverDetail.Text = span is null
            ? "Not open at this time"
            : $"{(front ? "In front" : "Open in background")}: {Ui.Clock(span[0], true)} – {Ui.Clock(span[1], true)} ({Ui.Time(span[1] - span[0])})";

        // Place the card centered above the pointer; flip below it if there's no room at the top.
        HoverCard.Visibility = Visibility.Visible;
        HoverCard.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var size = HoverCard.DesiredSize;
        var p = e.GetCurrentPoint(HoverLayer).Position;
        double left = Math.Clamp(p.X - size.Width / 2, 4, Math.Max(4, HoverLayer.ActualWidth - size.Width - 4));
        double top = p.Y - size.Height - 16;
        if (top < 4) top = p.Y + 24;
        Canvas.SetLeft(HoverCard, left);
        Canvas.SetTop(HoverCard, top);
    }

    // ---- resizable name column ----------------------------------------------------------

    void ApplyLabelWidth()
    {
        Axis.Margin = new Thickness(_labelWidth + LaneGap, 0, 0, 0);
        Handle.Margin = new Thickness(_labelWidth + LaneGap / 2 - Handle.Width / 2, 0, 0, 0);
        foreach (var l in _lanes) l.Row.ColumnDefinitions[0].Width = new GridLength(_labelWidth);
    }

    void OnHandleDown(object sender, PointerRoutedEventArgs e)
    {
        _dragging = true;
        _dragStartX = e.GetCurrentPoint(TimelineBody).Position.X;
        _dragStartWidth = _labelWidth;
        Handle.CapturePointer(e.Pointer);
        e.Handled = true;
    }

    void OnHandleMove(object sender, PointerRoutedEventArgs e)
    {
        if (!_dragging) return;
        double dx = e.GetCurrentPoint(TimelineBody).Position.X - _dragStartX;
        // Keep at least 200px for the lanes themselves.
        double max = TimelineBody.ActualWidth > 0 ? Math.Max(MinLabelWidth, TimelineBody.ActualWidth - 200) : double.MaxValue;
        _labelWidth = Math.Clamp(_dragStartWidth + dx, MinLabelWidth, max);
        ApplyLabelWidth();
    }

    void OnHandleUp(object sender, PointerRoutedEventArgs e)
    {
        if (!_dragging) return;
        _dragging = false;
        Handle.ReleasePointerCapture(e.Pointer);
        _t.Settings.LabelWidth = _labelWidth;   // saved with the other settings
    }
}
