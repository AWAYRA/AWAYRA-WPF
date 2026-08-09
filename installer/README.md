# Awayra Windows Installer

Production-grade per-user installer for the self-contained Awayra WPF application.

## Requirements

- Windows 10 or Windows 11 x64
- .NET 10 SDK on the build machine
- [Inno Setup 7](https://jrsoftware.org/isinfo.php) stable release (`ISCC.exe`)

Recipients do **not** need .NET, Visual C++ redistributables, Inno Setup, or development tools.

## Build

From the repository root:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\build-installer.ps1
```

The script:

1. Publishes a fresh self-contained `win-x64` single-file `Awayra.exe`
2. Validates version metadata and the application icon
3. Compiles `installer\Awayra.iss`
4. Writes artifacts under `artifacts\installer\`

Output:

- `Awayra-Setup-{VERSION}-x64.exe`
- `Awayra-Setup-{VERSION}-x64.sha256.txt`
- `BUILD-INFO.txt`

Generated installer files belong in GitHub Releases and must not be committed to the source repository.

## Optional code signing

Set these environment variables before building:

| Variable | Purpose |
|---|---|
| `AWAYRA_SIGN_CERT_PATH` | Path to an Authenticode certificate |
| `AWAYRA_SIGN_CERT_PASSWORD` | Certificate password |
| `AWAYRA_TIMESTAMP_URL` | RFC 3161 timestamp server URL |

When unset, the build completes unsigned and reports:

`UNSIGNED - Windows SmartScreen may show an Unknown Publisher warning.`

Never commit certificate files or passwords. Certificate formats are excluded by `.gitignore`.

## Installation model

| Setting | Value |
|---|---|
| Scope | Per-user (`PrivilegesRequired=lowest`) |
| Default directory | `%LocalAppData%\Programs\Awayra` |
| AppId | `{C348E9A2-7E31-4E8D-A638-94A635B813C1}` |
| Architecture | x64 |
| Minimum OS | Windows 10 x64 |

User settings and statistics under `%LocalAppData%\Awayra` are preserved across installation, upgrade, and uninstall.

## License page

The installer displays the repository's `LICENSE` file. Awayra is distributed under `GPL-3.0-only`.

## Links

- Repository: https://github.com/AWAYRA/AWAYRA-WPF
- Issues: https://github.com/AWAYRA/AWAYRA-WPF/issues
- Releases: https://github.com/AWAYRA/AWAYRA-WPF/releases

## Single-instance mutex

Awayra uses a per-user mutex:

`Local\Awayra.SingleInstance.{userSid}`

The installer relies on Inno Setup `CloseApplications=yes` to close a running Awayra instance safely during installation or upgrade. It does not force-terminate unrelated processes.
