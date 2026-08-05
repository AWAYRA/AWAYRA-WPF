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
- Guided eye exercise: an animated focus cue and ten counted blinks
- Guided movement routine: an animated stand, walk, stretch, and return
- Optional sound for each reminder
- Six locally generated sounds, including two soft melodies that fade in from silence rather than startling you
- Configurable volume and repeat interval
- Per-break mute and unmute control
- Idle detection and optional work-hour restrictions
- Windows startup, start-minimized, and tray behavior
- Daily break statistics
- Per-monitor DPI support and stabilized recovery after monitor wake, lock/unlock, resume, or display changes
- Reduced-motion setting that replaces every animation with static guidance
- Offline operation with no account, advertising, telemetry, or cloud dependency

The interface is English only.

## Break exercises

Each break shows a guided animation instead of a bare countdown.

**Eye Reset** draws an eye with expanding focus rings that prompt you to send your focus into the distance and bring it back, alongside ten complete blinks counted on screen. One blink every two seconds means a default 20-second break delivers exactly ten.

**Move Break** shows a figure typing at a desk who stands, turns, walks away while the camera follows, reaches up to stretch, bends side to side and rolls their shoulders. They then turn to face you for three squats and three jumps, before walking back and sitting down. The loop begins and ends in the same pose, so it plays as a continuous round trip.

Both are controlled from **Settings → Break sound and exercise**:

- **Show the guided exercise animation** is on by default. Turn it off for a plain countdown.
- **Reduced motion**, under Appearance, keeps the illustration but replaces the movement with a single line of written guidance.

## Sounds

All six sounds are generated locally at first use. Nothing is downloaded and no audio file ships with the application.

| Sound | Character |
|---|---|
| Soft bell, Gentle chime, Calm drop | Short alert tones |
| Calm piano | A four-note piano phrase |
| **Morning dew** | A five-note phrase that rises and returns, fading in from silence |
| **Still water** | The same shape an octave lower and slower |

The two melodies are deliberately quieter than the alert tones and begin below one twentieth of their peak volume, so a break never announces itself with a jolt.

## Default schedule

| Reminder | Interval | Duration |
|---|---:|---:|
| Eye Reset | 20 minutes | 20 seconds |
| Move Break | 45 minutes | 60 seconds |

All intervals and durations are configurable.

## Privacy

Awayra stores settings, runtime information, and logs locally under `%LocalAppData%\Awayra\`. It does not upload screenshots, browsing history, application usage, or personal information. The application makes no network calls of any kind.

When a break starts, Awayra captures the display your cursor is on and blurs it behind the break card, so the overlay reads as frosted glass rather than a black wall. That image exists only in memory for the length of the break. It is never written to disk, never leaves your computer, and is discarded when the overlay closes. If you would rather no capture happened at all, set **Overlay appearance** to *Solid*.

## Installation behavior

Awayra uses a per-user installation at `%LocalAppData%\Programs\Awayra` and does not require administrator access.

Program files are always replaced: the installer stops any running Awayra process and removes stale binaries and shortcuts before installing the new version.

**Your data is yours.** From version 1.2.0 the installer asks what to do with your existing settings, statistics and reminder schedule, and keeps them by default. Choosing *Delete my existing data* on the wizard page performs the old full reset. Silent installs preserve data unless `/CLEANDATA=yes` is passed:

```bash
Awayra-Setup-x64.exe /VERYSILENT /CLEANDATA=yes
```

Uninstall asks whether to remove your settings and statistics, and keeps them by default so a reinstall picks up where you left off. From version 1.3.0 a silent uninstall keeps them too, so removing Awayra through a package manager or management tool does not destroy your data. Add `/CLEANDATA=yes` to remove it deliberately:

```bash
unins000.exe /VERYSILENT /CLEANDATA=yes
```

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
| `src/Awayra.App` | WPF UI, tray integration, overlays, persistence, sound, diagnostics, and Windows interop |
| `tests/Awayra.Core.Tests` | Platform-neutral domain tests |
| `tests/Awayra.App.Tests` | WPF application and service tests |
| `tests/Awayra.UiTests` | Windows UI automation tests |

## Contributing and security

Read [CONTRIBUTING.md](CONTRIBUTING.md) before contributing. Security reports must follow [SECURITY.md](SECURITY.md). Release history is in [CHANGELOG.md](CHANGELOG.md), and use of the Awayra name and logo is covered by [TRADEMARKS.md](TRADEMARKS.md).

## License

Awayra is licensed under **GPL-3.0-only**. See [LICENSE](LICENSE).

Copyright © 2026 Farzin Alavi.

> Awayra is a wellness reminder, not a medical device. Persistent eye pain, severe headaches, double vision, numbness, or ongoing musculoskeletal pain should be assessed by a qualified professional.