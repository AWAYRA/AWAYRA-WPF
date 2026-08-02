<div align="center">

# Awayra — Windows Break Reminder

**A free, open-source 20-20-20 eye timer and movement reminder for Windows 10 and 11.**

![Windows](https://img.shields.io/badge/Windows-10%20%7C%2011-0078D4?logo=windows11&logoColor=white)
![.NET](https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet&logoColor=white)
![License](https://img.shields.io/badge/License-GPL--3.0--only-blue)
![Privacy](https://img.shields.io/badge/Privacy-Local--only-2ea44f)

[**Download Awayra for Windows**](https://github.com/AWAYRA/AWAYRA-WPF/releases/latest/download/Awayra-Setup-x64.exe) · [Release notes](https://github.com/AWAYRA/AWAYRA-WPF/releases/latest) · [Report a bug](https://github.com/AWAYRA/AWAYRA-WPF/issues)

</div>

## Download

| File | Purpose |
|---|---|
| [**Awayra-Setup-x64.exe**](https://github.com/AWAYRA/AWAYRA-WPF/releases/latest/download/Awayra-Setup-x64.exe) | Latest self-contained Windows x64 installer |
| [Awayra-Setup-x64.sha256.txt](https://github.com/AWAYRA/AWAYRA-WPF/releases/latest/download/Awayra-Setup-x64.sha256.txt) | SHA-256 verification |
| [GitHub Releases](https://github.com/AWAYRA/AWAYRA-WPF/releases) | Version history and release notes |

The installer includes the required .NET runtime. Official executable files are distributed only through this repository's GitHub Releases. Unsigned builds can display a Windows SmartScreen warning; verify the published SHA-256 checksum before installation.

## Features

- Independent Eye Reset and Move Break schedules
- Fullscreen break overlays with pause, skip, snooze, and complete controls
- Optional sound for each reminder
- Four locally generated sounds: Soft bell, Gentle chime, Calm drop, and an original Calm piano loop
- Configurable volume and repeat interval
- Per-break mute and unmute control
- Idle detection and optional work-hour restrictions
- Windows startup, start-minimized, and tray behavior
- Daily break statistics
- Per-monitor DPI support and stabilized recovery after monitor wake, lock/unlock, resume, or display changes
- Offline operation with no account, advertising, telemetry, or cloud dependency

## Default schedule

| Reminder | Interval | Duration |
|---|---:|---:|
| Eye Reset | 20 minutes | 20 seconds |
| Move Break | 45 minutes | 60 seconds |

All intervals and durations are configurable.

## Privacy

Awayra stores settings and runtime information locally under `%LocalAppData%\Awayra\`. It does not upload screenshots, browsing history, application usage, or personal information.

## Installation behavior

Awayra uses a per-user installation at `%LocalAppData%\Programs\Awayra` and does not require administrator access. Version 1.1.1 performs a clean replacement: it stops an old Awayra process and removes stale program files, settings, scheduler state, logs, shortcuts, and startup registration before installing the new version. Uninstall removes Awayra-owned application data.

## Development

Requirements:

- Windows 10 or Windows 11 x64
- .NET 10 SDK
- PowerShell
- Inno Setup 7 for installer builds

Run locally:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\dev.ps1
```

Build and test:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\verify-change.ps1
```

Build the installer:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\build-installer.ps1
```

Generated installers, executables, certificates, and release directories must not be committed. GitHub Actions builds and validates the installer before merge and publishes release assets from `main` after an intentional version bump.

## Repository structure

| Project | Responsibility |
|---|---|
| `src/Awayra.Core` | Scheduling, settings, validation, statistics, and domain logic |
| `src/Awayra.App` | WPF UI, tray integration, overlays, persistence, sound, and Windows interop |
| `tests/Awayra.Core.Tests` | Platform-neutral domain tests |
| `tests/Awayra.App.Tests` | WPF application and service tests |
| `tests/Awayra.UiTests` | Windows UI automation tests |

## Contributing and security

Read [CONTRIBUTING.md](CONTRIBUTING.md) before contributing. Security reports must follow [SECURITY.md](SECURITY.md). Release history is in [CHANGELOG.md](CHANGELOG.md), and use of the Awayra name and logo is covered by [TRADEMARKS.md](TRADEMARKS.md).

## License

Awayra is licensed under **GPL-3.0-only**. See [LICENSE](LICENSE).

Copyright © 2026 Farzin Alavi.

> Awayra is a wellness reminder, not a medical device. Persistent eye pain, severe headaches, double vision, numbness, or ongoing musculoskeletal pain should be assessed by a qualified professional.