param(
    [string]$Filter = ""
)

$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
Push-Location $root

try {
    $testProjects = @()
    $testsRoot = Join-Path $root "tests"

    if (Test-Path $testsRoot) {
        $testProjects = @(Get-ChildItem -Path $testsRoot -Recurse -Filter "*.csproj" |
            Sort-Object FullName |
            Select-Object -ExpandProperty FullName)
    }

    if ($testProjects.Count -eq 0) {
        Write-Warning "No automated test projects are currently included. Running a Debug build instead."
        dotnet build Awayra.sln -c Debug
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
        exit 0
    }

    foreach ($project in $testProjects) {
        if ([string]::IsNullOrWhiteSpace($Filter)) {
            dotnet test $project -c Debug
        }
        else {
            dotnet test $project -c Debug --filter $Filter
        }

        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    }
}
finally {
    Pop-Location
}
