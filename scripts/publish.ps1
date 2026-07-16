$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
$publishDir = Join-Path $root "artifacts\publish\win-x64"
Push-Location $root

try {
    dotnet restore
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    dotnet build Awayra.sln -c Release --no-restore
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    dotnet test Awayra.sln -c Release --no-build
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    if (Test-Path $publishDir) {
        Remove-Item $publishDir -Recurse -Force
    }
    New-Item -ItemType Directory -Path $publishDir -Force | Out-Null

    dotnet publish src/Awayra.App/Awayra.App.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=false -o $publishDir
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    $exe = Join-Path $publishDir "Awayra.exe"
    if (-not (Test-Path $exe)) {
        Write-Error "Published executable not found: $exe"
        exit 1
    }

    $info = Get-Item $exe
    $hash = Get-FileHash $exe -Algorithm SHA256
    Write-Host "Published: $exe"
    Write-Host "Size: $($info.Length) bytes"
    Write-Host "Modified: $($info.LastWriteTime)"
    Write-Host "SHA-256: $($hash.Hash)"
}
finally {
    Pop-Location
}
