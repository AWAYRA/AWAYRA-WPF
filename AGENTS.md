# Awayra Agent Instructions

## Architecture

- Windows-only native WPF application
- .NET 10 LTS, C#, XAML, MVVM with CommunityToolkit.Mvvm
- No Tauri, Rust, Node, Electron, MAUI, WinUI, web frontend, or JavaScript

## Development commands

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\dev.ps1
powershell -ExecutionPolicy Bypass -File .\scripts\test.ps1
powershell -ExecutionPolicy Bypass -File .\scripts\publish.ps1
powershell -ExecutionPolicy Bypass -File .\scripts\smoke.ps1
```

## Rules

- Use Debug builds for iteration; publish Release only after tests pass
- Do not create installers unless explicitly requested
- Do not push to Git remotes
- Do not modify tests to conceal defects
- Do not claim completion without build, test, and smoke verification
- Keep `Awayra.Core` free of WPF dependencies
- Record external packages in `docs/DEPENDENCIES.md`

## Do not touch

Never read or modify the old project at `D:\curser\AWAYRA\AWAYRA`.
