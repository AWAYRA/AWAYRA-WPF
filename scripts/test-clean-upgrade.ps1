param(
    [Parameter(Mandatory = $true)]
    [string]$InstallerPath,

    [Parameter(Mandatory = $true)]
    [string]$ExpectedVersion
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

function Assert-PathExists {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [string]$Because
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        throw "Expected path does not exist: $Path$(if ($Because) { " ($Because)" })"
    }
}

function Assert-PathMissing {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [string]$Because
    )

    if (Test-Path -LiteralPath $Path) {
        throw "Expected path to be removed: $Path$(if ($Because) { " ($Because)" })"
    }
}

function Assert-FileContent {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$Expected
    )

    Assert-PathExists $Path
    $actual = (Get-Content -LiteralPath $Path -Raw).Trim()
    if ($actual -ne $Expected) {
        throw "File $Path was expected to still contain '$Expected' but contained '$actual'."
    }
}

function Invoke-AwayraInstaller {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [string[]]$ExtraArguments = @()
    )

    $logRoot = if ($env:RUNNER_TEMP) { $env:RUNNER_TEMP } else { $env:TEMP }
    $logPath = Join-Path $logRoot ("awayra-install-" + [guid]::NewGuid().ToString("N") + ".log")
    $arguments = @(
        "/VERYSILENT",
        "/SUPPRESSMSGBOXES",
        "/NORESTART",
        "/SP-",
        "/LOG=`"$logPath`""
    ) + $ExtraArguments

    $process = Start-Process -FilePath $Path -ArgumentList $arguments -Wait -PassThru
    if ($process.ExitCode -ne 0) {
        $log = if (Test-Path $logPath) { Get-Content $logPath -Raw } else { "Installer log missing." }
        throw "Awayra installer failed with exit code $($process.ExitCode).`n$log"
    }
}

function Set-UserDataFixtures {
    param([Parameter(Mandatory = $true)][string]$Generation)

    New-Item -ItemType Directory -Path (Join-Path $script:DataDir "Logs") -Force | Out-Null
    New-Item -ItemType Directory -Path $script:RoamingDataDir -Force | Out-Null

    "settings-$Generation" | Set-Content (Join-Path $script:DataDir "settings.json") -Encoding UTF8
    "state-$Generation" | Set-Content (Join-Path $script:DataDir "state.json") -Encoding UTF8
    "stats-$Generation" | Set-Content (Join-Path $script:DataDir "stats.json") -Encoding UTF8
    "log-$Generation" | Set-Content (Join-Path $script:DataDir "Logs\awayra.log") -Encoding UTF8
    "roaming-$Generation" | Set-Content (Join-Path $script:RoamingDataDir "legacy.txt") -Encoding UTF8
}

function Set-LegacyFixtures {
    param([Parameter(Mandatory = $true)][string]$Generation)

    New-Item -ItemType Directory -Path $script:AppDir -Force | Out-Null
    "legacy-$Generation" | Set-Content (Join-Path $script:AppDir "stale-$Generation.dll") -Encoding UTF8
    Set-UserDataFixtures -Generation $Generation
}

function Assert-ProgramFilesReplaced {
    param([Parameter(Mandatory = $true)][string]$Generation)

    $executable = Join-Path $script:AppDir "Awayra.exe"
    Assert-PathExists $executable
    Assert-PathMissing (Join-Path $script:AppDir "stale-$Generation.dll") "program files must always be replaced"

    $actualVersion = (Get-Item $executable).VersionInfo.ProductVersion
    if ($actualVersion -notlike "$ExpectedVersion*") {
        throw "Expected installed product version $ExpectedVersion, found '$actualVersion'."
    }
}

function Assert-UserDataPreserved {
    param([Parameter(Mandatory = $true)][string]$Generation)

    Assert-FileContent (Join-Path $script:DataDir "settings.json") "settings-$Generation"
    Assert-FileContent (Join-Path $script:DataDir "state.json") "state-$Generation"
    Assert-FileContent (Join-Path $script:DataDir "stats.json") "stats-$Generation"
    Assert-FileContent (Join-Path $script:DataDir "Logs\awayra.log") "log-$Generation"
    Assert-FileContent (Join-Path $script:RoamingDataDir "legacy.txt") "roaming-$Generation"
}

function Assert-UserDataRemoved {
    Assert-PathMissing (Join-Path $script:DataDir "settings.json") "/CLEANDATA=yes was requested"
    Assert-PathMissing (Join-Path $script:DataDir "state.json") "/CLEANDATA=yes was requested"
    Assert-PathMissing (Join-Path $script:DataDir "stats.json") "/CLEANDATA=yes was requested"
    Assert-PathMissing $script:RoamingDataDir "/CLEANDATA=yes was requested"
}

function Reset-Machine {
    Get-Process -Name "Awayra" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
    Remove-Item $script:AppDir -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item $script:DataDir -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item $script:RoamingDataDir -Recurse -Force -ErrorAction SilentlyContinue
    Remove-ItemProperty -Path $script:RunKey -Name "Awayra" -ErrorAction SilentlyContinue
}

$InstallerPath = (Resolve-Path $InstallerPath).Path
$AppDir = Join-Path $env:LOCALAPPDATA "Programs\Awayra"
$DataDir = Join-Path $env:LOCALAPPDATA "Awayra"
$RoamingDataDir = Join-Path $env:APPDATA "Awayra"
$RunKey = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run"

try {
    Reset-Machine

    # ---------------------------------------------------------------------------------------------
    # 1. A silent upgrade over an existing installation must replace program files but KEEP data.
    # ---------------------------------------------------------------------------------------------
    Write-Host "[1/4] Upgrade over existing installation must preserve user data."
    Set-LegacyFixtures -Generation "before-upgrade"
    Invoke-AwayraInstaller -Path $InstallerPath
    Assert-ProgramFilesReplaced -Generation "before-upgrade"
    Assert-UserDataPreserved -Generation "before-upgrade"
    Write-Host "      OK - settings, statistics and logs survived the upgrade."

    # ---------------------------------------------------------------------------------------------
    # 2. A second silent upgrade must still preserve data (no drift on repeated reinstalls).
    # ---------------------------------------------------------------------------------------------
    Write-Host "[2/4] Repeated reinstall must still preserve user data."
    Set-LegacyFixtures -Generation "second-upgrade"
    Invoke-AwayraInstaller -Path $InstallerPath
    Assert-ProgramFilesReplaced -Generation "second-upgrade"
    Assert-UserDataPreserved -Generation "second-upgrade"
    Write-Host "      OK - data preserved again."

    # ---------------------------------------------------------------------------------------------
    # 3. An explicit /CLEANDATA=yes must perform the old destructive clean install.
    # ---------------------------------------------------------------------------------------------
    Write-Host "[3/4] /CLEANDATA=yes must remove user data on request."
    Set-LegacyFixtures -Generation "before-reset"
    Invoke-AwayraInstaller -Path $InstallerPath -ExtraArguments @("/CLEANDATA=yes")
    Assert-ProgramFilesReplaced -Generation "before-reset"
    Assert-UserDataRemoved

    $runKeyProperties = Get-ItemProperty -Path $RunKey -ErrorAction SilentlyContinue
    $runValueProperty = if ($null -ne $runKeyProperties) { $runKeyProperties.PSObject.Properties["Awayra"] } else { $null }
    if ($null -ne $runValueProperty) {
        throw "Startup registry value was not removed by /CLEANDATA=yes: $($runValueProperty.Value)"
    }
    Write-Host "      OK - fresh install removed data and startup registration."

    # ---------------------------------------------------------------------------------------------
    # 4. Silent uninstall must remove everything Awayra owns.
    # ---------------------------------------------------------------------------------------------
    Write-Host "[4/4] Silent uninstall must remove application and data directories."
    Set-UserDataFixtures -Generation "before-uninstall"

    $uninstaller = Get-ChildItem $AppDir -Filter "unins*.exe" -File | Select-Object -First 1
    if (-not $uninstaller) {
        throw "Awayra uninstaller was not created."
    }

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
    Write-Host "      OK - uninstall removed everything."

    Write-Host ""
    Write-Host "INSTALLER UPGRADE TEST: PASSED"
}
finally {
    Reset-Machine
}
