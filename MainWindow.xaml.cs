using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Graphics;

namespace ScreenTime;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        AppWindow.Resize(new SizeInt32 { Width = 1100, Height = 800 });
        Closed += (_, _) => { try { Tracker.Instance.Save(); } catch (Exception ex) { Diag.Log(ex); } };

        // Select the first page from code, once every named element exists (IsSelected in XAML can fire too early).
        Nav.SelectedItem = Nav.MenuItems[0];
    }

    void OnNav(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (Host is not null && args.SelectedItemContainer?.Tag is string page) Host.Navigate(Type.GetType(page)!);
    }
}
