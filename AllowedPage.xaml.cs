using Microsoft.UI.Xaml.Controls;

namespace ScreenTime;

public sealed partial class AllowedPage : Page
{
    public AllowedPage()
    {
        InitializeComponent();
        var s = Tracker.Instance.Settings;
        List.ItemsSource = s.Apps.Keys.OrderBy(Ui.Name).Select(k => new AllowedRow(k, s)).ToList();
        Unloaded += (_, _) => Tracker.Instance.Save();
    }
}
