$ErrorActionPreference = "Stop"
. (Join-Path $PSScriptRoot "launch-common.ps1")
$root = Get-RepoRoot
$publishDir = Join-Path $root.Path "artifacts\publish\win-x64"
Push-Location $root.Path

try {
    Stop-AwayraProcessesUnderRoot -RootPath $root.Path

    dotnet restore Awayra.sln
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    dotnet build Awayra.sln -c Release --no-restore
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    $testsRoot = Join-Path $root.Path "tests"
    $testProjects = @()
    if (Test-Path $testsRoot) {
        $testProjects = @(Get-ChildItem -Path $testsRoot -Recurse -Filter "*.csproj" |
            Sort-Object FullName |
            Select-Object -ExpandProperty FullName)
    }

    foreach ($project in $testProjects) {
        dotnet test $project -c Release --no-build
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    }

    if ($testProjects.Count -eq 0) {
        Write-Warning "No automated test projects are currently included."
    }

    if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }
    New-Item -ItemType Directory -Path $publishDir -Force | Out-Null

    dotnet publish src/Awayra.App/Awayra.App.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=false -o $publishDir
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    $exe = Join-Path $publishDir "Awayra.exe"
    if (-not (Test-Path $exe)) { throw "Published executable not found: $exe" }

    $hash = Get-ExeSha256 $exe
    $info = Get-Item $exe
    $gitCommit = (git rev-parse --short HEAD 2>$null)
    if (-not $gitCommit) { $gitCommit = "unknown" }
    $dirty = if ((git status --porcelain 2>$null)) { "dirty" } else { "clean" }
    $identity = @(
        "GitCommit=$gitCommit",
        "WorkingTreeStatus=$dirty",
        "BuildTimestampUtc=$((Get-Date).ToUniversalTime().ToString('o'))",
        "DotNetSdk=$(dotnet --version)",
        "ExecutableFile=Awayra.exe",
        "ExecutableSha256=$hash",
        "ExecutableSizeBytes=$($info.Length)"
    ) -join [Environment]::NewLine
    Set-Content -Path (Join-Path $publishDir "BUILD-IDENTITY.txt") -Value $identity -Encoding UTF8

    Write-Host "Published: $exe"
    Write-Host "Size: $($info.Length) bytes"
    Write-Host "SHA-256: $hash"
}
finally {
    Pop-Location
}
