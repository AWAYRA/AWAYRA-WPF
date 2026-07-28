# Source Audit

Date: 2026-07-17

## Scope

Audited `src/`, `scripts/`, `tests/`, `installer/`, and `docs/` in `D:\curser\AWAYRA-WPF`.

## Executable path resolution

| Area | Finding | Status |
|------|---------|--------|
| Autostart registry | `RegistryAutostartService` uses caller-supplied `Environment.ProcessPath` via `ApplicationHost.ApplyAutostartSetting()` | OK |
| Application host | Uses `Environment.ProcessPath` with empty fallback guard | OK |
| App startup icon | Uses `AppContext.BaseDirectory` for packaged assets | OK |
| No hardcoded old Tauri paths | None found in source | OK |
| No wildcard `Awayra.exe` discovery in scripts | Removed; scripts use explicit deterministic paths | Fixed |

## Lifecycle and coordination

| Component | Finding | Status |
|-----------|---------|--------|
| `ShowDashboard` | Authoritative restore via `DashboardRestorePlanner` + `MonitorLocator` | OK |
| Single instance | `NamedPipeSingleInstance` mutex + pipe signal | OK |
| Tray service | One `TrayService` per process; disposed on quit | OK |
| Close to tray | `ApplicationStartupPolicy.ShouldHideDashboardToTrayOnClose` | OK |
| Start minimized | `ApplicationStartupPolicy.ShouldShowDashboardOnStartup` respects `StartMinimized` | OK |

## Persistence

| Store | Path | Status |
|-------|------|--------|
| Settings | `%LocalAppData%\Awayra\settings.json` | OK |
| Scheduler state | `%LocalAppData%\Awayra\state.json` | OK |
| Statistics | `%LocalAppData%\Awayra\stats.json` | OK |
| Logs | `%LocalAppData%\Awayra\Logs\awayra.log` | OK |
| UI-test isolation | `--ui-test` overrides data root to temp directory | Added |

## Feature areas verified in source

- Snooze: per-break `EyeNextDue` / `MoveNextDue` from button-press time in `BreakScheduler`
- Overlay glass: scrim-only opacity via `OverlayGlassSettings` + DWM interop in `DwmHelper`
- Background visibility: `BackgroundVisibility` 10–30 binding in settings and live overlay update
- English-only: Persian/Arabic resources removed; no language selector
- Idle freeze: countdown frozen while idle via `_idleFrozenEyeRemaining` / `_idleFrozenMoveRemaining`

## Script audit

| Script | Before | After |
|--------|--------|-------|
| `dev.ps1` | Launched Debug EXE without hash verification | Stops old processes, prints SHA-256, verifies running path |
| `publish.ps1` | Published Release EXE | Adds `BUILD-IDENTITY.txt`, deterministic output path |
| `verify-change.ps1` | Repo-scoped process check only | Uses shared launch verification helpers |
| `run-fresh-release.ps1` | Did not exist | Added explicit Release launch verification |

## Bugs fixed during audit

1. Stale process/path confusion: hardened launch scripts with SHA-256 and running-path verification.
2. Machine cleanup script used unsafe `Program Files` concatenation; fixed with `Join-Path`.
3. Build identity not logged at startup; added `BuildIdentity` logging.
4. UI automation required stable selectors; added `AutomationProperties.AutomationId` to dashboard, settings, and overlays.
5. UI tests needed isolated storage; added `--ui-test` mode and command pipe for tray coordination testing.

## No speculative changes made

Dashboard layout, settings layout (except AutomationId metadata), window dimensions, fonts, scheduler intervals, tray behavior, and installer configuration were not redesigned.
