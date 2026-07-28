# Awayra Agent Instructions

## Architecture

- Windows-only native WPF application
- .NET 10 LTS, C#, XAML, MVVM with CommunityToolkit.Mvvm
- No Tauri, Rust, Node, Electron, MAUI, WinUI, web frontend, or JavaScript

## Development commands

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\dev.ps1
powershell -ExecutionPolicy Bypass -File .\scripts\test.ps1
powershell -ExecutionPolicy Bypass -File .\scripts\verify-change.ps1
powershell -ExecutionPolicy Bypass -File .\scripts\publish.ps1
powershell -ExecutionPolicy Bypass -File .\scripts\smoke.ps1
```

## Change guard workflow (required for every task)

1. Inspect the requested scope
2. List the exact files allowed to change
3. Record protected behaviors that must remain unchanged (see `docs/CHANGE_GUARD.md`)
4. Run focused baseline tests before editing
5. Make the smallest possible patch
6. Inspect `git diff` after editing
7. Run focused tests
8. Run `scripts/verify-change.ps1` before declaring success
9. Never touch unrelated files
10. Never perform broad cleanup during a narrow fix
11. Never change UI while fixing backend behavior
12. Never change behavior while fixing visual styling
13. Never build Release or Installer unless explicitly requested
14. Never commit or push unless explicitly requested
15. A task is **BLOCKED** if regression verification fails

## Rules

- Use Debug builds for iteration; publish Release only after tests pass
- Do not create installers unless explicitly requested
- Do not push to Git remotes
- Do not modify tests to conceal defects
- Do not claim completion without build, test, and `verify-change.ps1` verification
- Keep `Awayra.Core` free of WPF dependencies
- Record external packages in `docs/DEPENDENCIES.md`
- Read `docs/REGRESSION_BASELINE.md` before regression-sensitive work

## Do not touch

Never read or modify the old project at `D:\curser\AWAYRA\AWAYRA`.
