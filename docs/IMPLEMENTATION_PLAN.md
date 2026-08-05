# Awayra Implementation Notes

This document describes how the solution is laid out and how to work on it. Release history lives
in [CHANGELOG.md](../CHANGELOG.md).

## Projects

| Project | Target | Responsibility |
|---|---|---|
| `src/Awayra.Core` | `net10.0` | Scheduling, settings validation, work hours, statistics, localization keys. Platform-neutral and published as the `Awayra.Core` NuGet package. |
| `src/Awayra.App` | `net10.0-windows` | WPF dashboard, settings and overlay windows, break animations, tray integration, persistence, sound synthesis, Windows interop. |
| `tests/Awayra.Core.Tests` | `net10.0` | Domain tests. No Windows dependency. |
| `tests/Awayra.App.Tests` | `net10.0-windows` | Application, view model and XAML instantiation tests. |
| `tests/Awayra.UiTests` | `net10.0-windows` | UI Automation tests driven against a published build. Not run in CI; see below. |

## Localization

Localization keys live in `Awayra.Core`, and the only shipped resource set is English
(`src/Awayra.App/Resources/Strings.resx`). `LocalizationService` currently pins the process to `en`.
Adding a language means adding a satellite `.resx` and letting `LocalizationService.Apply` select it.

## Break animations

`EyeExerciseView` and `MoveExerciseView` are self-contained user controls under
`src/Awayra.App/Views`. Each owns its vector art and its storyboards, exposes `StartAnimation`,
`StopAnimation` and `ApplyReducedMotion`, and merges `OverlayStyles.xaml` so it can be instantiated
and tested standalone. `BreakOverlayWindow` shows exactly one of them per break, and only starts
motion once the overlay is loaded and only when Reduced motion is off.

## Test coverage

Core and application suites run on every push and pull request, and again inside the release
workflow before any installer is published.

`tests/Awayra.UiTests` drives the real application through UI Automation and a named pipe
(`UiTestCommandPipe`, `UiTestDiagnosticsPipe`). It requires an interactive desktop session, so it is
not part of CI and must be run manually on a developer machine.

## Commands

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\dev.ps1              # run locally
powershell -ExecutionPolicy Bypass -File .\scripts\verify-change.ps1    # build and test
powershell -ExecutionPolicy Bypass -File .\scripts\build-installer.ps1  # publish and package
```

Output: `artifacts/publish/win-x64/Awayra.exe` and `artifacts/installer/`.
