# Changelog

All notable changes to Awayra are documented here.

The project follows semantic versioning where practical.

## [Unreleased]

No unreleased changes yet.

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