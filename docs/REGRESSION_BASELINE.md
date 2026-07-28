# Awayra Regression Baseline

Recorded: 2026-07-17 (UTC+4)

## Git status

```
 M src/Awayra.App/App.xaml.cs
 M src/Awayra.App/Interop/NativeMethods.cs
 M src/Awayra.App/Resources/Theme.xaml
 M src/Awayra.App/ViewModels/MainViewModel.cs
 M src/Awayra.App/Views/BreakOverlayWindow.xaml
 M src/Awayra.App/Views/MainWindow.xaml
 M src/Awayra.App/Views/SettingsWindow.xaml
```

Seven modified files in the working tree (theme/contrast and dashboard visibility fixes). No commits recorded for these changes at baseline time.

## Build result

- `dotnet restore Awayra.sln` — succeeded
- `dotnet build Awayra.sln -c Debug` — succeeded (0 warnings, 0 errors)

## Test count

- **44** tests in `Awayra.Core.Tests` — all passed (baseline before regression hardening)

## Post-hardening test count

- **85** total tests (72 Core + 13 App) — all passed
- `scripts/verify-change.ps1` — REGRESSION VERIFICATION: PASSED

## .NET SDK

- **10.0.302** (pinned via `global.json`)

## Latest log excerpt

From `%LocalAppData%\Awayra\Logs\awayra.log` (2026-07-17):

- Earlier `XamlParseException` at `Theme.xaml` line 135 from invalid style setters (fixed in working tree).
- Latest successful sessions: `Dashboard window created.` → `Awayra started.`
- Second-instance signaling: `Second instance signaled existing instance and exiting.`
- Quit path: `Quit requested from tray.` → `Awayra shutting down.`

## Known working behaviors

- Dashboard creates and shows on startup when `StartMinimized=false`
- Tray icon appears; Open Awayra / tray click restores dashboard
- Close to tray hides dashboard without exiting
- Single-instance mutex and named-pipe activation
- Eye Reset and Move Break overlays open from tray/dashboard
- Settings window opens with light/dark contrast theme
- Scheduler timers, pause/resume, skip/snooze, idle pause, work hours
- Settings persistence under `%LocalAppData%\Awayra\`
- English, Persian, and Arabic localization with RTL for fa/ar

## Known limitations

- Settings XAML labels for some sections remain hardcoded English (not all bound to localization)
- Dispatcher unhandled exceptions are logged and marked handled (app continues)
- `scripts/dev.ps1` stops all `Awayra` processes globally (not repo-scoped)
- Release publish artifact exists from prior work; not part of routine Debug iteration
