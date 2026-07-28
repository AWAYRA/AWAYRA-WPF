# Awayra Windows Installer

Production-grade per-user installer for the self-contained Awayra WPF application.

## Requirements

- Windows 10/11 x64
- .NET SDK 10 (build machine only)
- [Inno Setup 7](https://jrsoftware.org/isinfo.php) stable release (`ISCC.exe`)

Recipients do **not** need .NET, Visual C++ redistributables, Inno Setup, or any development tools.

## Build

From the repository root:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\build-installer.ps1
```

This script:

1. Publishes a fresh self-contained `win-x64` single-file `Awayra.exe`
2. Validates version metadata and the application icon
3. Compiles `installer\Awayra.iss`
4. Writes artifacts under `artifacts\installer\`

Output:

- `Awayra-Setup-{VERSION}-x64.exe`
- `Awayra-Setup-{VERSION}-x64.sha256.txt`
- `BUILD-INFO.txt`

## Optional code signing

Set these environment variables before building:

| Variable | Purpose |
|---|---|
| `AWAYRA_SIGN_CERT_PATH` | Path to Authenticode certificate (PFX/PEM) |
| `AWAYRA_SIGN_CERT_PASSWORD` | Certificate password |
| `AWAYRA_TIMESTAMP_URL` | RFC 3161 timestamp server URL |

When unset, the build completes unsigned and reports:

`UNSIGNED - Windows SmartScreen may show an Unknown Publisher warning.`

## Installation model

| Setting | Value |
|---|---|
| Scope | Per-user (`PrivilegesRequired=lowest`) |
| Default directory | `%LocalAppData%\Programs\Awayra` |
| AppId | `{C348E9A2-7E31-4E8D-A638-94A635B813C1}` |
| Architecture | x64 |
| Minimum OS | Windows 10 x64 |

User settings and statistics under `%LocalAppData%\Awayra` are preserved across install, upgrade, and uninstall.

## Automated installer test

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\test-installer.ps1
```

Uses an isolated temporary install directory and the UI-test data profile. Does not modify production Awayra settings.

## License page

No root `LICENSE` file is present in the repository. The installer is built without a License page.

## Links

- Repository: https://github.com/mtalavi/Awayra
- Support: https://github.com/mtalavi/Awayra/issues

## Single-instance mutex

Awayra uses a per-user mutex:

`Local\Awayra.SingleInstance.{userSid}`

The installer relies on Inno Setup `CloseApplications=yes` (Restart Manager) to close a running Awayra instance safely during install/upgrade. It does not force-terminate the process.
