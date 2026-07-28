# Clean Rebuild Baseline

Captured: 2026-07-17 (UTC+4)

## Repository

`D:\curser\AWAYRA-WPF`

## Git status (short)

```
 M .cursor/rules/awayra.mdc
 M AGENTS.md
 M Awayra.sln
 M src/Awayra.App/App.xaml.cs
 M src/Awayra.App/Awayra.App.csproj
 M src/Awayra.App/Interop/NativeMethods.cs
 D src/Awayra.App/Resources/Strings.ar.resx
 D src/Awayra.App/Resources/Strings.fa.resx
 M src/Awayra.App/Resources/Strings.resx
 M src/Awayra.App/Resources/Theme.xaml
 M src/Awayra.App/Services/ApplicationHost.cs
 M src/Awayra.App/Services/LocalizationService.cs
 M src/Awayra.App/Services/TrayAndOverlay.cs
 M src/Awayra.App/ViewModels/MainViewModel.cs
 M src/Awayra.App/ViewModels/OverlayViewModel.cs
 M src/Awayra.App/ViewModels/SettingsViewModel.cs
 M src/Awayra.App/Views/BreakOverlayWindow.xaml
 M src/Awayra.App/Views/BreakOverlayWindow.xaml.cs
 M src/Awayra.App/Views/MainWindow.xaml
 M src/Awayra.App/Views/SettingsWindow.xaml
 M src/Awayra.Core/Localization/Localization.cs
 M src/Awayra.Core/Models/AppSettings.cs
 M src/Awayra.Core/Models/Enums.cs
 M src/Awayra.Core/Models/SchedulerState.cs
 M src/Awayra.Core/Persistence/JsonPersistence.cs
 M src/Awayra.Core/Services/BreakScheduler.cs
 M src/Awayra.Core/Services/SettingsValidator.cs
 M tests/Awayra.Core.Tests/BreakSchedulerTests.cs
 M tests/Awayra.Core.Tests/LocalizationTests.cs
 M tests/Awayra.Core.Tests/SettingsTests.cs
?? docs/CHANGE_GUARD.md
?? docs/REGRESSION_BASELINE.md
?? installer/
?? scripts/build-installer.ps1
?? scripts/verify-change.ps1
?? src/Awayra.Core/Coordination/
?? src/Awayra.Core/Services/OverlayGlassSettings.cs
?? tests/Awayra.App.Tests/
?? tests/Awayra.Core.Tests/ApplicationStartupPolicyTests.cs
?? tests/Awayra.Core.Tests/DashboardRestorePlannerTests.cs
?? tests/Awayra.Core.Tests/OverlayCoordinationTests.cs
?? tests/Awayra.Core.Tests/SnoozeSemanticsTests.cs
?? tests/Awayra.Core.Tests/TrayActionCatalogTests.cs
```

## Git diff stat

30 files changed, 1192 insertions(+), 472 deletions(-)

## Latest commit

```
ec78ecd Initial Awayra WPF baseline with tray app, scheduler, overlays, localization, tests, and publish scripts.
```

## .NET SDK

- Version: 10.0.302
- Commit: 35b593bebf
- RID: win-x64

## Installed SDKs

```
9.0.100
10.0.302
```

## Source backup

`D:\curser\AWAYRA-WPF-source-backup-20260717-140128.zip`

## Preservation rules

- No git reset, stash, clean, checkout, discard, commit, or push performed.
- All current source changes preserved.
- Old Tauri repository `D:\curser\AWAYRA\AWAYRA` not touched.
