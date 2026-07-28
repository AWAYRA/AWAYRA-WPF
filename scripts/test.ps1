param(
    [string]$Filter = ""
)

$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
Push-Location $root

try {
    $testProjects = @(
        "tests\Awayra.Core.Tests\Awayra.Core.Tests.csproj",
        "tests\Awayra.App.Tests\Awayra.App.Tests.csproj",
        "tests\Awayra.UiTests\Awayra.UiTests.csproj"
    )

    foreach ($project in $testProjects) {
        if ([string]::IsNullOrWhiteSpace($Filter)) {
            dotnet test $project -c Debug --no-restore
        }
        else {
            dotnet test $project -c Debug --no-restore --filter $Filter
        }

        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    }
}
finally {
    Pop-Location
}
