using Microsoft.UI.Xaml;
using Microsoft.Windows.AppNotifications;

namespace ScreenTime;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();
        UnhandledException += (_, e) => Diag.Log(e.Exception);   // so a crash leaves a trace
        try { AppNotificationManager.Default.Register(); } catch (Exception ex) { Diag.Log(ex); }   // lets us show toasts
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        try
        {
            new MainWindow().Activate();
            Tracker.Instance.Start();
        }
        catch (Exception ex)
        {
            Diag.Log(ex);
            throw;
        }
    }
}

/// <summary>Appends errors to %LOCALAPPDATA%\ScreenTime\crash.log. Separate class so logging never touches Tracker's static init.</summary>
static class Diag
{
    public static void Log(object? error)
    {
        try
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ScreenTime");
            Directory.CreateDirectory(dir);
            File.AppendAllText(Path.Combine(dir, "crash.log"), $"[{DateTime.Now:s}] {error}{Environment.NewLine}{Environment.NewLine}");
        }
        catch { }
    }
}
