using Microsoft.UI.Xaml;

namespace ScreenTime;

public partial class App : Application
{
    MainWindow? _window;
    Mutex? _mutex;   // held for the whole process lifetime

    public App()
    {
        InitializeComponent();
        UnhandledException += (_, e) => Diag.Log(e.Exception);   // so a crash leaves a trace
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        try
        {
            // One instance only. Closing the window just hides it to the tray, so a second launch would
            // run a second tracker and double-count. Instead, ask the running copy to show its window.
            _mutex = new Mutex(true, "ScreenTime.SingleInstance", out bool first);
            var show = new EventWaitHandle(false, EventResetMode.AutoReset, "ScreenTime.Show");
            if (!first)
            {
                show.Set();
                Environment.Exit(0);
            }

            _window = new MainWindow();
            _window.Activate();
            ThreadPool.RegisterWaitForSingleObject(show,
                (_, _) => _window.DispatcherQueue.TryEnqueue(_window.ShowFromTray), null, Timeout.Infinite, false);
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
