$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "launch-common.ps1")

function Stop-RepoAwayraProcesses {
    param([string]$RepoRoot)
    Get-Process -Name "Awayra" -ErrorAction SilentlyContinue | ForEach-Object {
        $process = $_
        try {
            $exePath = $process.MainModule.FileName
            if ($exePath -and $exePath.StartsWith($RepoRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
                Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
            }
        }
        catch { }
    }
}

function Fail {
    param([string]$Message)
    Write-Host "REGRESSION VERIFICATION: FAILED"
    Write-Host $Message
    exit 1
}

$repoRoot = (Get-RepoRoot).Path
Push-Location $repoRoot

try {
    Stop-RepoAwayraProcesses -RepoRoot $repoRoot

    dotnet build Awayra.sln -c Debug
    if ($LASTEXITCODE -ne 0) { Fail "Debug build failed." }

    $testsRoot = Join-Path $repoRoot "tests"
    $testProjects = @()
    if (Test-Path $testsRoot) {
        $testProjects = @(Get-ChildItem -Path $testsRoot -Recurse -Filter "*.csproj" |
            Sort-Object FullName |
            Select-Object -ExpandProperty FullName)
    }

    foreach ($project in $testProjects) {
        dotnet test $project -c Debug --no-build
        if ($LASTEXITCODE -ne 0) { Fail "Debug tests failed: $project" }
    }

    if ($testProjects.Count -eq 0) {
        Write-Warning "No automated test projects are currently included; launch verification will continue."
    }

    $exe = Join-Path $repoRoot "src\Awayra.App\bin\Debug\net10.0-windows\Awayra.exe"
    if (-not (Test-Path $exe)) { Fail "Debug executable not found: $exe" }

    Write-LaunchReport $exe
    $proc = Start-Process -FilePath $exe -PassThru
    Start-Sleep -Seconds 3
    if ($proc.HasExited) { Fail "Awayra Debug process exited during startup." }

    $null = Start-Process -FilePath $exe -PassThru
    Start-Sleep -Seconds 2

    try {
        $running = Assert-RunningProcessMatches $exe
        Write-Host "Running PID: $($running.ProcessId)"
    }
    catch {
        Fail $_.Exception.Message
    }

    Stop-RepoAwayraProcesses -RepoRoot $repoRoot
    Write-Host "REGRESSION VERIFICATION: PASSED"
    exit 0
}
finally {
    Pop-Location
}
