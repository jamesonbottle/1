# ScreenTime for Windows

A small WinUI 3 time tracker: how long you spent on screen, split by hour or day, with a timeline that gives every app its own horizontal lane so you can see which apps were open at the same time.

## What it shows

| Section | What you get | Built from |
|---|---|---|
| Time Track | One day at a time, with previous / next arrows | `Button`s |
| Day buttons | Click a day (Today, Yesterday, ...) to see what you were doing then | `Button` row |
| Calendar button | The calendar icon at the top right (above Today) opens a date picker to jump to any earlier day | `Flyout` + `CalendarView` |
| Timeline | One horizontal line per app. Solid = app in front, light = open in the background. Overlapping apps show up on separate lines, with a separator after the app names | `Canvas` + `Rectangle`s |
| Hover card | Hover a lane to see the exact time under the cursor, plus the start, end and length of the bar you're on | a `Border` on a `Canvas` overlay |
| Resizable names | Drag the divider between the app names and the lanes to fit long names | a custom `ResizeHandle` strip |
| Settings (left pane) | 24-hour or 12-hour time. Defaults to your system's clock format | `NavigationView` + `ToggleSwitch` |
| Pane toggle | The three-line button at the top of the left pane collapses it to icons and expands it again. The app remembers which one you left it on | `NavigationView` pane toggle |

Windows has no public screen-time API, so usage comes from sampling:

- once a second, the foreground window (`GetForegroundWindow` through the CsWin32 source generator) gives screen time and the solid timeline bars;
- every 5 seconds, every process with a visible window counts as "open", which gives the light bars. Minimized windows count as open.

Nothing is recorded while there has been no input for 5+ minutes.

Data lives in `%LOCALAPPDATA%\ScreenTime`: `settings.json` (app names, icon paths, the 24-hour setting and the name column width) plus one `yyyy-MM-dd.json` per day. Any error is appended to `crash.log` in the same folder.

Days recorded before the timeline existed only have the hourly totals, so their timeline is empty.

## Install (nothing to install)

Needs only Windows 10 1809+ or Windows 11. The .NET runtime and Windows App SDK are bundled, and the app is unpackaged (no MSIX, no certificate, no Store).

1. Download `ScreenTime-win-x64.zip` from the **Releases** page, or from the latest **Actions** run's artifacts.
2. Unzip anywhere.
3. Run `ScreenTime.exe`.

Closing or minimizing the window hides it to the tray (notification area) instead of the taskbar, and tracking keeps running. Left-click the tray icon to reopen the window; right-click it for **Open** / **Exit**. Only one copy runs at a time: launching it again just reopens the window of the running copy.

To cut a release, push a tag: `git tag v1.0 && git push origin v1.0`.

## Build from source

Needs the .NET 8 SDK (or Visual Studio 2022 with the WinUI workload):

```
dotnet run -r win-x64
```

CsWin32 is a build-time source generator only; it adds nothing to what the end user needs.

## Not in this version

- Downtime, App Limits, Always Allowed, Most Used and Pickups (removed; this build only tracks and displays time)
- Packaged-app (UWP) apps all show as "ApplicationFrameHost" and are not tracked
- Run at startup
- The tray icon isn't re-added if Explorer restarts (reopen the app to get it back)
