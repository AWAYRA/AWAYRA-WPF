param(
    [switch]$KeepRunning
)

$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "launch-common.ps1")
$root = Split-Path $PSScriptRoot -Parent
$exe = Join-Path $root "artifacts\publish\win-x64\Awayra.exe"

if (-not (Test-Path $exe)) {
    Write-Error "Published executable not found. Run scripts/publish.ps1 first."
    exit 1
}

Stop-AwayraProcessesUnderRoot -RootPath $root

$proc = Start-Process -FilePath $exe -PassThru
Start-Sleep -Seconds 5
if ($proc.HasExited) {
    Write-Error "Awayra process exited early."
    exit 1
}

$second = Start-Process -FilePath $exe -PassThru
Start-Sleep -Seconds 2
$processes = Get-Process Awayra -ErrorAction SilentlyContinue
if ($null -eq $processes -or $processes.Count -gt 2) {
    Write-Error "Unexpected Awayra process count: $($processes.Count)"
    exit 1
}

$dataRoot = Join-Path $env:LOCALAPPDATA "Awayra"
if (-not (Test-Path $dataRoot)) {
    New-Item -ItemType Directory -Path $dataRoot -Force | Out-Null
}
if (-not (Test-Path $dataRoot)) {
    Write-Error "Could not create data directory: $dataRoot"
    exit 1
}

if (-not $KeepRunning) {
    Stop-AwayraProcessesUnderRoot -RootPath $root
}

Write-Host "Smoke test passed."
