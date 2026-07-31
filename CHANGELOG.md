# Changelog

All notable changes to Awayra are documented here.

The project follows semantic versioning where practical.

## [Unreleased]

### Fixed

- Active break overlays now recover after monitor disconnect/reconnect, display-topology changes, sleep/resume, and session unlock
- Fullscreen overlay placement now uses physical monitor bounds correctly across mixed-DPI displays
- Closed or invalid overlay windows are recreated while preserving the active break countdown and session state

## [1.0.4] - 2026-07-30

### Added

- One-time setup guidance after installation

### Fixed

- Reminder timers now restart from the configured intervals after a new Windows boot
- UI schedule-transition tests now wait for the updated diagnostics snapshot

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

- Official repository and issue links now point to `AAA-It-uae/AWAYRA-WPF`
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
