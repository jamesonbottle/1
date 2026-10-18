# ScreenTime for Windows

A WinUI 3 take on iOS Screen Time: activity charts, pickups, most-used apps, Downtime, App Limits and Always Allowed.

## iOS feature -> what it uses here

| iOS Screen Time | Here | Built from |
|---|---|---|
| All Activity (Day / Week) | Hourly / daily bar chart, total or daily average, date navigation | `SelectorBar`, `ItemsControl`, `ProgressBar` |
| Most Used | Top 10 apps with real exe icons | `StorageFile.GetThumbnailAsync` (the Explorer icon) |
| Pickups | Counted when you return from 5+ min of no input | `GetLastInputInfo` |
| Downtime | Schedule (overnight ok); non-allowed apps are closed | `ToggleSwitch`, `TimePicker` |
| App Limits | Daily minutes per app, 5-minute warning, close at limit, "Ignore for today" | `ContentDialog`, `NumberBox`, `AppNotificationManager` toasts |
| Always Allowed | Apps exempt from Downtime | `ToggleSwitch` |
| Navigation | Left pane | `NavigationView` + `Frame` |

Windows has no public screen-time API, so usage comes from sampling the foreground window once a second (`GetForegroundWindow` through the CsWin32 source generator, so no hand-written P/Invoke).

Data lives in `%LOCALAPPDATA%\ScreenTime`: `settings.json` plus one `yyyy-MM-dd.json` per day. Any error is appended to `crash.log` in the same folder.

## Install (nothing to install)

Needs only Windows 10 1809+ or Windows 11. The .NET runtime and Windows App SDK are bundled, and the app is unpackaged (no MSIX, no certificate, no Store).

1. Download `ScreenTime-win-x64.zip` (or `win-arm64`) from the **Releases** page, or from the latest **Actions** run's artifacts.
2. Unzip anywhere.
3. Run `ScreenTime.exe`.

The window must stay open (or minimized) for tracking.

To cut a release, push a tag: `git tag v1.0 && git push origin v1.0`.

## Build from source

Needs the .NET 8 SDK (or Visual Studio 2022 with the WinUI workload):

```
dotnet run -r win-x64
```

CsWin32 is a build-time source generator only; it adds nothing to what the end user needs.

## Not in this version

- Notification counts, app categories, Communication Limits, Content & Privacy restrictions, multi-device sync, Screen Time passcode
- Packaged-app (UWP) apps all show as "ApplicationFrameHost" and are not tracked or limited
- Enforcement is a polite close (`CloseMainWindow`) of the foreground app; a determined user can quit this app or use Task Manager
- Tray icon / run at startup
