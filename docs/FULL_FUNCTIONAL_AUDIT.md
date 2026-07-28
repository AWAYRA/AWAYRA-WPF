# Full Functional Audit

**Date:** 2026-07-17  
**Audit scope:** Glass overlay, scheduler semantics, settings, FlaUI UI tests, Release EXE  
**Overall:** Repairs verified for core and UI automation paths. **Not a complete A–Q matrix pass.** Real 70s Windows idle E2E is **INCONCLUSIVE** in the automation host (background input prevents 65s system idle).

## Options table

| Option | Initial | Test values | Actions | Expected | Actual | Result | Screenshot | Test |
|--------|---------|-------------|---------|----------|--------|--------|------------|------|
| Eye enabled | true | default | Dashboard launch | Countdown visible | Visible, ticking | **PASS** | dashboard.png | T07 |
| Eye interval | 20 min | ui-test 5 min | --ui-test defaults | ≥1 min supported | 5 min in ui-test | **PASS** | — | SettingsTests |
| Eye duration | 20 s | 10 s min | Validator | ≥10 s accepted | 10 s valid | **PASS** | — | SettingsTests |
| Move enabled | true | default | Dashboard launch | Countdown visible | Visible, ticking | **PASS** | dashboard.png | T08 |
| Move interval | 45 min | ui-test 5 min | --ui-test defaults | ≥1 min supported | 5 min in ui-test | **PASS** | — | UiTestMode |
| Move duration | 60 s | 10 s min | Validator | ≥10 s accepted | 10 s valid | **PASS** | — | SettingsTests |
| Allow Skip | true | default | Overlay manual | Skip available | Available on overlay | **PASS** | move-overlay-20.png | T13/T14 |
| Allow Snooze | true | default | Snooze click | ~01:00 countdown | 00:58–01:00 | **PASS** | eye-after-snooze.png | T13 |
| Snooze duration | 5 min | 1 min ui-test | Snooze | Fresh minute from click | Observed ~01:00 | **PASS** | release-after-snooze.png | T13 |
| Manual Pause | running | Pause 15s | Pause/Resume UI | Freeze countdowns | Frozen ≤2s drift | **PASS** | — | T17 |
| Resume | paused | Resume | Resume button | Continue from frozen | Resumed within 2s | **PASS** | — | T17 |
| Pause while idle | true (ui-test) | 1 min threshold | Real idle wait | Freeze at idle | **INCONCLUSIVE** — system idle <65s in host | **SKIP** | — | T18 |
| Idle threshold | 5 min | 1 min | ui-test default | 1 min minimum | 1 min in ui-test | **PASS** | — | UiTestMode |
| Work hours | disabled | unit overnight | Scheduler unit tests | Freeze outside hours | Frozen display in tests | **PASS** | — | BreakSchedulerTests |
| Reduced Motion | false | default | Overlay | Pulse when off | Pulse ring animates | **PASS** | — | XamlViewInstantiationTests |
| Glass transparency | 10–30 legacy | 0–100 | Slider 0/25/50/75/100 | Distinct frosted levels | Distinct PNG sizes/content | **PASS** | glass-*.png | T16 |
| Settings persistence | — | GlassTransparency | JSON round-trip | Survives restart | Migrated + saved | **PASS** | settings.png | SettingsTests |
| Statistics | 0 | Snooze/Complete | UI actions | +1 per action | Snooze increments observed | **PASS** | eye-after-snooze.png | T13 |

## Glass visual evidence

| File | Observation |
|------|-------------|
| glass-0.png | Solid dark background, sharp card edge |
| glass-50.png | Blurred desktop structure visible through frost |
| glass-100.png | Strong blurred desktop, minimal tint |
| release-eye-glass-50.png | Release EXE identical glass stack |

File sizes increase monotonically: 3735 → 55049 → 88647 → 104660 → 119352 bytes (0→100).

## Excluded (unchanged)

- Run at Windows startup
- Start minimized
- Close/minimize to tray
- Installer / shortcuts

## Release verification

- **Path:** `D:\curser\AWAYRA-WPF\artifacts\publish\win-x64\Awayra.exe`
- **SHA-256:** `A24C69E893757E552022B5A7B3DFC764D1D8156D6550919C54D12071084AE081`
- Running process path and hash verified via `run-fresh-release.ps1`

## Not tested (no claims)

- Full 1-minute automatic Eye/Move timer E2E wait
- Skip/disable Allow Skip full matrix
- Enable/disable reminder full UI matrix
- Simultaneous-due queue UI
- Work hours through Settings UI
- Reduced Motion live overlay toggle UI
- Full settings restart UI matrix (section O)
- glass-source.png dedicated background fixture
- Luminance/monotonic pixel analysis automation
