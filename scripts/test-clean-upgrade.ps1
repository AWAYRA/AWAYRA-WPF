param(
    [Parameter(Mandatory = $true)]
    [string]$InstallerPath,

    [Parameter(Mandatory = $true)]
    [string]$ExpectedVersion
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

function Assert-PathExists {
    param([Parameter(Mandatory = $true)][string]$Path)

    if (-not (Test-Path -LiteralPath $Path)) {
        throw "Expected path does not exist: $Path"
    }
}

function Assert-PathMissing {
    param([Parameter(Mandatory = $true)][string]$Path)

    if (Test-Path -LiteralPath $Path) {
        throw "Expected path to be removed: $Path"
    }
}

function Invoke-AwayraInstaller {
    param([Parameter(Mandatory = $true)][string]$Path)

    $logPath = Join-Path $env:RUNNER_TEMP ("awayra-install-" + [guid]::NewGuid().ToString("N") + ".log")
    $arguments = @(
        "/VERYSILENT",
        "/SUPPRESSMSGBOXES",
        "/NORESTART",
        "/SP-",
        "/LOG=`"$logPath`""
    )

    $process = Start-Process -FilePath $Path -ArgumentList $arguments -Wait -PassThru
    if ($process.ExitCode -ne 0) {
        $log = if (Test-Path $logPath) { Get-Content $logPath -Raw } else { "Installer log missing." }
        throw "Awayra installer failed with exit code $($process.ExitCode).`n$log"
    }
}

function Set-LegacyFixtures {
    param([Parameter(Mandatory = $true)][string]$Generation)

    New-Item -ItemType Directory -Path $script:AppDir -Force | Out-Null
    New-Item -ItemType Directory -Path (Join-Path $script:DataDir "Logs") -Force | Out-Null
    New-Item -ItemType Directory -Path $script:RoamingDataDir -Force | Out-Null

    "legacy-$Generation" | Set-Content (Join-Path $script:AppDir "stale-$Generation.dll") -Encoding UTF8
    '{"schemaVersion":0,"eyeResetIntervalMinutes":999}' | Set-Content (Join-Path $script:DataDir "settings.json") -Encoding UTF8
    '{"activeBreak":"Eye"}' | Set-Content (Join-Path $script:DataDir "state.json") -Encoding UTF8
    '{"eyeCompleted":999}' | Set-Content (Join-Path $script:DataDir "stats.json") -Encoding UTF8
    "legacy-log-$Generation" | Set-Content (Join-Path $script:DataDir "Logs\awayra.log") -Encoding UTF8
    "legacy-roaming-$Generation" | Set-Content (Join-Path $script:RoamingDataDir "legacy.txt") -Encoding UTF8

    New-Item -Path $script:RunKey -Force | Out-Null
    Set-ItemProperty -Path $script:RunKey -Name "Awayra" -Value "`"$script:AppDir\Awayra.exe`""
}

function Assert-CleanInstallState {
    param([Parameter(Mandatory = $true)][string]$Generation)

    $executable = Join-Path $script:AppDir "Awayra.exe"
    Assert-PathExists $executable
    Assert-PathMissing (Join-Path $script:AppDir "stale-$Generation.dll")
    Assert-PathMissing (Join-Path $script:DataDir "settings.json")
    Assert-PathMissing (Join-Path $script:DataDir "state.json")
    Assert-PathMissing (Join-Path $script:DataDir "stats.json")
    Assert-PathMissing (Join-Path $script:DataDir "Logs\awayra.log")
    Assert-PathMissing $script:RoamingDataDir

    $actualVersion = (Get-Item $executable).VersionInfo.ProductVersion
    if ($actualVersion -notlike "$ExpectedVersion*") {
        throw "Expected installed product version $ExpectedVersion, found '$actualVersion'."
    }

    $runKeyProperties = Get-ItemProperty -Path $script:RunKey -ErrorAction SilentlyContinue
    $runValueProperty = if ($null -ne $runKeyProperties) {
        $runKeyProperties.PSObject.Properties["Awayra"]
    }
    else {
        $null
    }

    if ($null -ne $runValueProperty) {
        throw "Legacy Awayra startup registry value was not removed: $($runValueProperty.Value)"
    }
}

$InstallerPath = (Resolve-Path $InstallerPath).Path
$AppDir = Join-Path $env:LOCALAPPDATA "Programs\Awayra"
$DataDir = Join-Path $env:LOCALAPPDATA "Awayra"
$RoamingDataDir = Join-Path $env:APPDATA "Awayra"
$RunKey = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run"

try {
    Get-Process -Name "Awayra" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
    Remove-Item $AppDir -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item $DataDir -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item $RoamingDataDir -Recurse -Force -ErrorAction SilentlyContinue
    Remove-ItemProperty -Path $RunKey -Name "Awayra" -ErrorAction SilentlyContinue

    Write-Host "Creating simulated legacy installation fixtures."
    Set-LegacyFixtures -Generation "before-first-install"

    Write-Host "Installing Awayra $ExpectedVersion over legacy files and settings."
    Invoke-AwayraInstaller -Path $InstallerPath
    Assert-CleanInstallState -Generation "before-first-install"

    Write-Host "Creating a second generation of stale settings to simulate an upgrade/reinstall."
    Set-LegacyFixtures -Generation "before-upgrade"

    Write-Host "Reinstalling Awayra $ExpectedVersion and validating clean-upgrade behavior."
    Invoke-AwayraInstaller -Path $InstallerPath
    Assert-CleanInstallState -Generation "before-upgrade"

    $uninstaller = Get-ChildItem $AppDir -Filter "unins*.exe" -File | Select-Object -First 1
    if (-not $uninstaller) {
        throw "Awayra uninstaller was not created."
    }

    Write-Host "Uninstalling and validating that application-owned data is removed."
    $uninstallProcess = Start-Process -FilePath $uninstaller.FullName `
        -ArgumentList @("/VERYSILENT", "/SUPPRESSMSGBOXES", "/NORESTART") `
        -Wait `
        -PassThru
    if ($uninstallProcess.ExitCode -ne 0) {
        throw "Awayra uninstaller failed with exit code $($uninstallProcess.ExitCode)."
    }

    Assert-PathMissing $AppDir
    Assert-PathMissing $DataDir
    Assert-PathMissing $RoamingDataDir

    Write-Host "CLEAN UPGRADE INSTALLER TEST: PASSED"
}
finally {
    Get-Process -Name "Awayra" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
    Remove-Item $AppDir -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item $DataDir -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item $RoamingDataDir -Recurse -Force -ErrorAction SilentlyContinue
    Remove-ItemProperty -Path $RunKey -Name "Awayra" -ErrorAction SilentlyContinue
}