# Contributing to Awayra

Thanks for considering a contribution. Keep changes focused, reviewable, and consistent with Awayra's purpose: a small, private, reliable Windows break reminder.

## Before starting

- Search existing issues before opening a duplicate.
- Open an issue before large feature work or architectural changes.
- Keep bug fixes separate from visual redesigns and unrelated cleanup.
- Never include credentials, certificates, personal data, generated installers, or local build output.

## Development requirements

- Windows 10 or Windows 11 x64
- .NET 10 SDK
- PowerShell
- Inno Setup 7 only when building an installer

## Local workflow

Run the application:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\dev.ps1
```

Verify a change:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\verify-change.ps1
```

Build a release executable:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\publish.ps1
```

Build the Windows installer:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\build-installer.ps1
```

## Pull requests

A pull request should:

- Explain the problem and the chosen solution
- Keep the diff limited to the requested scope
- Preserve local-only operation and avoid telemetry or mandatory network access
- Update documentation when behavior or settings change
- Build successfully on Windows
- Avoid committing `bin`, `obj`, `artifacts`, `release`, certificates, or executables

Screenshots are useful for visible UI changes. Reproduction steps are required for bug fixes.

## Code style

- Follow `.editorconfig`
- Keep nullable reference types enabled
- Keep business logic in `Awayra.Core`
- Keep WPF and Windows-specific integration in `Awayra.App`
- Prefer small, explicit changes over broad refactors
- Do not suppress warnings or remove validation to make a build pass

## Licensing

By submitting a contribution, you agree that your contribution may be distributed under the repository's GNU General Public License v3.0 only (`GPL-3.0-only`).

The Awayra name, icon, and visual identity are governed separately by `TRADEMARKS.md`.
