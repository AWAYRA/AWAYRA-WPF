# Awayra 1.0.2

This directory contains the standalone, self-contained Windows x64 installer
for Awayra 1.0.2.

- Installer: `Awayra-Setup-1.0.2-x64.exe`
- SHA-256: `1E22615D7186393E989F5B3C6CDBCD2F1AA883B9C33042E6A1CAB90FB5FE761A`
- Install scope: per-user
- Default directory: `%LocalAppData%\Programs\Awayra`
- Minimum operating system: Windows 10 x64

Verify the download in PowerShell:

```powershell
(Get-FileHash .\Awayra-Setup-1.0.2-x64.exe -Algorithm SHA256).Hash
```

The installer is not code-signed. Windows SmartScreen may therefore display an
Unknown Publisher warning.
