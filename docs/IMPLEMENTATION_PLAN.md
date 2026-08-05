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

## Versioning

`Directory.Build.props` holds the released version and every project inherits it. Two files cannot
read it and must be bumped alongside: `src/Awayra.App/app.manifest` (`assemblyIdentity version`,
four-part) and the `MyAppVersion` fallback in `installer/Awayra.iss`. Both the build and release
workflows fail if the three disagree.

## Styling

There is no global theme dictionary. `App.xaml` contributes only the `BoolToVisibility` converter,
and each window merges its own scoped palette: `DashboardStyles.xaml`, `SettingsStyles.xaml`,
`OverlayStyles.xaml`, `AboutStyles.xaml`. A window that sets no `Foreground` therefore inherits
nothing, so palettes define their own text brushes.

## Localization

Localization keys live in `Awayra.Core`, and the only shipped resource set is English
(`src/Awayra.App/Resources/Strings.resx`). `LocalizationService` currently pins the process to `en`.
Adding a language means adding a satellite `.resx` and letting `LocalizationService.Apply` select it.

Keys exist only for text that is actually rendered through them: the dashboard, tray menu, overlay
and validation messages. The Settings window is still authored in English directly in XAML, so
adding a language means moving those labels to resources at the same time. Keys with no consumer are
removed rather than left in `StringKeys`, so `LocalizationTests` measures real coverage.

## Culture

`LocalizationService.Apply` pins the UI thread to `en`, but background threads keep the machine
culture. Anything persisted or compared — JSON times, statistics day keys, log timestamps, countdown
text — formats with `CultureInfo.InvariantCulture` so a machine on a different time separator or a
non-Gregorian calendar still reads back what it wrote.

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
