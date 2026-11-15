# generate-icons.ps1
# Generates valid Windows ICO files for the win-collect app.
# Run this script once locally or let the CI pipeline run it before building.
#
# Usage:
#   powershell -ExecutionPolicy Bypass -File scripts/generate-icons.ps1

param(
    [string]$OutputDir = "$PSScriptRoot\..\src\win-collect\Resources"
)

Add-Type -AssemblyName System.Drawing

function New-IconFile {
    param(
        [string]$Path,
        [System.Drawing.Color]$Color,
        [int]$Size = 32
    )

    $bmp = New-Object System.Drawing.Bitmap($Size, $Size)
    $g   = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.Clear($Color)

    # Draw a simple "W" shape as the icon mark
    $white = [System.Drawing.Color]::FromArgb(220, 255, 255, 255)
    $pen   = New-Object System.Drawing.Pen($white, [float]([int]($Size / 8)))
    $m = [int]($Size * 0.15)
    $b = [int]($Size * 0.8)
    $pts = @(
        [System.Drawing.Point]::new($m, $m),
        [System.Drawing.Point]::new([int]($Size * 0.3), $b),
        [System.Drawing.Point]::new([int]($Size / 2), [int]($Size * 0.5)),
        [System.Drawing.Point]::new([int]($Size * 0.7), $b),
        [System.Drawing.Point]::new($Size - $m, $m)
    )
    $g.DrawLines($pen, $pts)
    $pen.Dispose()
    $g.Dispose()

    # Convert Bitmap → HICON → save as valid .ico stream
    $hIcon = $bmp.GetHicon()
    $icon  = [System.Drawing.Icon]::FromHandle($hIcon)
    $fs    = [System.IO.File]::Open($Path, [System.IO.FileMode]::Create)
    $icon.Save($fs)
    $fs.Flush()
    $fs.Close()
    $icon.Dispose()
    $bmp.Dispose()

    Write-Host "  Created: $Path"
}

$null = New-Item -ItemType Directory -Force -Path $OutputDir

Write-Host "Generating win-collect icons in: $OutputDir"

# App icon — accent red
New-IconFile `
    -Path (Join-Path $OutputDir "app.ico") `
    -Color ([System.Drawing.Color]::FromArgb(233, 69, 96))

# Tray active — teal (monitoring on)
New-IconFile `
    -Path (Join-Path $OutputDir "tray-active.ico") `
    -Color ([System.Drawing.Color]::FromArgb(0, 176, 155))

# Tray inactive — dark slate (monitoring off)
New-IconFile `
    -Path (Join-Path $OutputDir "tray-inactive.ico") `
    -Color ([System.Drawing.Color]::FromArgb(50, 60, 80))

Write-Host "Done. All icons generated successfully."
