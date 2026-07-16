$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
Push-Location $root

try {
    Get-Process Awayra -ErrorAction SilentlyContinue | Stop-Process -Force
    dotnet build src/Awayra.App/Awayra.App.csproj -c Debug
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    $exe = Join-Path $root "src\Awayra.App\bin\Debug\net10.0-windows\Awayra.exe"
    Write-Host "Debug executable: $exe"
    Start-Process -FilePath $exe
}
finally {
    Pop-Location
}
