# Awayra Search Discoverability Setup

The repository metadata, security policy, release links, and GitHub Pages files are maintained for `AWAYRA/AWAYRA-WPF`.

## Apply repository settings

A repository owner can review or apply the public repository settings with GitHub CLI:

```powershell
gh auth login --hostname github.com --web
powershell -ExecutionPolicy Bypass -File .\scripts\configure-public-repository.ps1 -WhatIf
powershell -ExecutionPolicy Bypass -File .\scripts\configure-public-repository.ps1
```

The script verifies admin access and then:

- updates the public repository description and website
- applies discoverability topics
- enables supported security features
- configures GitHub Pages from `main` and `/docs`
- protects `main` from force pushes and deletion
- requires the Windows `build-and-test` and `installer` checks before merging
- enables squash and rebase merging and deletes merged branches

## Repository About section

**Description**

```text
Free open-source Windows break reminder with a 20-20-20 eye timer, movement breaks, locally generated sounds, and no telemetry.
```

**Website**

```text
https://awayra.github.io/AWAYRA-WPF/
```

**Topics**

```text
windows windows-11 wpf dotnet csharp break-reminder eye-strain eye-care
20-20-20 screen-break stretch-reminder posture productivity wellness
open-source privacy desktop-app system-tray no-telemetry work-break-timer
```

## GitHub Pages

```text
branch: main
folder: /docs
URL: https://awayra.github.io/AWAYRA-WPF/
```

## Distribution

Official installer and checksum files are published through:

```text
https://github.com/AWAYRA/AWAYRA-WPF/releases/latest
```

Awayra does not require Microsoft Store distribution.