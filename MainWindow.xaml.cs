using Microsoft.UI.Xaml;
using Windows.Graphics;

namespace ScreenTime;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        AppWindow.Resize(new SizeInt32 { Width = 1000, Height = 800 });
        Closed += (_, _) => { try { Tracker.Instance.Save(); } catch (Exception ex) { Diag.Log(ex); } };
    }
}
