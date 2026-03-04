Add-Type -AssemblyName System.Drawing

function New-MarkViewIcon {
    param([int]$size)
    $bmp = New-Object System.Drawing.Bitmap($size, $size)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $g.Clear([System.Drawing.Color]::FromArgb(255, 30, 30, 46))

    $cardColor = [System.Drawing.Color]::FromArgb(255, 42, 42, 61)
    $margin = [int]($size * 0.08)
    $g.FillRectangle((New-Object System.Drawing.SolidBrush($cardColor)), $margin, $margin, $size - $margin*2, $size - $margin*2)

    $accentColor = [System.Drawing.Color]::FromArgb(255, 137, 180, 250)
    $accent2Color = [System.Drawing.Color]::FromArgb(255, 203, 166, 247)
    $textDimColor = [System.Drawing.Color]::FromArgb(255, 100, 100, 140)

    $lineH = [int]($size * 0.08)
    $x = [int]($size * 0.16)
    $w = $size - $x * 2

    # H1
    $g.FillRectangle((New-Object System.Drawing.SolidBrush($accentColor)), $x, [int]($size * 0.2), $w, $lineH)
    # H2
    $g.FillRectangle((New-Object System.Drawing.SolidBrush($accent2Color)), $x, [int]($size * 0.36), [int]($w * 0.7), [int]($lineH * 0.75))
    # body lines
    for ($i = 0; $i -lt 3; $i++) {
        $lineW = if ($i -eq 2) { [int]($w * 0.6) } else { $w }
        $g.FillRectangle((New-Object System.Drawing.SolidBrush($textDimColor)), $x, [int]($size * 0.52) + $i * [int]($lineH * 1.6), $lineW, [int]($lineH * 0.5))
    }

    $g.Dispose()
    return $bmp
}

$sizes = @(256, 48, 32, 16)
$bitmaps = @{}
foreach ($s in $sizes) { $bitmaps[$s] = New-MarkViewIcon -size $s }

$icoPath = Join-Path $PSScriptRoot 'app.ico'
$stream = [System.IO.File]::OpenWrite($icoPath)
$writer = New-Object System.IO.BinaryWriter($stream)
$writer.Write([uint16]0)
$writer.Write([uint16]1)
$writer.Write([uint16]$sizes.Count)

$pngStreams = @()
foreach ($s in $sizes) {
    $ms = New-Object System.IO.MemoryStream
    $bitmaps[$s].Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $pngStreams += $ms
}

$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $s = $sizes[$i]; $sz = if ($s -eq 256) { 0 } else { $s }
    $writer.Write([byte]$sz); $writer.Write([byte]$sz)
    $writer.Write([byte]0); $writer.Write([byte]0)
    $writer.Write([uint16]1); $writer.Write([uint16]32)
    $writer.Write([uint32]$pngStreams[$i].Length)
    $writer.Write([uint32]$offset)
    $offset += $pngStreams[$i].Length
}
foreach ($ms in $pngStreams) { $writer.Write($ms.ToArray()); $ms.Dispose() }
$writer.Close(); $stream.Close()
foreach ($s in $sizes) { $bitmaps[$s].Dispose() }
Write-Host "Done: $icoPath"
