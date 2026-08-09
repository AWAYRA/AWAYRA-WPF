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

        $version = (Get-Item $path).VersionInfo.ProductVersion
        if ([string]::IsNullOrWhiteSpace($version)) {
            $version = (Get-Item $path).VersionInfo.FileVersion
        }

        if ($version -match '(?i)(beta|preview|rc|nightly|dev)') {
            throw "Rejected non-stable Inno Setup build at $path ($version)."
        }

        return [PSCustomObject]@{
            Path = $path
            Version = if ([string]::IsNullOrWhiteSpace($version)) { "7.x" } else { $version }
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
        $candidate = Get-ChildItem $kitsRoot -Directory -ErrorAction SilentlyContinue |
            Sort-Object Name -Descending |
            ForEach-Object { Join-Path $_.FullName "x64\signtool.exe" } |
            Where-Object { Test-Path $_ } |
            Select-Object -First 1

        if ($candidate) { return $candidate }
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

    $arguments = @(
        "sign",
        "/f", $CertPath,
        "/fd", "SHA256",
        "/tr", $TimestampUrl,
        "/td", "SHA256",
        "/v"
    )

    if (-not [string]::IsNullOrWhiteSpace($CertPassword)) {
        $arguments += @("/p", $CertPassword)
    }

    $arguments += $FilePath
    & $SignTool @arguments
    if ($LASTEXITCODE -ne 0) {
        throw "signtool failed for $FilePath (exit $LASTEXITCODE)."
    }

    $signature = Get-AuthenticodeSignature -FilePath $FilePath
    if ($signature.Status -ne "Valid") {
        throw "Authenticode signature verification failed for $FilePath ($($signature.Status))."
    }
}

function Get-AppVersionFromExe {
    param([Parameter(Mandatory = $true)][string]$ExePath)

    $versionInfo = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($ExePath)
    $productVersion = ($versionInfo.ProductVersion -split '\+')[0].Trim()
    if ([string]::IsNullOrWhiteSpace($productVersion)) {
        throw "Product version metadata missing on $ExePath"
    }

    $parts = $productVersion.Split('.')
    while ($parts.Count -lt 4) { $parts += "0" }

    return [PSCustomObject]@{
        Version = ($parts[0..2] -join '.')
        VersionInfo = ($parts[0..3] -join '.')
    }
}

function Get-PeMachineType {
    param([Parameter(Mandatory = $true)][string]$Path)

    $stream = [System.IO.File]::OpenRead($Path)
    try {
        $reader = [System.IO.BinaryReader]::new($stream)
        $stream.Position = 0x3C
        $peOffset = $reader.ReadInt32()
        $stream.Position = $peOffset + 4
        $machine = $reader.ReadUInt16()

        switch ($machine) {
            0x8664 { return "x64" }
            0x014C { return "x86" }
            default { return "unknown-$machine" }
        }
    }
    finally {
        $stream.Dispose()
    }
}

$root = (Get-RepoRoot).Path
$publishDir = Join-Path $root "artifacts\publish\win-x64"
$installerDir = Join-Path $root "artifacts\installer"
$issPath = Join-Path $root "installer\Awayra.iss"
$iconPath = Join-Path $root "src\Awayra.App\Assets\awayra.ico"
$licensePath = Join-Path $root "LICENSE"

Push-Location $root
try {
    Stop-AwayraProcessesUnderRoot -RootPath $root

    foreach ($requiredFile in @($issPath, $iconPath, $licensePath)) {
        if (-not (Test-Path $requiredFile)) {
            throw "Required build file not found: $requiredFile"
        }
    }

    if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }
    if (Test-Path $installerDir) { Remove-Item $installerDir -Recurse -Force }
    New-Item -ItemType Directory -Path $publishDir -Force | Out-Null
    New-Item -ItemType Directory -Path $installerDir -Force | Out-Null

    dotnet publish src\Awayra.App\Awayra.App.csproj `
        -c Release `
        -r win-x64 `
        --self-contained true `
        -p:PublishSingleFile=true `
        -p:PublishTrimmed=false `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:DebugType=embedded `
        -p:DebugSymbols=true `
        -o $publishDir
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed." }

    $publishedExe = Join-Path $publishDir "Awayra.exe"
    if (-not (Test-Path $publishedExe)) {
        throw "Published executable not found: $publishedExe"
    }

    # Symbols are embedded rather than dropped, so a crash report from a released build can still be
    # symbolicated. No separate .pdb ships, which keeps the check below meaningful.
    $forbiddenArtifacts = @(Get-ChildItem $publishDir -Recurse -Include "*.pdb", "*.Tests.dll" -ErrorAction SilentlyContinue)
    if ($forbiddenArtifacts.Count -gt 0) {
        throw "Publish directory contains forbidden artifacts: $($forbiddenArtifacts.FullName -join ', ')"
    }

    Copy-Item $iconPath (Join-Path $publishDir "awayra.ico") -Force

    $version = Get-AppVersionFromExe -ExePath $publishedExe
    $architecture = Get-PeMachineType -Path $publishedExe
    if ($architecture -ne "x64") {
        throw "Published executable architecture is $architecture, expected x64."
    }

    $signingStatus = "UNSIGNED - Windows SmartScreen may show an Unknown Publisher warning."
    $certPath = $env:AWAYRA_SIGN_CERT_PATH
    $certPassword = $env:AWAYRA_SIGN_CERT_PASSWORD
    $timestampUrl = $env:AWAYRA_TIMESTAMP_URL

    if (-not [string]::IsNullOrWhiteSpace($certPath)) {
        if (-not (Test-Path $certPath)) { throw "Signing certificate not found: $certPath" }
        if ([string]::IsNullOrWhiteSpace($timestampUrl)) {
            throw "AWAYRA_TIMESTAMP_URL is required when signing."
        }

        $signTool = Find-SignTool
        if (-not $signTool) { throw "signtool.exe was not found." }

        Invoke-AuthenticodeSign `
            -SignTool $signTool `
            -FilePath $publishedExe `
            -CertPath $certPath `
            -CertPassword $certPassword `
            -TimestampUrl $timestampUrl

        $signingStatus = "SIGNED (Awayra.exe verified)"
    }

    $inno = Find-InnoSetupCompiler
    & $inno.Path $issPath `
        "/DMyAppVersion=$($version.Version)" `
        "/DMyAppVersionInfo=$($version.VersionInfo)" `
        "/DPublishDir=$publishDir"
    if ($LASTEXITCODE -ne 0) { throw "Inno Setup compilation failed." }

    $installerName = "Awayra-Setup-$($version.Version)-x64.exe"
    $installerPath = Join-Path $installerDir $installerName
    if (-not (Test-Path $installerPath)) {
        throw "Expected installer not found: $installerPath"
    }

    if (-not [string]::IsNullOrWhiteSpace($certPath)) {
        $signTool = Find-SignTool
        Invoke-AuthenticodeSign `
            -SignTool $signTool `
            -FilePath $installerPath `
            -CertPath $certPath `
            -CertPassword $certPassword `
            -TimestampUrl $timestampUrl

        $signingStatus = "SIGNED (Awayra.exe and installer verified)"
    }

    $publishHash = (Get-FileHash $publishedExe -Algorithm SHA256).Hash
    $installerHash = (Get-FileHash $installerPath -Algorithm SHA256).Hash
    $publishInfo = Get-Item $publishedExe
    $installerInfo = Get-Item $installerPath
    $gitCommit = (git rev-parse HEAD 2>$null)
    if (-not $gitCommit) { $gitCommit = "unknown" }

    $checksumPath = Join-Path $installerDir "Awayra-Setup-$($version.Version)-x64.sha256.txt"
    @(
        "$installerHash  $installerName"
        "PublishedExeSha256=$publishHash"
    ) | Set-Content -Path $checksumPath -Encoding UTF8

    $buildInfoPath = Join-Path $installerDir "BUILD-INFO.txt"
    @(
        "Product=Awayra"
        "ProductVersion=$($version.Version)"
        "GitCommit=$gitCommit"
        "BuildDateUtc=$((Get-Date).ToUniversalTime().ToString('o'))"
        "DotNetSdk=$(dotnet --version)"
        "InnoSetupVersion=$($inno.Version)"
        "PublishedExeFile=Awayra.exe"
        "PublishedExeSha256=$publishHash"
        "PublishedExeSizeBytes=$($publishInfo.Length)"
        "InstallerFile=$installerName"
        "InstallerSha256=$installerHash"
        "InstallerSizeBytes=$($installerInfo.Length)"
        "SigningStatus=$signingStatus"
        "Architecture=x64"
        "InstallationScope=PerUser"
        "DefaultInstallDirectory=%LocalAppData%\Programs\Awayra"
        "MinimumWindowsVersion=Windows 10 x64"
        "License=GPL-3.0-only"
        "LicensePage=Included"
        "SourceRepository=https://github.com/AWAYRA/AWAYRA-WPF"
    ) | Set-Content -Path $buildInfoPath -Encoding UTF8

    Write-Host ""
    Write-Host "Installer build complete."
    Write-Host "Product version: $($version.Version)"
    Write-Host "Installer: $installerPath"
    Write-Host "Installer size: $($installerInfo.Length) bytes"
    Write-Host "Installer SHA-256: $installerHash"
    Write-Host "Signing status: $signingStatus"
}
finally {
    Pop-Location
}
