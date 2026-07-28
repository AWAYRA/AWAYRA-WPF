$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

. (Join-Path $PSScriptRoot "launch-common.ps1")

function Find-InnoSetupCompiler {
    $candidates = @(
        (Join-Path $env:LOCALAPPDATA "Programs\Inno Setup 7\ISCC.exe"),
        "C:\Program Files\Inno Setup 7\ISCC.exe",
        "C:\Program Files (x86)\Inno Setup 7\ISCC.exe"
    )

    foreach ($path in $candidates) {
        if (-not (Test-Path $path)) { continue }

        $versionText = (Get-Item $path).VersionInfo.ProductVersion
        if ([string]::IsNullOrWhiteSpace($versionText)) {
            $versionText = (Get-Item $path).VersionInfo.FileVersion
        }

        if ($versionText -match '(?i)(beta|preview|rc|nightly|dev)') {
            throw "Rejected non-stable Inno Setup build at $path ($versionText)."
        }

        $setupExe = Join-Path (Split-Path $path -Parent) "Compil32.exe"
        if (-not (Test-Path $setupExe)) {
            $setupExe = Join-Path (Split-Path $path -Parent) "Setup.exe"
        }

        $innoVersion = "7.x"
        if (Test-Path $setupExe) {
            $productVersion = (Get-Item $setupExe).VersionInfo.ProductVersion
            if (-not [string]::IsNullOrWhiteSpace($productVersion)) {
                $innoVersion = $productVersion
            }
        }

        return [PSCustomObject]@{
            Path = $path
            Version = $innoVersion
        }
    }

    throw @"
Inno Setup 7 compiler (ISCC.exe) was not found.
Install the stable release:
  winget install --id JRSoftware.InnoSetup.7 -e -s winget -i
"@
}

function Find-SignTool {
    $fromPath = Get-Command signtool.exe -ErrorAction SilentlyContinue
    if ($fromPath) { return $fromPath.Source }

    $kitsRoot = "${env:ProgramFiles(x86)}\Windows Kits\10\bin"
    if (Test-Path $kitsRoot) {
        $latest = Get-ChildItem $kitsRoot -Directory -ErrorAction SilentlyContinue |
            Sort-Object Name -Descending |
            Select-Object -First 1
        if ($latest) {
            $candidate = Join-Path $latest.FullName "x64\signtool.exe"
            if (Test-Path $candidate) { return $candidate }
        }
    }

    return $null
}

function Invoke-AuthenticodeSign {
    param(
        [Parameter(Mandatory = $true)][string]$SignTool,
        [Parameter(Mandatory = $true)][string]$FilePath,
        [Parameter(Mandatory = $true)][string]$CertPath,
        [string]$CertPassword,
        [Parameter(Mandatory = $true)][string]$TimestampUrl
    )

    $args = @(
        "sign",
        "/f", $CertPath,
        "/fd", "SHA256",
        "/tr", $TimestampUrl,
        "/td", "SHA256",
        "/v",
        $FilePath
    )

    if (-not [string]::IsNullOrWhiteSpace($CertPassword)) {
        $args = @("sign", "/f", $CertPath, "/p", $CertPassword, "/fd", "SHA256", "/tr", $TimestampUrl, "/td", "SHA256", "/v", $FilePath)
    }

    & $SignTool @args
    if ($LASTEXITCODE -ne 0) {
        throw "signtool failed for $FilePath (exit $LASTEXITCODE)."
    }

    $signature = Get-AuthenticodeSignature -FilePath $FilePath
    if ($signature.Status -ne "Valid") {
        throw "Authenticode signature verification failed for $FilePath ($($signature.Status))."
    }
}

function Get-PeMachineType([string]$Path) {
    $bytes = [System.IO.File]::ReadAllBytes($Path)
    if ($bytes.Length -lt 64) { throw "Invalid PE file: $Path" }
    $peOffset = [BitConverter]::ToInt32($bytes, 0x3C)
    $machine = [BitConverter]::ToUInt16($bytes, $peOffset + 4)
    switch ($machine) {
        0x8664 { return "x64" }
        0x014C { return "x86" }
        default { return "unknown($machine)" }
    }
}

function Get-AppVersionFromExe([string]$ExePath) {
    $versionInfo = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($ExePath)
    $productVersion = ($versionInfo.ProductVersion -split '\+')[0].Trim()
    if ([string]::IsNullOrWhiteSpace($productVersion)) {
        throw "Product version metadata missing on $ExePath"
    }

    $parts = $productVersion.Split('.')
    while ($parts.Count -lt 4) {
        $parts += "0"
    }

    return [PSCustomObject]@{
        Version = ($parts[0..2] -join '.')
        VersionInfo = ($parts[0..3] -join '.')
    }
}

function Test-PublishDirectory([string]$PublishDir) {
    $exe = Join-Path $PublishDir "Awayra.exe"
    if (-not (Test-Path $exe)) {
        throw "Published executable not found: $exe"
    }

    $forbiddenPatterns = @("*.pdb", "*.Tests.dll", "*.deps.json", "BUILD-IDENTITY.txt")
    foreach ($pattern in $forbiddenPatterns) {
        $matches = Get-ChildItem -Path $PublishDir -Recurse -Filter $pattern -ErrorAction SilentlyContinue
        if ($matches) {
            throw "Publish directory contains forbidden artifact(s): $($matches.FullName -join ', ')"
        }
    }

    return $exe
}

$root = (Get-RepoRoot).Path
$publishDir = Join-Path $root "artifacts\publish\win-x64"
$installerWorkDir = Join-Path $root "artifacts\installer"
$issPath = Join-Path $root "installer\Awayra.iss"
$iconPath = Join-Path $root "src\Awayra.App\Assets\awayra.ico"

Push-Location $root
try {
    Stop-AllAwayraProcesses

    if (Test-Path $publishDir) {
        Remove-Item $publishDir -Recurse -Force
    }
    New-Item -ItemType Directory -Path $publishDir -Force | Out-Null

    dotnet publish src\Awayra.App\Awayra.App.csproj `
        -c Release `
        -r win-x64 `
        --self-contained true `
        -p:PublishSingleFile=true `
        -p:PublishTrimmed=false `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:DebugType=None `
        -p:DebugSymbols=false `
        -p:ReadyToRun=false `
        -o $publishDir
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed." }

    $exe = Test-PublishDirectory -PublishDir $publishDir
    if (-not (Test-Path $iconPath)) {
        throw "Application icon not found: $iconPath"
    }

    Copy-Item $iconPath (Join-Path $publishDir "awayra.ico") -Force

    $version = Get-AppVersionFromExe -ExePath $exe
    if ($version.Version -ne "1.0.0") {
        Write-Host "Using application version $($version.Version)"
    }

    $signingStatus = "UNSIGNED - Windows SmartScreen may show an Unknown Publisher warning."
    $signTool = Find-SignTool
    $certPath = $env:AWAYRA_SIGN_CERT_PATH
    $certPassword = $env:AWAYRA_SIGN_CERT_PASSWORD
    $timestampUrl = $env:AWAYRA_TIMESTAMP_URL

    if (-not [string]::IsNullOrWhiteSpace($certPath)) {
        if (-not $signTool) { throw "AWAYRA_SIGN_CERT_PATH is set but signtool.exe was not found." }
        if (-not (Test-Path $certPath)) { throw "Signing certificate not found: $certPath" }
        if ([string]::IsNullOrWhiteSpace($timestampUrl)) { throw "AWAYRA_TIMESTAMP_URL is required when signing." }

        Invoke-AuthenticodeSign -SignTool $signTool -FilePath $exe -CertPath $certPath -CertPassword $certPassword -TimestampUrl $timestampUrl
        $signingStatus = "SIGNED (Awayra.exe verified)"
    }

    $inno = Find-InnoSetupCompiler
    if (Test-Path $installerWorkDir) {
        Remove-Item $installerWorkDir -Recurse -Force
    }
    New-Item -ItemType Directory -Path $installerWorkDir -Force | Out-Null

    $publishDirForIss = ($publishDir -replace '\\', '\\')
    & $inno.Path $issPath `
        "/DMyAppVersion=$($version.Version)" `
        "/DMyAppVersionInfo=$($version.VersionInfo)" `
        "/DPublishDir=$publishDir"
    if ($LASTEXITCODE -ne 0) { throw "Inno Setup compilation failed." }

    $setupExe = Join-Path $installerWorkDir "Awayra-Setup-$($version.Version)-x64.exe"
    if (-not (Test-Path $setupExe)) {
        throw "Expected installer not found: $setupExe"
    }

    $setupMatches = @(Get-ChildItem $installerWorkDir -Filter "Awayra-Setup-$($version.Version)-x64.exe")
    if ($setupMatches.Count -ne 1) {
        throw "Expected exactly one installer EXE, found $($setupMatches.Count)."
    }

    if (-not [string]::IsNullOrWhiteSpace($certPath)) {
        Invoke-AuthenticodeSign -SignTool $signTool -FilePath $setupExe -CertPath $certPath -CertPassword $certPassword -TimestampUrl $timestampUrl
        $signingStatus = "SIGNED (Awayra.exe and installer verified)"
    }

    $publishHash = (Get-FileHash $exe -Algorithm SHA256).Hash
    $setupHash = (Get-FileHash $setupExe -Algorithm SHA256).Hash
    $setupInfo = Get-Item $setupExe
    $gitCommit = (git rev-parse HEAD 2>$null)
    if (-not $gitCommit) { $gitCommit = "unknown" }
    $buildDateUtc = (Get-Date).ToUniversalTime().ToString("o")
    $dotnetSdk = (dotnet --version)

    $hashFile = Join-Path $installerWorkDir "Awayra-Setup-$($version.Version)-x64.sha256.txt"
    @(
        "$setupHash  $($setupInfo.Name)"
        "PublishedExeSha256=$publishHash"
    ) | Set-Content -Path $hashFile -Encoding UTF8

    $buildInfoPath = Join-Path $installerWorkDir "BUILD-INFO.txt"
    @(
        "Product=Awayra"
        "ProductVersion=$($version.Version)"
        "GitCommit=$gitCommit"
        "BuildDateUtc=$buildDateUtc"
        "DotNetSdk=$dotnetSdk"
        "InnoSetupVersion=$($inno.Version)"
        "PublishedExePath=$exe"
        "PublishedExeSha256=$publishHash"
        "PublishedExeSizeBytes=$((Get-Item $exe).Length)"
        "InstallerPath=$setupExe"
        "InstallerSha256=$setupHash"
        "InstallerSizeBytes=$($setupInfo.Length)"
        "SigningStatus=$signingStatus"
        "Architecture=x64"
        "InstallationScope=PerUser"
        "DefaultInstallDirectory=%LocalAppData%\Programs\Awayra"
        "MinimumWindowsVersion=Windows 10 x64"
        "LicensePage=Omitted (no root LICENSE file)"
    ) | Set-Content -Path $buildInfoPath -Encoding UTF8

    $peArch = Get-PeMachineType $exe
    if ($peArch -ne "x64") { throw "Published Awayra.exe architecture is $peArch, expected x64." }

    Write-Host ""
    Write-Host "Installer build complete."
    Write-Host "Product version: $($version.Version)"
    Write-Host "Published app: $exe"
    Write-Host "Published size: $((Get-Item $exe).Length) bytes"
    Write-Host "Published SHA-256: $publishHash"
    Write-Host "Installer: $setupExe"
    Write-Host "Installer size: $($setupInfo.Length) bytes"
    Write-Host "Installer SHA-256: $setupHash"
    Write-Host "Signing status: $signingStatus"
    Write-Host "Inno Setup: $($inno.Version) ($($inno.Path))"
}
finally {
    Pop-Location
}
