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

function Assert-TestEvidence {
    param(
        [Parameter(Mandatory)] [string]$ResultPath,
        [Parameter(Mandatory)] [string]$ProjectName
    )

    if (-not (Test-Path $ResultPath)) {
        Fail "Test result was not created for $ProjectName: $ResultPath"
    }

    [xml]$trx = Get-Content $ResultPath
    $counters = $trx.TestRun.ResultSummary.Counters
    if ($null -eq $counters -or [int]$counters.total -le 0) {
        Fail "No tests were executed for $ProjectName."
    }

    if ([int]$counters.failed -ne 0) {
        Fail "$ProjectName recorded $($counters.failed) failed tests."
    }

    Write-Host "$ProjectName: total=$($counters.total), passed=$($counters.passed), failed=$($counters.failed), skipped=$($counters.notExecuted)"
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

    if ($testProjects.Count -eq 0) {
        Fail "No automated test projects were found."
    }

    $resultsRoot = Join-Path $repoRoot "artifacts\verification-tests"
    if (Test-Path $resultsRoot) {
        Remove-Item $resultsRoot -Recurse -Force
    }
    New-Item -ItemType Directory -Path $resultsRoot -Force | Out-Null

    foreach ($project in $testProjects) {
        $projectName = [System.IO.Path]::GetFileNameWithoutExtension($project)
        $resultDirectory = Join-Path $resultsRoot $projectName
        New-Item -ItemType Directory -Path $resultDirectory -Force | Out-Null
        $resultFile = "$projectName.trx"

        dotnet test $project -c Debug --no-build --logger "trx;LogFileName=$resultFile" --results-directory $resultDirectory
        if ($LASTEXITCODE -ne 0) { Fail "Debug tests failed: $project" }

        Assert-TestEvidence -ResultPath (Join-Path $resultDirectory $resultFile) -ProjectName $projectName
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
    Stop-RepoAwayraProcesses -RepoRoot $repoRoot
    Pop-Location
}
