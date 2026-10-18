using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace ScreenTime;

public sealed partial class LimitsPage : Page
{
    readonly Tracker _t = Tracker.Instance;

    public LimitsPage()
    {
        InitializeComponent();
        Loaded += (_, _) => Refresh();
        Unloaded += (_, _) => _t.Save();
    }

    // Not refreshed every second on purpose: rebuilding the list would swallow button clicks.
    void Refresh() => List.ItemsSource = _t.Settings.Limits.Select(l =>
    {
        bool ignored = _t.Today.Ignored.Contains(l.App);
        string detail = ignored
            ? "Limit ignored for today"
            : $"{Ui.Time(_t.Today.Seconds(l.App))} of {Ui.Time(l.Minutes * 60)} today";
        return new LimitRow(l.App, Ui.Name(l.App), detail, Ui.Icon(l.App), !ignored);
    }).ToList();

    async void OnAdd(object sender, RoutedEventArgs e)
    {
        AppPick.ItemsSource = _t.Settings.Apps
            .Select(kv => new AppChoice(kv.Key, kv.Value.Name)).OrderBy(a => a.Name).ToList();
        AddDialog.XamlRoot = XamlRoot;
        if (await AddDialog.ShowAsync() == ContentDialogResult.Primary && AppPick.SelectedItem is AppChoice c)
        {
            _t.Settings.Limits.RemoveAll(l => l.App == c.Key);
            _t.Settings.Limits.Add(new() { App = c.Key, Minutes = (int)Minutes.Value });
            Refresh();
        }
    }

    void OnIgnore(object sender, RoutedEventArgs e)
    {
        _t.Today.Ignored.Add((string)((FrameworkElement)sender).Tag);
        Refresh();
    }

    void OnRemove(object sender, RoutedEventArgs e)
    {
        var key = (string)((FrameworkElement)sender).Tag;
        _t.Settings.Limits.RemoveAll(l => l.App == key);
        Refresh();
    }
}
