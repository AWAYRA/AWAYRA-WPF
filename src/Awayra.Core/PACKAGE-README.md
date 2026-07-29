# AAAItUae.Awayra.Core

Reusable, platform-neutral domain logic from [Awayra](https://github.com/AAA-It-uae/AWAYRA-WPF), the open-source Windows break reminder.

This package contains:

- eye-break and movement-break scheduling
- pause, snooze, skip, work-hours, and idle-state rules
- reminder settings validation
- scheduler state and snapshot models
- break statistics logic

It does **not** contain the WPF user interface, Windows tray integration, Registry startup handling, installer, telemetry, or network services.

## Target framework

- .NET 10 (`net10.0`)

## Install from GitHub Packages

GitHub's NuGet registry requires authentication. Add the organization feed using a GitHub token with `read:packages`, then install the package:

```bash
dotnet nuget add source https://nuget.pkg.github.com/AAA-It-uae/index.json \
  --name github \
  --username YOUR_GITHUB_USERNAME \
  --password YOUR_GITHUB_TOKEN \
  --store-password-in-clear-text

dotnet add package AAAItUae.Awayra.Core --source github
```

Do not commit tokens or a credential-bearing `nuget.config` file.

## Minimal use

```csharp
using Awayra.Core.Models;
using Awayra.Core.Services;

var clock = new SystemClock();
var settings = AppSettings.CreateDefault();
var scheduler = new BreakScheduler(clock, settings);

scheduler.BreakStarted += (_, e) =>
{
    Console.WriteLine($"Break started: {e.BreakType}");
};

scheduler.Tick();
var snapshot = scheduler.GetSnapshot();
Console.WriteLine(snapshot.Status);
```

## License

GNU General Public License v3.0 only (`GPL-3.0-only`). Applications distributing modified or combined GPL-covered code must comply with the license terms.
