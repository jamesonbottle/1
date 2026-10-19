using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Graphics;

namespace ScreenTime;

public sealed partial class MainWindow : Window
{
    readonly TrayIcon _tray;

    public MainWindow()
    {
        InitializeComponent();
        ExtendsContentIntoTitleBar = true;   // draw into the title bar area, like Settings and Store
        SetTitleBar(AppTitleBar);
        AppWindow.Resize(new SizeInt32 { Width = 1100, Height = 800 });
        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "app.ico"));

        // The X button hides the window to the tray and tracking carries on; "Exit" in the tray menu really quits.
        _tray = new TrayIcon(this);
        AppWindow.Closing += (_, e) =>
        {
            if (_tray.Exiting) return;
            e.Cancel = true;
            AppWindow.Hide();
        };
        Closed += (_, _) =>
        {
            _tray.Remove();
            try { Tracker.Instance.Save(); } catch (Exception ex) { Diag.Log(ex); }
        };

        // Remember whether the hamburger menu left the pane open or collapsed to icons. Watching the property itself
        // catches every way of toggling it; the setting is written to disk with the rest (every 30s and on exit).
        Nav.IsPaneOpen = Tracker.Instance.Settings.PaneOpen;
        Nav.RegisterPropertyChangedCallback(NavigationView.IsPaneOpenProperty,
            (_, _) => Tracker.Instance.Settings.PaneOpen = Nav.IsPaneOpen);

        // Select the first page from code, once every named element exists (IsSelected in XAML can fire too early).
        Nav.SelectedItem = Nav.MenuItems[0];
    }

    public void ShowFromTray() => _tray.Show();

    void OnNav(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (Host is null) return;
        Host.Navigate(args.IsSettingsSelected ? typeof(SettingsPage) : typeof(ActivityPage));
    }
}
