# UI Audit

Date: 2026-07-17

## Tested executable

- Path: `D:\curser\AWAYRA-WPF\src\Awayra.App\bin\Debug\net10.0-windows\Awayra.exe`
- SHA-256: `CD700EF65DAC3F544DCCB07582A723C33FD9C8BBFBF1A98FD7D829E6D99D2C72`
- UI-test mode: `--ui-test` (isolated temp data directory)

## Native FlaUI test framework

- FlaUI.Core 5.0.0
- FlaUI.UIA3 5.0.0
- MSTest 3.9.3
- Selection via `AutomationProperties.AutomationId`

## Test results (16/16 passed)

| Test | Requirement coverage | Result |
|------|---------------------|--------|
| T01_CleanFirstLaunch_ShowsDashboard | 1, 7, 57 | PASS |
| T02_StartMinimized_DefaultsFalse | 2 | PASS |
| T03_ProcessPathMatchesTestedExecutable | 3, 15 | PASS |
| T04_ProcessHashMatchesTestedBuild | 4 | PASS |
| T05_LogRecordsBuildIdentity | 5 | PASS |
| T06_SingleOldProcessGuard | 6 | PASS |
| T07_EyeCountdownVisibleAndChanges | 8 | PASS |
| T08_MoveCountdownVisibleAndChanges | 9 | PASS |
| T09_SettingsButtonOpensSettings | 10, 58 | PASS |
| T10_SingleInstanceSecondLaunchRestoresDashboard | 13, 14 | PASS |
| T11_TrayOpenCoordination | 16 | PASS |
| T12_TraySettingsCoordination | 17 | PASS |
| T13_EyeOverlayOpensAndSnoozes | 20–27, 32–36 | PASS |
| T14_MoveOverlayOpensAndSnoozes | 28–31 | PASS |
| T15_SettingsBackgroundVisibilityAndNoLanguage | 52–54 | PASS |
| T16_BackgroundVisibilityScreenshotsDiffer | 41–51 | PASS |

## Screenshots

| File | Path |
|------|------|
| dashboard.png | `artifacts/ui-audit/dashboard.png` |
| settings.png | `artifacts/ui-audit/settings.png` |
| eye-overlay-10.png | `artifacts/ui-audit/eye-overlay-10.png` |
| eye-overlay-20.png | `artifacts/ui-audit/eye-overlay-20.png` |
| eye-overlay-30.png | `artifacts/ui-audit/eye-overlay-30.png` |
| move-overlay-20.png | `artifacts/ui-audit/move-overlay-20.png` |
| eye-after-snooze.png | `artifacts/ui-audit/eye-after-snooze.png` |
| move-after-snooze.png | `artifacts/ui-audit/move-after-snooze.png` |

## Visual findings

- Dashboard, Settings, Eye overlay, and Move overlay load without `XamlParseException`.
- Background visibility screenshots at 10%, 20%, and 30% are not identical.
- Center-card text and controls remain readable in all captured overlay screenshots.
- No Language selector present in Settings.

## Tray coordination

Tray notification-area automation is unreliable in headless contexts. Tray Open, Settings, and command routing were validated through the production `UiTestCommandPipe` handlers (`TRAY_OPEN`, `TRAY_SETTINGS`) wired to the same `ShowDashboard` and `ShowSettings` methods used by the tray service.

## Log health

No unhandled exceptions or missing-resource markers were observed in UI-test logs during the FlaUI suite run.
