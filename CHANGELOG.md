# Changelog

All notable changes to Awayra are documented here.

The project follows semantic versioning where practical.

## [Unreleased]

No unreleased changes yet.

## [1.2.0] - 2026-08-05

### Added

- Guided eye exercise on the Eye Reset overlay: an animated eye with expanding focus rings and ten complete blinks counted on screen
- Guided movement routine on the Move Break overlay: a figure stops typing, stands, walks away with the camera following, stretches overhead, bends side to side and rolls their shoulders, then turns to face the viewer for three squats and three jumps before walking back and sitting down
- Two soft melodic sounds, Morning dew and Still water, that swell in from silence and recede rather than starting at full volume like the alert tones
- Detailed workstation in the Move Break scene: a monitor with an on-screen layout, keyboard, mouse, mug, and an office chair with a gas cylinder, star base and casters
- Both animations honour the existing Reduced motion setting, which replaces them with static illustrations and written guidance
- Setting to turn the guided break exercise off entirely, on by default, in Settings under Break sound and exercise
- Installer wizard page asking whether to keep or delete existing settings, statistics and reminder schedule
- `/CLEANDATA=yes` installer switch for unattended installs that intentionally want a full reset
- Uninstall now asks before removing personal data
- Release workflow builds and runs both test suites before publishing

### Changed

- **The installer no longer deletes your data on upgrade.** Settings, statistics, scheduler state and logs are preserved by default; only program files and shortcuts are always replaced
- Silent installs preserve user data unless `/CLEANDATA=yes` is passed
- Dashboard is shorter now that the diagnostics panel is gone
- Dependabot also tracks GitHub Actions versions

### Fixed

- The Eye Reset blink is now driven by two lids that sweep across a stationary eyeball, closing quickly and opening slowly the way a real blink does, instead of the whole eye squashing
- Installer check boxes and radio buttons no longer have their glyphs clipped at 125% or 150% display scaling. Every option now lives on one page built from native controls, including the launch choice that used to sit on the Finished page
- Six sound choices now fit the Settings window without a scrollbar at its minimum size
- Snoozing one reminder no longer postpones the other. A snoozed Eye Reset could delay an unrelated Move Break by up to the full snooze duration; each reminder now keeps its own schedule, with only a 60-second handoff grace so two overlays never appear back to back

### Removed

- Display diagnostic recorder, the dashboard screen-blink button, and the `%LocalAppData%\Awayra\Diagnostics` timeline. A full native-API audit confirmed Awayra cannot cause a physical monitor blink: it contains no display-mode, topology, monitor-power, HDR or DXGI call of any kind. The recorder had served its purpose
- Dead `DwmHelper` window-glass helper, which had no callers and passed an undocumented DWM attribute
- Dead `AppTheme` setting, which was persisted but never read by any code
- Unreachable `SchedulerStatus.PausedIdle` value and its unused resource string

## [1.1.3] - 2026-08-03

### Added

- Continuous local display timeline with UTC, local, and monotonic timestamps
- Two-second desktop state snapshots covering monitor topology, resolution, refresh rate, DWM state, power state, foreground process, and Awayra process health
- Low-level capture for Windows display, power, device, setting, DPI, and DWM composition messages
- Direct capture of session, power-mode, display-setting, and user-preference system events
- Dashboard action to mark a visible screen blink and create a complete diagnostic ZIP
- Diagnostic bundle collection for Awayra logs, Windows System and Application events, DxgKrnl and Kernel-PnP events, connected monitor and display devices, active power plan, display power settings, and DxDiag

### Changed

- Application, assembly, package, installer, and public release metadata updated to `1.1.3`
- Dashboard includes the current diagnostic recorder status and automatically opens the generated ZIP in Explorer

### Notes

- Version 1.1.3 is an evidence-gathering build. It does not claim that the remaining physical monitor blink is fixed.
- Diagnostic information stays local until the user explicitly sends the generated ZIP.

## [1.1.2] - 2026-08-03

### Changed

- Fullscreen break overlays are now created and positioned while fully transparent and non-activating
- Overlay activation is deferred until WPF reports that the first complete content frame has rendered
- Monitor recovery is held until the initial overlay frame has been revealed
- Added structured log entries for invisible overlay preparation, first-frame reveal, and later display recovery

### Fixed

- Prevented the one-frame black fullscreen surface that could look like the monitor briefly powered off when a break overlay opened
- Removed the second visible `SetWindowPos` call from the overlay startup path
- Prevented display-recovery positioning from racing the initial WPF render

## [1.1.1] - 2026-08-02

### Added

- Original locally generated Calm piano loop with no downloaded or third-party audio asset
- Automated validation for all four sound themes and the piano waveform duration
- Installer tests covering clean installation over legacy files, reinstall, and complete uninstall cleanup
- TRX evidence validation for core and application test suites

### Changed

- Rebalanced the Settings window into two organized columns with sound controls at the top left
- Moved Windows build, installer, and NuGet validation to the self-hosted Windows runner
- Added every test project to `Awayra.sln` so `--no-build` tests cannot use stale binaries
- Unified application, assembly, package, and installer versions at `1.1.1`
- Migrated repository, installer, and package metadata from `AAA-It-uae` to `AWAYRA`
- Renamed the reusable package to `Awayra.Core` and moved its GitHub Packages feed to the `AWAYRA` organization

### Fixed

- Prevented fullscreen overlay recomposition when monitor bounds have not changed
- Stabilized monitor bounds before performing one physical-pixel reposition after wake, unlock, resume, or topology changes
- Prevented repeated display recovery from causing visible monitor flashes
- Replaced the previous false-green Windows test path with a complete solution build and verified test-result gate
- Ensured upgrades remove stale program files, settings, runtime state, logs, shortcuts, and startup registration

## [1.1.0] - 2026-08-02

### Added

- Independent Reminder and Sound switches under Eye Reset and Move Break timers
- Separate persistent sound enablement for eye and movement breaks
- Three built-in offline sound choices: Soft bell, Gentle chime, and Calm drop
- Configurable sound volume from 0 to 100
- Configurable sound repeat interval from 1 to 60 seconds
- Sound preview in Settings
- Per-break mute and unmute control inside the fullscreen overlay
- Automated tests for legacy settings migration, sound validation, generated WAV integrity, mute behavior, and lifecycle handling

### Changed

- Break sounds are generated locally without external packages, downloaded assets, telemetry, or network access
- Existing settings files load the new sound options with safe defaults and no reset
- Dashboard height and timer cards were adjusted to fit the new compact controls without changing the existing navigation flow

### Fixed

- Break sound now stops on completion, skip, snooze, Windows lock, suspend, shutdown, and application exit
- Monitor wake, unlock, and display changes no longer create duplicate fullscreen overlays or visible flicker
- Fullscreen overlay sizing now respects per-monitor DPI

## [1.0.3] - 2026-07-29

### Added

- GPL-3.0-only licensing notice
- Security policy and contribution guide
- Trademark policy
- Structured issue and pull request templates
- Windows build validation workflow
- Automatic GitHub Release publishing from the application version on `main`
- Permanent latest-release filenames for the Windows installer and SHA-256 checksum
- Direct installer download table on the repository home page

### Changed

- Official repository and issue links now point to `AWAYRA/AWAYRA-WPF`
- Generated installers and local build metadata are excluded from the source tree
- Public source history starts from a clean open-source baseline
- Windows CI now runs core and application automated tests after the Release build
- The GitHub Pages landing page links directly to the official installer and checksum
- Application and installer version metadata now use `1.0.3` to avoid reusing the existing `v1.0.2` tag

## [1.0.2] - 2026-07-28

### Changed

- Updated application and installer version metadata
- Restored release build safeguards
- Improved Windows application icon assets

## [1.0.0] - 2026-07-28

### Added

- Independent Eye Reset and Move Break reminders
- Configurable intervals and break durations
- Fullscreen break overlays
- Pause, resume, skip, snooze, and manual break controls
- Idle detection and optional work-hour restrictions
- Windows startup and start-minimized settings
- Daily break statistics
- Dark and light themes
- Local JSON persistence with no account, cloud, telemetry, or server dependency
- Self-contained Windows x64 publishing and installer scripts