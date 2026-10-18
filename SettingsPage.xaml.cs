using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace ScreenTime;

public sealed partial class SettingsPage : Page
{
    public SettingsPage()
    {
        InitializeComponent();
        NavigationCacheMode = NavigationCacheMode.Required;
    }

    // Read the setting every time the page is shown, so it never shows a stale value.
    protected override void OnNavigatedTo(NavigationEventArgs e) =>
        Hour24Toggle.IsOn = Tracker.Instance.Settings.Use24Hour;

    void OnHour24(object sender, RoutedEventArgs e) =>
        Tracker.Instance.Settings.Use24Hour = Hour24Toggle.IsOn;   // saved with the other settings; the Time Track page reads it on show
}
