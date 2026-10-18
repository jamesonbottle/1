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
        Closed += (_, _) => Tracker.Instance.Save();
    }

    void OnNav(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItemContainer?.Tag is string page) Host.Navigate(Type.GetType(page)!);
    }
}
