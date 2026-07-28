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

## Windows installer (compiled release)

The latest **compiled, self-contained Windows build** is published on GitHub Releases. You do **not** need the .NET SDK or any development tools to install or run it.

**Download:** [GitHub Releases](https://github.com/AAA-It-uae/AWAYRA-WPF/releases/latest)

| Asset | Purpose |
|---|---|
| `Awayra-Setup-1.0.0-x64.exe` | Per-user Windows installer (recommended) |
| `Awayra-Setup-1.0.0-x64.sha256.txt` | SHA-256 checksum for the installer |
| `BUILD-INFO.txt` | Build metadata (commit, SDK, signing status) |

This installer is built from the source code in this repository (`scripts/build-installer.ps1`). It packages a **self-contained** `win-x64` single-file `Awayra.exe` with the .NET runtime embedded, so recipients do not need to install .NET separately.

- **Default install location:** `%LocalAppData%\Programs\Awayra`
- **Architecture:** Windows 10/11 x64
- **User data** (`%LocalAppData%\Awayra\`) is preserved across upgrade and uninstall

Build the same installer locally:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\build-installer.ps1
```

## Release publish (developer)

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\publish.ps1
```

Output executable:

`artifacts\publish\win-x64\Awayra.exe`

Self-contained single-file `win-x64` build. Use `scripts/build-installer.ps1` to wrap it in a Windows installer.

## Local data

- Settings: `%LocalAppData%\Awayra\settings.json`
- Scheduler state: `%LocalAppData%\Awayra\state.json`
- Statistics: `%LocalAppData%\Awayra\stats.json`
- Logs: `%LocalAppData%\Awayra\Logs\awayra.log`

## Tray behavior

- Closing the dashboard hides Awayra to the tray when **Close dashboard to tray** is enabled
- Left-click or double-click the tray icon to open the dashboard
- Use **Quit** in the tray menu to fully exit, flush data, and remove the tray icon

## Support Awayra

Awayra is free and open source.

If it helps you, you can optionally support its continued development.

[Support Awayra](https://www.buymeacoffee.com/YOUR_USERNAME)

## Known limitations

- Overlay backdrop effects depend on Windows DWM support and fall back to translucent panels when unavailable
- Tray tooltip text is truncated to 63 characters per Windows NotifyIcon limits
