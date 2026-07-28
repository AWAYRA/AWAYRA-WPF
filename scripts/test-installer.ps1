$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

. (Join-Path $PSScriptRoot "launch-common.ps1")

function Get-AppVersionFromExe([string]$ExePath) {
    $versionInfo = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($ExePath)
    $productVersion = ($versionInfo.ProductVersion -split '\+')[0].Trim()
    if ([string]::IsNullOrWhiteSpace($productVersion)) {
        throw "Product version metadata missing on $ExePath"
    }

    $parts = $productVersion.Split('.')
    while ($parts.Count -lt 3) { $parts += "0" }
    return ($parts[0..2] -join '.')
}

function Stop-RepoOwnedProcesses([string]$RepoRoot) {
    Stop-AllAwayraProcesses

    Get-Process -Name testhost, vstest.console, dotnet -ErrorAction SilentlyContinue | ForEach-Object {
        try {
            $cmd = (Get-CimInstance Win32_Process -Filter "ProcessId=$($_.Id)").CommandLine
            if ($cmd -and $cmd -like "*$RepoRoot*") {
                Stop-Process -Id $_.Id -Force -ErrorAction SilentlyContinue
            }
        }
        catch { }
    }
}

function Invoke-SilentInstall {
    param(
        [Parameter(Mandatory = $true)][string]$InstallerPath,
        [Parameter(Mandatory = $true)][string]$InstallDir,
        [Parameter(Mandatory = $true)][string]$LogPath
    )

    $arguments = @(
        "/VERYSILENT",
        "/SUPPRESSMSGBOXES",
        "/NORESTART",
        "/NOICONS",
        "/LOG=`"$LogPath`"",
        "/DIR=`"$InstallDir`""
    )

    $process = Start-Process -FilePath $InstallerPath -ArgumentList $arguments -Wait -PassThru
    return $process.ExitCode
}

function Get-UninstallerPath([string]$InstallDir) {
    $unins = Get-ChildItem $InstallDir -Filter "unins*.exe" -ErrorAction SilentlyContinue | Select-Object -First 1
    if (-not $unins) { throw "Uninstaller not found under $InstallDir" }
    return $unins.FullName
}

function Assert-NoAwayraProcesses {
    $processes = @(Get-Process -Name Awayra -ErrorAction SilentlyContinue)
    if ($processes.Count -gt 0) {
        throw "Unexpected Awayra process(es) still running: $($processes.Id -join ', ')"
    }
}

function Test-InstalledAppsEntry([string]$DisplayName, [string]$InstallLocation) {
    $uninstallRoots = @(
        "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\*"
    )

    $entries = foreach ($root in $uninstallRoots) {
        Get-ItemProperty $root -ErrorAction SilentlyContinue |
            Where-Object { $_.DisplayName -eq $DisplayName }
    }

    if (@($entries).Count -ne 1) {
        throw "Expected exactly one Add/Remove Programs entry for '$DisplayName', found $(@($entries).Count)."
    }

    $entry = @($entries)[0]
    if ($entry.InstallLocation -and -not $entry.InstallLocation.StartsWith($InstallLocation, [System.StringComparison]::OrdinalIgnoreCase)) {
        Write-Host "InstallLocation registry value: $($entry.InstallLocation)"
    }
}

$root = (Get-RepoRoot).Path
$installerDir = Join-Path $root "artifacts\installer"
$publishExe = Join-Path $root "artifacts\publish\win-x64\Awayra.exe"
$logRoot = Join-Path $installerDir "test-logs"
$testId = (Get-Date).ToUniversalTime().ToString("yyyyMMdd-HHmmss") + "-" + [Guid]::NewGuid().ToString("N").Substring(0, 8)
$installDir = Join-Path $env:TEMP "Awayra-InstallerTest-$testId"
$installLog = Join-Path $logRoot "install-$testId.log"
$upgradeLog = Join-Path $logRoot "upgrade-$testId.log"
$uninstallLog = Join-Path $logRoot "uninstall-$testId.log"
$uiTestLog = Join-Path $logRoot "ui-tests-$testId.log"

New-Item -ItemType Directory -Path $logRoot -Force | Out-Null

Push-Location $root
try {
    Stop-RepoOwnedProcesses -RepoRoot $root

    if (-not (Test-Path $publishExe)) {
        throw "Published executable not found. Run scripts\build-installer.ps1 first: $publishExe"
    }

    $version = Get-AppVersionFromExe -ExePath $publishExe
    $installerPath = Join-Path $installerDir "Awayra-Setup-$version-x64.exe"
    if (-not (Test-Path $installerPath)) {
        throw "Installer not found: $installerPath"
    }

    $publishedHash = Get-ExeSha256 $publishExe
    Write-Host "Using installer: $installerPath"
    Write-Host "Test install directory: $installDir"

    if (Test-Path $installDir) {
        Remove-Item $installDir -Recurse -Force
    }

    $installExit = Invoke-SilentInstall -InstallerPath $installerPath -InstallDir $installDir -LogPath $installLog
    if ($installExit -ne 0) {
        throw "Silent installation failed with exit code $installExit. Log: $installLog"
    }

    $installedExe = Join-Path $installDir "Awayra.exe"
    if (-not (Test-Path $installedExe)) {
        throw "Installed Awayra.exe not found: $installedExe"
    }

    $installedHash = Get-ExeSha256 $installedExe
    if ($installedHash -ne $publishedHash) {
        throw "Installed EXE hash mismatch. Expected $publishedHash Actual $installedHash"
    }

    $installedVersion = Get-AppVersionFromExe -ExePath $installedExe
    if ($installedVersion -ne $version) {
        throw "Installed version mismatch. Expected $version Actual $installedVersion"
    }

    $uninstaller = Get-UninstallerPath -InstallDir $installDir
    Write-Host "Uninstaller: $uninstaller"

    Test-InstalledAppsEntry -DisplayName "Awayra" -InstallLocation $installDir

    $uiFilter = "FullyQualifiedName~Awayra.UiTests&FullyQualifiedName!~T20_RealWindowsIdle"
    $env:AWAYRA_UI_TEST_EXE = $installedExe

    dotnet test tests\Awayra.UiTests\Awayra.UiTests.csproj -c Debug --filter $uiFilter *> $uiTestLog
    $uiExit = $LASTEXITCODE
    Remove-Item Env:AWAYRA_UI_TEST_EXE -ErrorAction SilentlyContinue

    if ($uiExit -ne 0) {
        throw "Installed-app UI tests failed (exit $uiExit). Log: $uiTestLog"
    }

    Stop-AllAwayraProcesses

    $upgradeExit = Invoke-SilentInstall -InstallerPath $installerPath -InstallDir $installDir -LogPath $upgradeLog
    if ($upgradeExit -ne 0) {
        throw "Silent upgrade failed with exit code $upgradeExit. Log: $upgradeLog"
    }

    if (-not (Test-Path $installedExe)) {
        throw "Installed Awayra.exe missing after upgrade."
    }

    $postUpgradeHash = Get-ExeSha256 $installedExe
    if ($postUpgradeHash -ne $publishedHash) {
        throw "Post-upgrade EXE hash mismatch. Expected $publishedHash Actual $postUpgradeHash"
    }

    Test-InstalledAppsEntry -DisplayName "Awayra" -InstallLocation $installDir
    Assert-NoAwayraProcesses

    $uninstallArgs = @("/VERYSILENT", "/SUPPRESSMSGBOXES", "/NORESTART", "/LOG=`"$uninstallLog`"")
    $uninstallProcess = Start-Process -FilePath $uninstaller -ArgumentList $uninstallArgs -Wait -PassThru
    if ($uninstallProcess.ExitCode -ne 0) {
        throw "Silent uninstall failed with exit code $($uninstallProcess.ExitCode). Log: $uninstallLog"
    }

    if (Test-Path $installedExe) {
        throw "Awayra.exe still present after uninstall: $installedExe"
    }

    Assert-NoAwayraProcesses

    $userDataRoot = Join-Path $env:LOCALAPPDATA "Awayra"
    if (-not (Test-Path $userDataRoot)) {
        Write-Host "User data root not present (acceptable for clean machine): $userDataRoot"
    }
    else {
        Write-Host "User data preserved at: $userDataRoot"
    }

    if (Test-Path $installDir) {
        $remaining = Get-ChildItem $installDir -Recurse -ErrorAction SilentlyContinue
        if ($remaining) {
            Remove-Item $installDir -Recurse -Force -ErrorAction SilentlyContinue
        }
    }

    Write-Host ""
    Write-Host "Installer automated test: PASSED"
    Write-Host "Install log: $installLog"
    Write-Host "Upgrade log: $upgradeLog"
    Write-Host "Uninstall log: $uninstallLog"
    Write-Host "UI test log: $uiTestLog"
}
finally {
    Remove-Item Env:AWAYRA_UI_TEST_EXE -ErrorAction SilentlyContinue
    Stop-AllAwayraProcesses
    Pop-Location
}
