# Current Change Audit (vs ec78ecd)

## 1. Intended product behavior changes
- Break scheduler: pause/resume, idle handling, configuration pause, snooze semantics
- Overlay coordination and glass clarity settings
- Dashboard/tray/settings UI updates (Theme.xaml, MainWindow, SettingsWindow, overlays)
- Localization trimmed to English-only (removed ar/fa resx)
- Persistence recovery and settings validation hardening
- Simulated idle monitor for UI-test commands
- Build identity logging and single-instance coordination

## 2. Test-harness changes
- `tests/Awayra.UiTests/` (FlaUI session, diagnostics client, native/functional tests)
- `tests/Awayra.App.Tests/`
- Extended Core tests (scheduler, settings, startup policy, tray catalog, etc.)
- App UI-test pipes: `UiTestMode`, `UiTestDiagnosticsWriter`, command/diagnostics pipes

## 3. Documentation/scripts
- `docs/CHANGE_GUARD.md`, `REGRESSION_BASELINE.md`, audit baselines, cleanup reports
- `scripts/verify-change.ps1`, `build-installer.ps1`, `clean-awayra-machine.ps1`, launch helpers
- `AGENTS.md`, `.cursor/rules/awayra.mdc`
- `installer/` (not requested in this stabilization pass)

## 4. Suspicious or unrelated changes
- `scripts/publish.ps1` and installer scripts add Release/installer paths (no evidence of altered Debug defaults)
- Large Theme.xaml/visual churn mixed with behavior work (styling-only risk, not a production-default regression)

## Protected-behavior check (ec78ecd → working tree)
| Area | Finding |
|------|---------|
| Production defaults (`AppSettings.CreateDefault`) | Still Eye 20 min / Move 45 min — unchanged |
| Dashboard countdown behavior | Scheduler logic changed; UI-test mode had wrong harness intervals (5 min override), not production default change |
| Scheduler intervals | Core intervals configurable; production defaults preserved |
| Normal user settings path | Still `%LocalAppData%\Awayra` when not in UI-test mode |
| Startup/tray behavior | Extended (UI-test skips autostart); normal path intact |
| Release publishing | Scripts extended; no automatic Release build in Debug workflow |

**Suspicious production regression:** None requiring production edits for this stabilization task. UI-test failures traced to harness not seeding isolated settings or verifying 1-minute profile.
