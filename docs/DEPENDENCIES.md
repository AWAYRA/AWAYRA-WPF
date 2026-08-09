# Dependencies

## NuGet packages

| Package | Version | Project | Purpose |
|---|---:|---|---|
| CommunityToolkit.Mvvm | 8.4.2 | Awayra.App | MVVM primitives such as `ObservableObject` and `RelayCommand` |

## Windows and .NET framework components

| Component | Project | Purpose |
|---|---|---|
| WPF | Awayra.App | Native Windows desktop interface |
| System.Windows.Forms | Awayra.App | `NotifyIcon` system tray integration |
| System.Text.Json | Awayra.Core, Awayra.App | Local JSON persistence |

## Build-only tools

| Tool | Purpose |
|---|---|
| .NET 10 SDK | Restore, build, and publish |
| PowerShell | Development and release scripts |
| Inno Setup 7 | Optional Windows installer creation |
| Windows SignTool | Optional Authenticode signing |

Awayra has no runtime web service, analytics SDK, advertising SDK, database server, or mandatory network dependency.
