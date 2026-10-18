using Microsoft.UI.Xaml;
using Microsoft.Windows.AppNotifications;

namespace ScreenTime;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();
        try { AppNotificationManager.Default.Register(); } catch { }   // lets us show toasts
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        new MainWindow().Activate();
        Tracker.Instance.Start();
    }
}
