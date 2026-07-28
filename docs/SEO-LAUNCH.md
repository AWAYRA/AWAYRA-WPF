# Awayra Search Discoverability Setup

The repository content, metadata values, security policy, and GitHub Pages files are prepared.

## One-command repository launch

A repository owner can apply the public launch settings with the official GitHub CLI:

```powershell
gh auth login --hostname github.com --web
powershell -ExecutionPolicy Bypass -File .\scripts\configure-public-repository.ps1
```

The script verifies admin access and then:

- changes the repository visibility to public
- sets the repository description and website
- applies the discoverability topics listed below
- enables vulnerability alerts, automated security fixes, and private vulnerability reporting when supported
- configures GitHub Pages from `main` and `/docs`
- protects `main` from force pushes and deletion
- requires the Windows `build` check before merging
- enables squash and rebase merging and deletes merged branches

Use `-WhatIf` to review the operations without changing repository settings:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\configure-public-repository.ps1 -WhatIf
```

## Repository About section

**Description**

```text
Free open-source Windows break reminder with a 20-20-20 eye timer, movement breaks, fullscreen overlays, and no telemetry.
```

**Website**

```text
https://aaa-it-uae.github.io/AWAYRA-WPF/
```

**Topics**

```text
windows
windows-11
wpf
dotnet
csharp
break-reminder
eye-strain
eye-care
20-20-20
screen-break
stretch-reminder
posture
productivity
wellness
open-source
privacy
desktop-app
system-tray
no-telemetry
work-break-timer
```

## GitHub Pages

The public launch script configures Pages to deploy directly from:

```text
branch: main
folder: /docs
```

Expected website URL:

```text
https://aaa-it-uae.github.io/AWAYRA-WPF/
```

## Distribution

Keep the official download link pointed to GitHub Releases:

```text
https://github.com/AAA-It-uae/AWAYRA-WPF/releases/latest
```

Awayra does not require Microsoft Store distribution.

## Search intent covered

- Windows break reminder
- 20-20-20 eye timer
- eye strain reminder for Windows
- Windows stretch reminder
- posture break reminder
- screen break timer
- work break reminder
- open-source break reminder
- privacy-first desktop wellness app
