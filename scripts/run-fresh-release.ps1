param(
    [string]$ExePath = (Join-Path (Resolve-Path (Join-Path $PSScriptRoot "..")).Path "artifacts\publish\win-x64\Awayra.exe")
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "launch-common.ps1")

Stop-AwayraProcessesUnderRoot -RootPath (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
if (-not (Test-Path $ExePath)) { throw "Release executable not found: $ExePath" }

Write-LaunchReport $ExePath
$proc = Start-Process -FilePath $ExePath -PassThru
Start-Sleep -Seconds 4
if ($proc.HasExited) { throw "Awayra Release process exited during startup." }

$running = Assert-RunningProcessMatches $ExePath
Write-Host "Running PID: $($running.ProcessId)"
Write-Host "Running path verified: $($running.Path)"
Write-Host "Running hash verified: $($running.Sha256)"
