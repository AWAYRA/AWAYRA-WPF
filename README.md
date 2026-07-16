# Awayra

Awayra is a lightweight native Windows application that helps you take healthy breaks during long computer sessions. It runs primarily in the system tray and provides independent Eye Reset and Move Break reminders.

## Features

- System tray application with dashboard, settings, and fullscreen break overlays
- Independent Eye Reset and Move Break schedulers
- Pause, resume, skip, snooze, idle detection, and work-hours restrictions
- English, Persian (RTL), and Arabic (RTL) localization
- Local JSON persistence under `%LocalAppData%\Awayra\`
- No server, account, cloud, telemetry, or internet dependency

## Architecture

- `Awayra.Core` — domain models, scheduler, validation, statistics, localization abstractions
- `Awayra.App` — WPF UI, tray, overlays, Win32 interop, persistence implementations
- `Awayra.Core.Tests` — deterministic MSTest suite for business logic

Stack: C#, .NET 10, WPF, MVVM (CommunityToolkit.Mvvm), System.Windows.Forms.NotifyIcon

## Requirements

- Windows 10/11 x64
- .NET 10 SDK for development

## Development

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\dev.ps1
```

## Tests

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\test.ps1
```

Focused example:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\test.ps1 -Filter "FullyQualifiedName~BreakScheduler"
```

## Release publish

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\publish.ps1
```

Output executable:

`D:\curser\AWAYRA-WPF\artifacts\publish\win-x64\Awayra.exe`

Self-contained single-file `win-x64` build. No installer is produced.

## Local data

- Settings: `%LocalAppData%\Awayra\settings.json`
- Scheduler state: `%LocalAppData%\Awayra\state.json`
- Statistics: `%LocalAppData%\Awayra\stats.json`
- Logs: `%LocalAppData%\Awayra\Logs\awayra.log`

## Tray behavior

- Closing the dashboard hides Awayra to the tray when **Close dashboard to tray** is enabled
- Left-click or double-click the tray icon to open the dashboard
- Use **Quit** in the tray menu to fully exit, flush data, and remove the tray icon

## Known limitations

- Overlay backdrop effects depend on Windows DWM support and fall back to translucent panels when unavailable
- Tray tooltip text is truncated to 63 characters per Windows NotifyIcon limits
