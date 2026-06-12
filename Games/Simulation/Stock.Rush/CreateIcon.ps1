# Stock.Rush app icon generator
# Bright warm background + red/blue candlesticks (Korean market colors)
# Output: Resources\app.ico (16/32/48/256 PNG-encoded ICO)

Add-Type -AssemblyName System.Drawing

$outPath = Join-Path $PSScriptRoot "Resources\app.ico"
$sizes = @(16, 32, 48, 256)

function Draw-Icon([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap($size, $size)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias

    $s = $size / 256.0

    # Rounded-square bright gradient background
    $rect = New-Object System.Drawing.Rectangle(0, 0, $size, $size)
    $bg = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        $rect,
        [System.Drawing.Color]::FromArgb(255, 255, 214, 79),
        [System.Drawing.Color]::FromArgb(255, 255, 152, 0),
        [System.Drawing.Drawing2D.LinearGradientMode]::ForwardDiagonal)

    $radius = [Math]::Max(2, 52 * $s)
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $radius * 2
    $w = $size - 1
    $path.AddArc(0, 0, $d, $d, 180, 90)
    $path.AddArc($w - $d, 0, $d, $d, 270, 90)
    $path.AddArc($w - $d, $w - $d, $d, $d, 0, 90)
    $path.AddArc(0, $w - $d, $d, $d, 90, 90)
    $path.CloseFigure()
    $g.FillPath($bg, $path)

    $red = [System.Drawing.Color]::FromArgb(255, 226, 50, 62)
    $blue = [System.Drawing.Color]::FromArgb(255, 30, 90, 200)
    $redBrush = New-Object System.Drawing.SolidBrush($red)
    $blueBrush = New-Object System.Drawing.SolidBrush($blue)

    # Candle helper values (256 base): body width 40, wick width 12
    $bw = [Math]::Max(2, 40 * $s)
    $ww = [Math]::Max(1, 12 * $s)

    function Draw-Candle($brush, $cx, $top, $bottom, $wickTop, $wickBottom) {
        $g.FillRectangle($brush, [single]($cx - $ww / 2), [single]$wickTop, [single]$ww, [single]($wickBottom - $wickTop))
        $g.FillRectangle($brush, [single]($cx - $bw / 2), [single]$top, [single]$bw, [single]($bottom - $top))
    }

    # Left: blue (down) candle - mid height
    Draw-Candle $blueBrush (64 * $s) (118 * $s) (190 * $s) (96 * $s) (210 * $s)
    # Center: red (up) candle - rising
    Draw-Candle $redBrush (128 * $s) (84 * $s) (172 * $s) (62 * $s) (194 * $s)
    # Right: red (up) candle - tall, breaking out
    Draw-Candle $redBrush (192 * $s) (46 * $s) (140 * $s) (28 * $s) (162 * $s)

    $g.Dispose()
    $bg.Dispose(); $redBrush.Dispose(); $blueBrush.Dispose(); $path.Dispose()
    return $bmp
}

# Build ICO with PNG-encoded entries
$entries = @()
foreach ($size in $sizes) {
    $bmp = Draw-Icon $size
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $entries += , @{ Size = $size; Data = $ms.ToArray() }
    $ms.Dispose(); $bmp.Dispose()
}

$fs = [System.IO.File]::Create($outPath)
$bwOut = New-Object System.IO.BinaryWriter($fs)

# ICONDIR
$bwOut.Write([UInt16]0)             # reserved
$bwOut.Write([UInt16]1)             # type: icon
$bwOut.Write([UInt16]$entries.Count)

$offset = 6 + 16 * $entries.Count
foreach ($e in $entries) {
    $dim = if ($e.Size -ge 256) { 0 } else { $e.Size }
    $bwOut.Write([Byte]$dim)        # width
    $bwOut.Write([Byte]$dim)        # height
    $bwOut.Write([Byte]0)           # palette
    $bwOut.Write([Byte]0)           # reserved
    $bwOut.Write([UInt16]1)         # planes
    $bwOut.Write([UInt16]32)        # bpp
    $bwOut.Write([UInt32]$e.Data.Length)
    $bwOut.Write([UInt32]$offset)
    $offset += $e.Data.Length
}
foreach ($e in $entries) { $bwOut.Write($e.Data) }

$bwOut.Close(); $fs.Close()
Write-Host "Icon created: $outPath ($((Get-Item $outPath).Length) bytes)"
