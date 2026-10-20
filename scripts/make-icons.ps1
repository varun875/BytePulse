# Generates the application's icons from one drawing, so every size in the project
# comes from the same artwork and the whole set can be regenerated.
#
# The mark is the thing the application is: a segmented ring, the same shape as the
# chart on the scan page, in the colours that chart uses.
#
#   powershell -ExecutionPolicy Bypass -File .\scripts\make-icons.ps1

[CmdletBinding()]
param(
    [string]$OutputDirectory
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrEmpty($OutputDirectory)) {
    $OutputDirectory = Join-Path (Split-Path -Parent $PSScriptRoot) "src/BytePulse.App/Assets"
}

Add-Type -AssemblyName System.Drawing

$Background = [System.Drawing.Color]::FromArgb(255, 0x17, 0x17, 0x1B)
$Segments = @(
    @{ Start = 0;   Sweep = 100; Color = [System.Drawing.Color]::FromArgb(255, 0x4C, 0x8B, 0xF5) },
    @{ Start = 120; Sweep = 100; Color = [System.Drawing.Color]::FromArgb(255, 0x4F, 0xC1, 0x8C) },
    @{ Start = 240; Sweep = 100; Color = [System.Drawing.Color]::FromArgb(255, 0xE9, 0xB4, 0x4C) }
)

function New-Canvas {
    param([int]$Width, [int]$Height)

    $bitmap = [System.Drawing.Bitmap]::new(
        $Width,
        $Height,
        [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)

    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $graphics.Clear([System.Drawing.Color]::Transparent)

    return @{ Bitmap = $bitmap; Graphics = $graphics }
}

function Add-RoundedBackground {
    param($Graphics, [double]$Width, [double]$Height)

    [double]$radius = [Math]::Min($Width, $Height) * 0.22
    [double]$d = $radius * 2

    $path = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $path.AddArc(0, 0, $d, $d, 180, 90)
    $path.AddArc($Width - $d, 0, $d, $d, 270, 90)
    $path.AddArc($Width - $d, $Height - $d, $d, $d, 0, 90)
    $path.AddArc(0, $Height - $d, $d, $d, 90, 90)
    $path.CloseFigure()

    $brush = [System.Drawing.SolidBrush]::new($Background)
    $Graphics.FillPath($brush, $path)
    $brush.Dispose()
    $path.Dispose()
}

function Add-Ring {
    param($Graphics, [double]$Left, [double]$Top, [double]$Size)

    [double]$inset = $Size * 0.23
    [double]$diameter = $Size - ($inset * 2)
    [double]$x = $Left + $inset
    [double]$y = $Top + $inset
    [double]$width = [Math]::Max(1.5, $Size * 0.115)

    $rect = [System.Drawing.RectangleF]::new($x, $y, $diameter, $diameter)

    foreach ($segment in $Segments) {
        $pen = [System.Drawing.Pen]::new($segment.Color, $width)
        $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Flat
        $pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Flat
        $Graphics.DrawArc($pen, $rect, $segment.Start, $segment.Sweep)
        $pen.Dispose()
    }
}

function Save-Png {
    param($Bitmap, [string]$Path)

    $directory = Split-Path -Parent $Path
    if (-not (Test-Path $directory)) {
        New-Item -ItemType Directory -Path $directory | Out-Null
    }

    $Bitmap.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
}

function New-IconBitmap {
    param([int]$Size, [bool]$Plated)

    $canvas = New-Canvas -Width $Size -Height $Size

    if ($Plated) {
        Add-RoundedBackground -Graphics $canvas.Graphics -Width $Size -Height $Size
    }

    Add-Ring -Graphics $canvas.Graphics -Left 0 -Top 0 -Size $Size
    $canvas.Graphics.Dispose()

    return $canvas.Bitmap
}

function New-BannerBitmap {
    param([int]$Width, [int]$Height)

    $canvas = New-Canvas -Width $Width -Height $Height

    $brush = [System.Drawing.SolidBrush]::new($Background)
    $canvas.Graphics.FillRectangle($brush, 0, 0, $Width, $Height)
    $brush.Dispose()

    [double]$size = [Math]::Min($Height * 0.72, $Width * 0.45)
    [double]$left = ($Width - $size) / 2
    [double]$top = ($Height - $size) / 2

    Add-Ring -Graphics $canvas.Graphics -Left $left -Top $top -Size $size
    $canvas.Graphics.Dispose()

    return $canvas.Bitmap
}

function Save-Ico {
    param([string]$Path, [int[]]$Sizes)

    $images = New-Object System.Collections.ArrayList

    foreach ($size in $Sizes) {
        $bitmap = New-IconBitmap -Size $size -Plated $true
        $stream = [System.IO.MemoryStream]::new()
        $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
        [void]$images.Add(@{ Bytes = $stream.ToArray(); Size = $size })
        $stream.Dispose()
        $bitmap.Dispose()
    }

    $file = [System.IO.MemoryStream]::new()
    $writer = [System.IO.BinaryWriter]::new($file)

    $writer.Write([uint16]0)                 # reserved
    $writer.Write([uint16]1)                 # type: icon
    $writer.Write([uint16]$images.Count)

    $offset = 6 + (16 * $images.Count)

    foreach ($image in $images) {
        [int]$dimension = $image.Size
        if ($dimension -ge 256) {
            $dimension = 0
        }

        $writer.Write([byte]$dimension)      # width
        $writer.Write([byte]$dimension)      # height
        $writer.Write([byte]0)               # palette
        $writer.Write([byte]0)               # reserved
        $writer.Write([uint16]1)             # colour planes
        $writer.Write([uint16]32)            # bits per pixel
        $writer.Write([uint32]$image.Bytes.Length)
        $writer.Write([uint32]$offset)

        $offset = $offset + $image.Bytes.Length
    }

    foreach ($image in $images) {
        $writer.Write($image.Bytes)
    }

    $writer.Flush()
    [System.IO.File]::WriteAllBytes($Path, $file.ToArray())

    $writer.Dispose()
    $file.Dispose()
}

if (-not (Test-Path $OutputDirectory)) {
    New-Item -ItemType Directory -Path $OutputDirectory | Out-Null
}

$plated = @(
    @{ Name = "Square44x44Logo.scale-200.png"; Size = 88 },
    @{ Name = "Square150x150Logo.scale-200.png"; Size = 300 },
    @{ Name = "StoreLogo.png"; Size = 50 },
    @{ Name = "LockScreenLogo.scale-200.png"; Size = 48 }
)

foreach ($item in $plated) {
    $bitmap = New-IconBitmap -Size $item.Size -Plated $true
    Save-Png -Bitmap $bitmap -Path (Join-Path $OutputDirectory $item.Name)
    $bitmap.Dispose()
}

# The taskbar variants sit on whatever the shell draws behind them, so they carry no
# background of their own.
$unplated = @(
    @{ Name = "Square44x44Logo.targetsize-24_altform-unplated.png"; Size = 24 },
    @{ Name = "Square44x44Logo.targetsize-48_altform-lightunplated.png"; Size = 48 }
)

foreach ($item in $unplated) {
    $bitmap = New-IconBitmap -Size $item.Size -Plated $false
    Save-Png -Bitmap $bitmap -Path (Join-Path $OutputDirectory $item.Name)
    $bitmap.Dispose()
}

foreach ($item in @(
    @{ Name = "SplashScreen.scale-200.png"; Width = 620; Height = 300 },
    @{ Name = "Wide310x150Logo.scale-200.png"; Width = 620; Height = 300 }
)) {
    $bitmap = New-BannerBitmap -Width $item.Width -Height $item.Height
    Save-Png -Bitmap $bitmap -Path (Join-Path $OutputDirectory $item.Name)
    $bitmap.Dispose()
}

Save-Ico -Path (Join-Path $OutputDirectory "AppIcon.ico") -Sizes @(16, 20, 24, 32, 40, 48, 64, 128, 256)

Write-Host "Wrote icons to $OutputDirectory"
Get-ChildItem $OutputDirectory | Select-Object Name, Length | Format-Table -AutoSize
