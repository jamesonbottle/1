using Microsoft.UI.Xaml.Controls;

namespace ScreenTime;

public sealed partial class DowntimePage : Page
{
    public Settings S => Tracker.Instance.Settings;   // two-way bound in XAML, saved when leaving the page

    public DowntimePage()
    {
        InitializeComponent();
        Unloaded += (_, _) => Tracker.Instance.Save();
    }
}
