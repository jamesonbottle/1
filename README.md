# ScreenTime for Windows

A small WinUI 3 time tracker: how long you spent on screen, split by hour or day, with a timeline that gives every app its own horizontal lane so you can see which apps were open at the same time.

## What it shows

| Section | What you get | Built from |
|---|---|---|
| Time Track (Day / Week) | Hourly / daily bar chart, total or daily average, date navigation | `SelectorBar`, `ItemsControl` |
| Timeline (Day only) | One horizontal line per app. Solid = app in front, light = open in the background. Overlapping apps show up on separate lines | `Canvas` + `Rectangle`s |
| Pickups | Counted when you return from 5+ min of no input | `GetLastInputInfo` |
| Most Used | Top 10 apps with real exe icons | `StorageFile.GetThumbnailAsync` (the Explorer icon) |

Windows has no public screen-time API, so usage comes from sampling:

- once a second, the foreground window (`GetForegroundWindow` through the CsWin32 source generator) gives screen time and the solid timeline bars;
- every 5 seconds, every process with a visible window counts as "open", which gives the light bars. Minimized windows count as open.

Nothing is recorded while there has been no input for 5+ minutes.

Data lives in `%LOCALAPPDATA%\ScreenTime`: `settings.json` (app names and icon paths) plus one `yyyy-MM-dd.json` per day. Any error is appended to `crash.log` in the same folder.

Days recorded before the timeline existed only have the hourly totals, so their timeline is empty.

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

- Downtime, App Limits and Always Allowed (removed; this build only tracks and displays time)
- Packaged-app (UWP) apps all show as "ApplicationFrameHost" and are not tracked
- Tray icon / run at startup
