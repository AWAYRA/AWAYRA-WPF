param(
    [string]$Filter = ""
)

$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
Push-Location $root

try {
    $testsRoot = Join-Path $root "tests"
    $testProjects = @()
    if (Test-Path $testsRoot) {
        $testProjects = @(Get-ChildItem -Path $testsRoot -Recurse -Filter "*.csproj" |
            Sort-Object FullName |
            Select-Object -ExpandProperty FullName)
    }

    if ($testProjects.Count -eq 0) {
        throw "No automated test projects were found."
    }

    $resultsRoot = Join-Path $root "artifacts\test-results-local"
    if (Test-Path $resultsRoot) {
        Remove-Item $resultsRoot -Recurse -Force
    }
    New-Item -ItemType Directory -Path $resultsRoot -Force | Out-Null

    foreach ($project in $testProjects) {
        $projectName = [System.IO.Path]::GetFileNameWithoutExtension($project)
        $projectResults = Join-Path $resultsRoot $projectName
        $resultFile = "$projectName.trx"
        New-Item -ItemType Directory -Path $projectResults -Force | Out-Null

        $arguments = @(
            "test", $project,
            "-c", "Debug",
            "--logger", "trx;LogFileName=$resultFile",
            "--results-directory", $projectResults
        )
        if (-not [string]::IsNullOrWhiteSpace($Filter)) {
            $arguments += @("--filter", $Filter)
        }

        & dotnet @arguments
        if ($LASTEXITCODE -ne 0) {
            throw "Tests failed: $project"
        }

        $resultPath = Join-Path $projectResults $resultFile
        if (-not (Test-Path $resultPath)) {
            throw "Test evidence was not created: $resultPath"
        }

        [xml]$trx = Get-Content $resultPath
        $counters = $trx.TestRun.ResultSummary.Counters
        if ($null -eq $counters -or [int]$counters.total -le 0) {
            throw "No tests executed for $projectName."
        }
        if ([int]$counters.failed -ne 0) {
            throw "$projectName recorded $($counters.failed) failed tests."
        }

        Write-Host "$projectName: total=$($counters.total), passed=$($counters.passed), failed=$($counters.failed)"
    }
}
finally {
    Pop-Location
}
