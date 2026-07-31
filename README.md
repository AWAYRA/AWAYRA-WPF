<div align="center">

# Awayra — Free Windows Break Reminder

**An open-source 20-20-20 eye break timer, movement reminder, and screen break app for Windows 10 and 11.**

![Windows](https://img.shields.io/badge/Windows-10%20%7C%2011-0078D4?logo=windows11&logoColor=white)
![.NET](https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet&logoColor=white)
![License](https://img.shields.io/badge/License-GPL--3.0--only-blue)
![Privacy](https://img.shields.io/badge/Privacy-Local--only-2ea44f)

[Download Awayra for Windows](https://github.com/AAA-It-uae/AWAYRA-WPF/releases/latest/download/Awayra-Setup-x64.exe) · [Release notes](https://github.com/AAA-It-uae/AWAYRA-WPF/releases/latest) · [Report a bug](https://github.com/AAA-It-uae/AWAYRA-WPF/issues)

</div>

## Download

| What you need | System | Direct download |
|---|---|---|
| **Awayra installer — recommended** | Windows 10 or 11, x64 | **[Download `Awayra-Setup-x64.exe`](https://github.com/AAA-It-uae/AWAYRA-WPF/releases/latest/download/Awayra-Setup-x64.exe)** |
| SHA-256 checksum | Installer verification | [Download checksum](https://github.com/AAA-It-uae/AWAYRA-WPF/releases/latest/download/Awayra-Setup-x64.sha256.txt) |
| Release notes and older versions | All published releases | [Open GitHub Releases](https://github.com/AAA-It-uae/AWAYRA-WPF/releases) |

The installer is self-contained, so users do not need to install .NET separately. Official executable files are published only through **GitHub Releases**. Do not download installer files from source branches, forks, or third-party websites.

Unsigned releases may show a Windows SmartScreen **Unknown Publisher** warning until the project establishes signing reputation. Verify the SHA-256 checksum before installation.

> **Upgrade behavior in Awayra 1.0.5:** installing or reinstalling Awayra performs a clean reset. Previous Awayra settings, scheduler state, statistics, logs, startup registration, shortcuts, and stale program files are removed before the new version is installed. This is intentional while the application is under active stabilization.

## What is Awayra?

Awayra is a lightweight, native **Windows break reminder** for people who spend long hours working, studying, gaming, designing, or programming at a computer.

It combines two independent schedules:

- an **eye strain reminder** based on the 20-20-20 rule
- a **movement and posture break reminder** for standing, walking, stretching, and changing position

Awayra stays quietly in the Windows system tray and displays a focused fullscreen reminder when a break is due. It works offline, stores everything locally, and does not require an account, subscription, cloud service, or internet connection.

## Why use a screen break reminder?

Long, uninterrupted screen sessions can contribute to digital eye strain. Common symptoms include dry or irritated eyes, blurred vision, headaches, and difficulty refocusing after prolonged near work. Screen concentration can also reduce normal blinking.

Remaining in one position for too long can fatigue the muscles supporting the neck, shoulders, back, wrists, and hips. Awayra helps interrupt long periods of screen focus and static sitting before they become the default pattern of your workday.

Awayra is a wellness reminder, not a medical device. It does not replace exercise, good ergonomics, professional eye care, or medical treatment.

## Recommended break schedule

| Reminder | Frequency | Duration | Suggested action |
|---|---:|---:|---|
| **Eye Reset** | Every 20 minutes | 20 seconds | Look at something about 20 feet / 6 metres away and blink naturally. |
| **Move Break** | Every 30–60 minutes | At least 60 seconds | Stand up, walk briefly, change posture, and relax your shoulders. |

Awayra's default schedule is:

- **Eye Reset:** every 20 minutes for 20 seconds
- **Move Break:** every 45 minutes for 60 seconds

Both schedules are fully configurable.

## Features

- Independent eye break and movement break timers
- 20-20-20 eye reminder support
- Clear fullscreen break overlays
- Pause, resume, skip, snooze, and start-now controls
- Idle detection that avoids reminders while you are away
- Optional work-hour restrictions
- Windows startup and start-minimized options
- Daily completion, skip, and snooze statistics
- Dark and light themes with reduced-motion support
- Local settings and data storage
- No account, advertising, telemetry, server, or cloud sync
- Native C# and WPF application for Windows 10 and Windows 11

## Who is Awayra for?

Awayra is designed for:

- software developers and programmers
- office workers and remote teams
- designers, video editors, and content creators
- students and researchers
- gamers and streamers
- anyone searching for a free Windows stretch reminder, posture reminder, work break timer, or eye-care timer

## Installation

1. Download [`Awayra-Setup-x64.exe`](https://github.com/AAA-It-uae/AWAYRA-WPF/releases/latest/download/Awayra-Setup-x64.exe).
2. Run the installer.
3. Open Awayra once and leave it running in the Windows system tray.
4. Use the dashboard or tray menu to adjust schedules, start a break, pause reminders, or quit.

| Setting | Value |
|---|---|
| Supported systems | Windows 10 and Windows 11 x64 |
| Installer type | Per-user Windows installer |
| Default location | `%LocalAppData%\Programs\Awayra` |
| Runtime | Self-contained; .NET is included |
| Distribution | GitHub Releases only |

## Privacy-first by design

Awayra works entirely on your computer. It does not require an account and does not send usage data anywhere.

Local files are stored under `%LocalAppData%\Awayra\`:

| File | Purpose |
|---|---|
| `settings.json` | User preferences |
| `state.json` | Current reminder schedule |
| `stats.json` | Daily break statistics |
| `Logs\awayra.log` | Local diagnostic log |

These files are removed by the 1.0.5 clean-upgrade installer and by uninstall.

## Frequently asked questions

### Does Awayra support the 20-20-20 rule?

Yes. The default Eye Reset reminder runs every 20 minutes for 20 seconds. During the break, look at something approximately 20 feet or 6 metres away.

### Is Awayra free?

Yes. Awayra is free and open-source software released under the GPL-3.0-only license.

### Does Awayra work offline?

Yes. The application does not need an internet connection after download and installation.

### Does Awayra collect screen activity or personal data?

No. Awayra does not upload telemetry, browsing history, screenshots, application usage, or personal information. Idle detection is processed locally by Windows.

### Can I change the break intervals?

Yes. Eye breaks, movement breaks, durations, snooze timing, working hours, startup behavior, and other reminder settings are configurable.

### Will an upgrade keep my current settings?

Not in version 1.0.5. The installer intentionally removes previous Awayra settings and runtime data so testing starts from a clean, known state.

### Is Awayra available for macOS or Linux?

No. The current release is a native Windows application built with WPF. The supported platforms are Windows 10 and Windows 11 x64.

### Is Awayra published in the Microsoft Store?

No. Official installers are distributed directly through this repository's GitHub Releases page.

## Development

### Requirements

- Windows 10 or Windows 11 x64
- .NET 10 SDK
- PowerShell
- Inno Setup 7 only for installer builds

### Run the application

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\dev.ps1
```

### Build and verify

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\verify-change.ps1
```

The verification script builds the solution, runs test projects under `tests/`, and performs a Windows launch and single-instance check.

### Build the installer locally

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\build-installer.ps1
```

Local output: `artifacts\installer\Awayra-Setup-{VERSION}-x64.exe`

Generated executables, installers, certificates, and release folders must not be committed to the source tree. The `Publish Windows release` workflow builds and publishes distributable files automatically when a new application version reaches `main`.

## Release process

The application version is defined in `src/Awayra.App/Awayra.App.csproj`.

When a commit reaches `main`:

1. GitHub Actions reads the application version.
2. If a Release for that version already exists, the workflow exits without changing it.
3. Otherwise, the workflow builds a self-contained x64 installer.
4. It executes the clean-upgrade and uninstall E2E test.
5. It creates the matching version tag and GitHub Release.
6. It uploads versioned files and the permanent `Awayra-Setup-x64.exe` download alias.

A new public release therefore requires an intentional version bump before merging.

## Architecture

| Project | Responsibility |
|---|---|
| `Awayra.Core` | Scheduling, settings, validation, statistics, and domain logic |
| `Awayra.App` | WPF interface, tray integration, overlays, persistence, and Windows interop |

**Technology:** C#, .NET 10, WPF, MVVM with CommunityToolkit.Mvvm, and Windows NotifyIcon.

## Contributing and security

- Read [CONTRIBUTING.md](CONTRIBUTING.md) before submitting changes.
- Report security issues according to [SECURITY.md](SECURITY.md).
- See [CHANGELOG.md](CHANGELOG.md) for release history.
- The Awayra name and logo are governed by [TRADEMARKS.md](TRADEMARKS.md).

## License

Awayra is open-source software licensed under the **GNU General Public License v3.0 only** (`GPL-3.0-only`). See [LICENSE](LICENSE).

Modified distributions must follow the GPL requirements. The license does not grant permission to present an unofficial fork as the official Awayra application.

Copyright © 2026 Farzin Alavi.

## Health note

Persistent eye pain, double vision, severe headaches, numbness, or ongoing neck, back, or wrist pain should be assessed by a qualified healthcare professional.

## References

- [Digital Eye Strain — American Academy of Ophthalmology EyeWiki](https://eyewiki.aao.org/Computer_Vision_Syndrome_%28Digital_Eye_Strain%29)
- [Computer Workstation Micro-Breaks — OSHA](https://www.osha.gov/etools/computer-workstations/work-process)
- [Physical Activity and Sedentary Behaviour — World Health Organization](https://www.who.int/news-room/fact-sheets/detail/physical-activity)
