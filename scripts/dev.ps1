$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "launch-common.ps1")
$root = Get-RepoRoot
Push-Location $root.Path

try {
    Stop-AwayraProcessesUnderRoot -RootPath $root.Path
    dotnet build src/Awayra.App/Awayra.App.csproj -c Debug
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    $exe = Join-Path $root.Path "src\Awayra.App\bin\Debug\net10.0-windows\Awayra.exe"
    if (-not (Test-Path $exe)) { throw "Debug executable not found: $exe" }

    Write-LaunchReport $exe
    $proc = Start-Process -FilePath $exe -PassThru
    Start-Sleep -Seconds 3
    if ($proc.HasExited) { throw "Awayra Debug process exited during startup." }

    $running = Assert-RunningProcessMatches $exe
    Write-Host "Running PID: $($running.ProcessId)"
    Write-Host "Running path verified: $($running.Path)"
}
finally {
    Pop-Location
}
