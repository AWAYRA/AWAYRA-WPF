<div align="center">

# Awayra

**A calm, privacy-first break reminder for healthier computer use on Windows.**

![Windows](https://img.shields.io/badge/Windows-10%20%7C%2011-0078D4?logo=windows11&logoColor=white)
![.NET](https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet&logoColor=white)
![Privacy](https://img.shields.io/badge/Privacy-Local--only-2ea44f)

[Download for Windows](https://github.com/AAA-It-uae/AWAYRA-WPF/releases/latest) · [Report an issue](https://github.com/AAA-It-uae/AWAYRA-WPF/issues)

</div>

## What is Awayra?

Awayra is a lightweight native Windows application that reminds you to briefly rest your eyes and move your body during long computer sessions.

It stays quietly in the system tray, tracks two independent break schedules, and displays a focused fullscreen reminder when a break is due.

## Why regular breaks matter

Long, uninterrupted screen sessions can contribute to **digital eye strain**. Common symptoms include dry or irritated eyes, blurred vision, headaches, and difficulty refocusing after prolonged near work. Screen concentration can also reduce normal blinking.

Remaining in one position for too long can fatigue the muscles supporting the neck, shoulders, back, wrists, and hips. A highly sedentary routine is also associated with broader long-term cardiovascular and metabolic health risks.

Awayra does not replace exercise, good ergonomics, or medical care. It solves one practical problem: remembering to interrupt long periods of screen focus and static sitting.

## A practical break routine

| Break | Frequency | Duration | What to do |
|---|---:|---:|---|
| **Eye Reset** | Every 20 minutes | 20 seconds | Look at something about 20 feet / 6 metres away and blink naturally. |
| **Move Break** | Every 30–60 minutes | At least 60 seconds | Stand up, walk briefly, change posture, and relax your shoulders. |

Awayra's default schedule is:

- **Eye Reset:** every 20 minutes for 20 seconds
- **Move Break:** every 45 minutes for 60 seconds

Both schedules are fully configurable.

## How Awayra helps

- Independent Eye Reset and Move Break timers
- Clear fullscreen break overlays
- Pause, resume, skip, snooze, and start-now controls
- Idle detection that avoids unnecessary reminders while you are away
- Optional work-hour restrictions
- Windows startup and start-minimized options
- Daily completion, skip, and snooze statistics
- Dark and light themes with reduced-motion support
- Local settings and data storage
- No account, server, cloud sync, telemetry, or internet dependency

## Download and install

Download the latest compiled installer from [GitHub Releases](https://github.com/AAA-It-uae/AWAYRA-WPF/releases/latest).

- **Supported systems:** Windows 10 and Windows 11 x64
- **Installer type:** Per-user Windows installer
- **Default location:** `%LocalAppData%\Programs\Awayra`
- **Runtime:** Self-contained; .NET does not need to be installed separately

After installation, open Awayra once and leave it running in the system tray. Use the dashboard or tray menu to adjust the schedule, start a break immediately, pause reminders, or quit the application.

## Privacy

Awayra works entirely on your computer. It does not require an account and does not send usage data anywhere.

Local files are stored under `%LocalAppData%\Awayra\`:

| File | Purpose |
|---|---|
| `settings.json` | User preferences |
| `state.json` | Current reminder schedule |
| `stats.json` | Daily break statistics |
| `Logs\awayra.log` | Local diagnostic log |

## Development

### Requirements

- Windows 10 or 11 x64
- .NET 10 SDK
- PowerShell

### Run the application

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\dev.ps1
```

### Run tests

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\test.ps1
```

### Verify a change

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\verify-change.ps1
```

### Build the installer

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\build-installer.ps1
```

## Architecture

| Project | Responsibility |
|---|---|
| `Awayra.Core` | Scheduling, settings, validation, statistics, and domain logic |
| `Awayra.App` | WPF interface, tray integration, overlays, persistence, and Windows interop |
| `Awayra.Core.Tests` | Core business-logic tests |
| `Awayra.App.Tests` | Application-level tests |
| `Awayra.UiTests` | Windows UI and interaction tests |

**Technology:** C#, .NET 10, WPF, MVVM with CommunityToolkit.Mvvm, and Windows NotifyIcon.

## Health note

Awayra is a wellness reminder, not a medical device. Persistent eye pain, double vision, severe headaches, numbness, or ongoing neck, back, or wrist pain should be assessed by a qualified healthcare professional.

## References

- [Digital Eye Strain — American Academy of Ophthalmology EyeWiki](https://eyewiki.aao.org/Computer_Vision_Syndrome_%28Digital_Eye_Strain%29)
- [Computer Workstation Micro-Breaks — OSHA](https://www.osha.gov/etools/computer-workstations/work-process)
- [Physical Activity and Sedentary Behaviour — World Health Organization](https://www.who.int/news-room/fact-sheets/detail/physical-activity)
