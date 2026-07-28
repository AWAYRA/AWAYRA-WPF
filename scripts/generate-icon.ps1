param(
    [string]$OutputPath = (Join-Path $PSScriptRoot "..\src\Awayra.App\Assets\awayra.ico")
)

$ErrorActionPreference = "Stop"
$generatorDir = Join-Path $PSScriptRoot "IconGenerator"
$resolvedOutput = [System.IO.Path]::GetFullPath($OutputPath)

Push-Location $generatorDir
try {
    dotnet run --project .\IconGenerator.csproj -- $resolvedOutput
    if ($LASTEXITCODE -ne 0) { throw "Icon generation failed." }
}
finally {
    Pop-Location
}
