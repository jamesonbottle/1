using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Shell;
using Windows.Win32.UI.WindowsAndMessaging;
using WinRT.Interop;

namespace ScreenTime;

/// <summary>
/// Notification-area (tray) icon. Closing the window hides it instead and the app keeps tracking.
/// Left-click the icon to bring the window back; right-click for Open / Exit.
/// WinUI 3 has no tray API, so this is the plain Shell_NotifyIcon call (through CsWin32, like the rest of the Win32 code).
/// </summary>
sealed class TrayIcon
{
    const uint WM_TRAY = 0x8001;                       // WM_APP + 1: the shell posts this to our window for mouse events on the icon
    const int WM_LBUTTONUP = 0x0202, WM_RBUTTONUP = 0x0205;
    const uint WM_SYSCOMMAND = 0x0112;
    const nuint SC_MINIMIZE = 0xF020;                  // the low 4 bits of a WM_SYSCOMMAND wParam are used by Windows, so mask them off
    const uint CmdOpen = 1, CmdExit = 2;

    readonly Window _window;
    readonly HWND _hwnd;
    readonly SUBCLASSPROC _proc;                       // hold a reference, or the GC frees the callback
    NOTIFYICONDATAW _nid;

    /// <summary>True once Exit was chosen, so the window's Close button knows to really close.</summary>
    public bool Exiting { get; private set; }

    public TrayIcon(Window window)
    {
        _window = window;
        _hwnd = new HWND(WindowNative.GetWindowHandle(window));
        _proc = WndProc;
        PInvoke.SetWindowSubclass(_hwnd, _proc, 1, 0);

        _nid = new NOTIFYICONDATAW
        {
            cbSize = NidSize(),
            hWnd = _hwnd,
            uID = 1,
            uFlags = NOTIFY_ICON_DATA_FLAGS.NIF_MESSAGE | NOTIFY_ICON_DATA_FLAGS.NIF_ICON | NOTIFY_ICON_DATA_FLAGS.NIF_TIP,
            uCallbackMessage = WM_TRAY,
            hIcon = LoadAppIcon(),
        };

        // szTip is a fixed 128-char buffer; view it as a span so we can copy the tooltip text in.
        var tip = MemoryMarshal.CreateSpan(ref Unsafe.As<__char_128, char>(ref _nid.szTip), 128);
        "Screen Time".AsSpan().CopyTo(tip);

        PInvoke.Shell_NotifyIcon(NOTIFY_ICON_MESSAGE.NIM_ADD, _nid);
    }

    static uint NidSize() { unsafe { return (uint)sizeof(NOTIFYICONDATAW); } }

    // Loads app.ico (copied next to the exe) straight from disk.
    static unsafe HICON LoadAppIcon()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "app.ico");
        fixed (char* p = path)
        {
            var h = PInvoke.LoadImage(HINSTANCE.Null, new PCWSTR(p), GDI_IMAGE_TYPE.IMAGE_ICON, 0, 0,
                IMAGE_FLAGS.LR_LOADFROMFILE | IMAGE_FLAGS.LR_DEFAULTSIZE);
            return new HICON(h.Value);
        }
    }

    static unsafe void AddItem(HMENU menu, uint id, string text)
    {
        fixed (char* p = text) PInvoke.AppendMenu(menu, MENU_ITEM_FLAGS.MF_STRING, id, new PCWSTR(p));
    }

    public void Show()
    {
        _window.AppWindow.Show();
        // Only restore a minimized window; Restore() on a maximized one would shrink it back to its normal size.
        if (_window.AppWindow.Presenter is OverlappedPresenter { State: OverlappedPresenterState.Minimized } p) p.Restore();
        _window.Activate();
    }

    public void Remove() => PInvoke.Shell_NotifyIcon(NOTIFY_ICON_MESSAGE.NIM_DELETE, _nid);

    LRESULT WndProc(HWND hWnd, uint msg, WPARAM wParam, LPARAM lParam, nuint id, nuint data)
    {
        // The minimize button (and a taskbar click on the active window) send SC_MINIMIZE. Treat it like the X button:
        // hide to the tray instead of minimizing to the taskbar.
        if (msg == WM_SYSCOMMAND && (wParam.Value & 0xFFF0) == SC_MINIMIZE)
        {
            _window.AppWindow.Hide();
            return new LRESULT(0);
        }

        if (msg == WM_TRAY)
        {
            switch ((int)lParam.Value & 0xFFFF)   // the mouse message is in the low word
            {
                case WM_LBUTTONUP: Show(); break;
                case WM_RBUTTONUP: ShowMenu(); break;
            }
            return new LRESULT(0);
        }
        return PInvoke.DefSubclassProc(hWnd, msg, wParam, lParam);
    }

    unsafe void ShowMenu()
    {
        var menu = PInvoke.CreatePopupMenu();
        AddItem(menu, CmdOpen, "Open Screen Time");
        AddItem(menu, CmdExit, "Exit");

        PInvoke.GetCursorPos(out var pt);
        PInvoke.SetForegroundWindow(_hwnd);   // without this the menu doesn't close when you click elsewhere
        const uint TPM_RIGHTBUTTON = 0x0002, TPM_RETURNCMD = 0x0100;   // return the chosen command instead of posting it
        int cmd = PInvoke.TrackPopupMenuEx(menu, TPM_RETURNCMD | TPM_RIGHTBUTTON, pt.X, pt.Y, _hwnd, null).Value;
        PInvoke.DestroyMenu(menu);

        if (cmd == CmdOpen) Show();
        else if (cmd == CmdExit) Exit();
    }

    void Exit()
    {
        Exiting = true;
        try { Tracker.Instance.Save(); } catch (Exception ex) { Diag.Log(ex); }
        Remove();   // otherwise a ghost icon lingers until you hover over it
        Application.Current.Exit();
    }
}
