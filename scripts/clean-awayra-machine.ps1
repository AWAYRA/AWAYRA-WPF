#Requires -Version 5.1
<#
.SYNOPSIS
Removes Awayra-owned runtime remnants from the current Windows user account.
.DESCRIPTION
Prints every path and registry value before deletion. Does not remove unrelated software.
#>
param(
    [string]$ReportPath = (Join-Path (Split-Path $PSScriptRoot -Parent) "docs\MACHINE_CLEANUP_REPORT.md")
)

$ErrorActionPreference = "Stop"

$report = [System.Collections.Generic.List[string]]::new()
function Add-Report([string]$Line) { $report.Add($Line) }

function Get-ProcessReport {
    Get-Process -Name "Awayra" -ErrorAction SilentlyContinue | ForEach-Object {
        $path = try { $_.MainModule.FileName } catch { "<access denied>" }
        $hash = if ($path -and (Test-Path $path)) { (Get-FileHash $path -Algorithm SHA256).Hash } else { "n/a" }
        $version = try { $_.MainModule.FileVersionInfo.FileVersion } catch { "n/a" }
        [PSCustomObject]@{
            PID = $_.Id
            Path = $path
            StartTime = $_.StartTime
            Version = $version
            SHA256 = $hash
        }
    }
}

Add-Report "# Machine Cleanup Report"
Add-Report ""
Add-Report "Timestamp: $(Get-Date -Format o)"
Add-Report ""

# Phase 2: stop processes
Add-Report "## Processes before stop"
$before = @(Get-ProcessReport)
if ($before.Count -eq 0) {
    Add-Report "- None"
} else {
    foreach ($p in $before) {
        Add-Report "- PID $($p.PID) | $($p.Path) | $($p.StartTime) | v$($p.Version) | $($p.SHA256)"
    }
}

foreach ($p in $before) {
    Stop-Process -Id $p.PID -Force -ErrorAction SilentlyContinue
}
Start-Sleep -Seconds 2

$afterStop = @(Get-ProcessReport)
Add-Report ""
Add-Report "## Processes after stop"
if ($afterStop.Count -eq 0) { Add-Report "- None (PASS)" } else { foreach ($p in $afterStop) { Add-Report "- STILL RUNNING: PID $($p.PID) | $($p.Path)" } }

# Directories
$dirs = @(
    (Join-Path $env:LOCALAPPDATA 'Awayra'),
    (Join-Path $env:APPDATA 'Awayra'),
    (Join-Path $env:ProgramData 'Awayra'),
    (Join-Path $env:LOCALAPPDATA 'Programs\Awayra')
)
if ($env:ProgramFiles) { $dirs += Join-Path $env:ProgramFiles 'Awayra' }
if (${env:ProgramFiles(x86)}) { $dirs += Join-Path ${env:ProgramFiles(x86)} 'Awayra' }
$tempPatterns = @("Awayra", "Awayra-*", "Awayra_*")
foreach ($pattern in $tempPatterns) {
    Get-ChildItem -Path $env:TEMP -Filter $pattern -ErrorAction SilentlyContinue | ForEach-Object { $dirs += $_.FullName }
}

Add-Report ""
Add-Report "## Directories to remove"
$removedDirs = @()
$failedDirs = @()
foreach ($dir in $dirs | Select-Object -Unique) {
    if ([string]::IsNullOrWhiteSpace($dir)) { continue }
    Add-Report "- $dir"
    if (Test-Path $dir) {
        try {
            Remove-Item $dir -Recurse -Force
            $removedDirs += $dir
        } catch {
            $failedDirs += "$dir ($($_.Exception.Message))"
        }
    }
}

# Shortcuts
$shortcutRoots = @(
    [Environment]::GetFolderPath('Desktop'),
    [Environment]::GetFolderPath('CommonDesktopDirectory'),
    (Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs"),
    (Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs\Startup"),
    (Join-Path $env:ProgramData "Microsoft\Windows\Start Menu\Programs"),
    (Join-Path $env:ProgramData "Microsoft\Windows\Start Menu\Programs\StartUp")
)
$removedShortcuts = @()
Add-Report ""
Add-Report "## Shortcuts to remove"
foreach ($root in $shortcutRoots) {
    if (-not (Test-Path $root)) { continue }
    Get-ChildItem $root -Recurse -Include *.lnk -ErrorAction SilentlyContinue | ForEach-Object {
        $name = $_.Name
        if ($name -match 'Awayra') {
            Add-Report "- $($_.FullName)"
            try { Remove-Item $_.FullName -Force; $removedShortcuts += $_.FullName } catch { }
        } else {
            $shell = New-Object -ComObject WScript.Shell
            $link = $shell.CreateShortcut($_.FullName)
            if ($link.TargetPath -match 'Awayra') {
                Add-Report "- $($_.FullName) -> $($link.TargetPath)"
                try { Remove-Item $_.FullName -Force; $removedShortcuts += $_.FullName } catch { }
            }
        }
    }
}

# Registry Run value
Add-Report ""
Add-Report "## Registry entries to remove"
$removedRegistry = @()
$failedRegistry = @()
$runKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
if (Test-Path $runKey) {
    $val = Get-ItemProperty -Path $runKey -Name 'Awayra' -ErrorAction SilentlyContinue
    if ($val) {
        Add-Report "- $runKey\Awayra = $($val.Awayra)"
        try { Remove-ItemProperty -Path $runKey -Name 'Awayra'; $removedRegistry += "$runKey\Awayra" } catch { $failedRegistry += "$runKey\Awayra" }
    }
}

$extraKeys = @(
    'HKCU:\Software\Awayra',
    'HKCU:\Software\Classes\Applications\Awayra.exe'
)
foreach ($key in $extraKeys) {
    if (Test-Path $key) {
        Add-Report "- $key"
        try { Remove-Item $key -Recurse -Force; $removedRegistry += $key } catch { $failedRegistry += $key }
    }
}

function Remove-AwayraUninstallEntries([string]$Root) {
    if (-not (Test-Path $Root)) { return }
    Get-ChildItem $Root -ErrorAction SilentlyContinue | ForEach-Object {
        $display = (Get-ItemProperty $_.PSPath -ErrorAction SilentlyContinue).DisplayName
        $install = (Get-ItemProperty $_.PSPath -ErrorAction SilentlyContinue).InstallLocation
        $uninstall = (Get-ItemProperty $_.PSPath -ErrorAction SilentlyContinue).UninstallString
        if ($display -eq 'Awayra' -and (($install -match 'Awayra') -or ($uninstall -match 'Awayra'))) {
            Add-Report "- $($_.PSPath) DisplayName=Awayra"
            try { Remove-Item $_.PSPath -Recurse -Force; $script:removedRegistry += $_.PSPath } catch { $script:failedRegistry += $_.PSPath }
        }
    }
}
Remove-AwayraUninstallEntries 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall'
Remove-AwayraUninstallEntries 'HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall'
Remove-AwayraUninstallEntries 'HKLM:\Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall'

# Services and scheduled tasks
Add-Report ""
Add-Report "## Services and scheduled tasks"
$svc = Get-Service -ErrorAction SilentlyContinue | Where-Object { $_.Name -eq 'Awayra' -or $_.DisplayName -eq 'Awayra' }
$removedSvc = @()
foreach ($s in $svc) {
    Add-Report "- Service: $($s.Name)"
    try { if ($s.Status -ne 'Stopped') { Stop-Service $s.Name -Force }; sc.exe delete $s.Name | Out-Null; $removedSvc += $s.Name } catch { }
}

$tasks = schtasks /Query /FO LIST /V 2>$null | Out-String
$removedTasks = @()
if ($tasks -match 'Awayra') {
    schtasks /Query /FO CSV 2>$null | ConvertFrom-Csv | Where-Object { $_.TaskName -match 'Awayra' } | ForEach-Object {
        Add-Report "- Scheduled task: $($_.TaskName)"
        schtasks /Delete /TN $_.TaskName /F 2>$null | Out-Null
        $removedTasks += $_.TaskName
    }
} else {
    Add-Report "- No Awayra scheduled tasks found"
}

# Final verification
Add-Report ""
Add-Report "## Final verification"
$finalProcesses = @(Get-ProcessReport)
$verdict = 'PASSED'
if ($finalProcesses.Count -gt 0) { $verdict = 'BLOCKED'; Add-Report "- FAIL: Awayra processes still running" } else { Add-Report "- PASS: No Awayra.exe processes" }

foreach ($dir in $dirs | Select-Object -Unique) {
    if ([string]::IsNullOrWhiteSpace($dir)) { continue }
    if (Test-Path $dir) { $verdict = 'BLOCKED'; Add-Report "- FAIL: Awayra directory still exists: $dir" }
}
$runVal = Get-ItemProperty -Path $runKey -Name 'Awayra' -ErrorAction SilentlyContinue
if ($runVal) { $verdict = 'BLOCKED'; Add-Report "- FAIL: Startup Run value still exists" } else { Add-Report "- PASS: No HKCU Run\Awayra value" }

Add-Report ""
Add-Report "## Summary"
Add-Report "- Processes stopped: $($before.Count)"
Add-Report "- Directories removed: $($removedDirs.Count)"
Add-Report "- Shortcuts removed: $($removedShortcuts.Count)"
Add-Report "- Registry entries removed: $($removedRegistry.Count)"
Add-Report "- Services removed: $($removedSvc.Count)"
Add-Report "- Scheduled tasks removed: $($removedTasks.Count)"
if ($failedDirs.Count -gt 0) { Add-Report "- Failed directories: $($failedDirs -join '; ')"; $verdict = 'BLOCKED' }
if ($failedRegistry.Count -gt 0) { Add-Report "- Failed registry: $($failedRegistry -join '; ')"; $verdict = 'BLOCKED' }
Add-Report ""
Add-Report "## CLEANUP VERDICT: $verdict"

$reportDir = Split-Path $ReportPath -Parent
if (-not (Test-Path $reportDir)) { New-Item -ItemType Directory -Path $reportDir -Force | Out-Null }
$report -join "`n" | Set-Content -Path $ReportPath -Encoding UTF8
Write-Host "Cleanup report: $ReportPath"
Write-Host "CLEANUP VERDICT: $verdict"
if ($verdict -eq 'BLOCKED') { exit 1 }
