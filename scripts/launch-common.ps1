function Get-RepoRoot {
    Resolve-Path (Join-Path $PSScriptRoot "..")
}

# Stops only the Awayra built from this checkout. Scripts must never terminate the copy the
# developer has installed and is relying on while they work.
function Stop-AwayraProcessesUnderRoot {
    param([Parameter(Mandatory = $true)][string]$RootPath)

    $normalizedRoot = [System.IO.Path]::GetFullPath($RootPath).TrimEnd('\') + '\'
    Get-Process -Name "Awayra" -ErrorAction SilentlyContinue | ForEach-Object {
        try {
            $exePath = $_.MainModule.FileName
            if ($exePath -and $exePath.StartsWith($normalizedRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
                Stop-Process -Id $_.Id -Force -ErrorAction Stop
            }
        }
        catch { }
    }
    Start-Sleep -Milliseconds 500
}

function Get-ExeSha256([string]$Path) {
    if (-not (Test-Path $Path)) { return $null }
    return (Get-FileHash $Path -Algorithm SHA256).Hash
}

function Assert-RunningProcessMatches([string]$ExpectedExePath) {
    $expected = (Resolve-Path $ExpectedExePath).Path
    $expectedHash = Get-ExeSha256 $expected
    $processes = @(Get-Process -Name "Awayra" -ErrorAction SilentlyContinue)
    if ($processes.Count -ne 1) {
        throw "Expected exactly one Awayra process, found $($processes.Count)."
    }

    $actualPath = $processes[0].MainModule.FileName
    $actualHash = Get-ExeSha256 $actualPath
    if (-not $actualPath.Equals($expected, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Running process path mismatch. Expected: $expected Actual: $actualPath"
    }
    if ($actualHash -ne $expectedHash) {
        throw "Running process hash mismatch. Expected: $expectedHash Actual: $actualHash"
    }

    return [PSCustomObject]@{
        ProcessId = $processes[0].Id
        Path = $actualPath
        Sha256 = $actualHash
    }
}

function Write-LaunchReport([string]$ExePath) {
    $hash = Get-ExeSha256 $ExePath
    Write-Host "Executable: $ExePath"
    Write-Host "SHA-256: $hash"
}
