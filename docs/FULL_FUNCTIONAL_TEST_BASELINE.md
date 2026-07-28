# Full Functional Test Baseline

**Date:** 2026-07-17 (UTC+4)  
**SDK:** .NET 10.0.302

## Git status (short)

Working tree dirty with glass overlay, scheduler semantics, UI-test, and FlaUI test changes. No commits or pushes performed.

## Test counts (post-repair)

| Project | Passed | Failed | Skipped | Total |
|---------|--------|--------|---------|-------|
| Awayra.Core.Tests | 97 | 0 | 0 | 97 |
| Awayra.App.Tests | 15 | 0 | 0 | 15 |
| Awayra.UiTests | 17 | 0 | 1 | 18 |
| **Total** | **129** | **0** | **1** | **130** |

Skipped: `T18_RealWindowsIdle_FreezesAndResumesCountdowns` (system idle did not reach 65s in automation environment).

## Known defects at baseline

1. DWM-based overlay produced uniform solid dark background (no frosted glass).
2. Manual pause did not freeze dashboard countdowns.
3. Work-hours outside range did not freeze displayed countdowns.
4. Glass slider limited to BackgroundVisibility 10–30.
5. Minimum break duration was 5 seconds (spec requires 10).
6. Full A–Q FlaUI matrix incomplete.
7. Real 70s Windows idle E2E blocked by continuous background input in automation host.

## Debug executable

- **Path:** `D:\curser\AWAYRA-WPF\src\Awayra.App\bin\Debug\net10.0-windows\Awayra.exe`
- **SHA-256:** `CD700EF65DAC3F544DCCB07582A723C33FD9C8BBFBF1A98FD7D829E6D99D2C72`

## verify-change

`scripts/verify-change.ps1` — **PASSED** (build + 129 passed / 1 skipped + process path/hash smoke)
