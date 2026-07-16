param(
    [string]$OutputPath = (Join-Path $PSScriptRoot "..\src\Awayra.App\Assets\awayra.ico")
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

$directory = Split-Path $OutputPath -Parent
if (-not (Test-Path $directory)) {
    New-Item -ItemType Directory -Path $directory -Force | Out-Null
}

$size = 64
$bitmap = New-Object System.Drawing.Bitmap $size, $size
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
$graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$graphics.Clear([System.Drawing.Color]::FromArgb(0, 0, 0, 0))

$background = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 24, 30, 38))
$graphics.FillEllipse($background, 4, 4, 56, 56)

$accent = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 76, 194, 255))
$font = New-Object System.Drawing.Font("Segoe UI", 28, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)
$graphics.DrawString("A", $font, $accent, 17, 12)

$graphics.Dispose()

$iconHandle = $bitmap.GetHicon()
$icon = [System.Drawing.Icon]::FromHandle($iconHandle)
$stream = New-Object System.IO.FileStream($OutputPath, [System.IO.FileMode]::Create)
$icon.Save($stream)
$stream.Close()
$bitmap.Dispose()

Write-Host "Generated icon: $OutputPath"
