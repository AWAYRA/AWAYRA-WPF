param(
    [string]$Filter = ""
)

$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
Push-Location $root

try {
    if ([string]::IsNullOrWhiteSpace($Filter)) {
        dotnet test Awayra.sln -c Debug --no-restore
    }
    else {
        dotnet test Awayra.sln -c Debug --no-restore --filter $Filter
    }
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}
finally {
    Pop-Location
}
