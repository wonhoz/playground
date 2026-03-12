# Ctx.Menu icon creation script
param([string]$Out = "$PSScriptRoot\Resources\app.ico")

Add-Type -AssemblyName System.Drawing

function New-CtxMenuBitmap([int]$sz) {
    $bmp = New-Object System.Drawing.Bitmap($sz, $sz)
    $g   = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.Clear([System.Drawing.Color]::Transparent)

    $scale = $sz / 32.0

    $panelW = [int](22 * $scale)
    $panelH = [int](26 * $scale)
    $panelX = [int](8  * $scale)
    $panelY = [int](4  * $scale)
    $radius = [int](3  * $scale)

    $bgBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 28, 32, 52))
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddArc($panelX, $panelY, $radius*2, $radius*2, 180, 90)
    $path.AddArc($panelX+$panelW-$radius*2, $panelY, $radius*2, $radius*2, 270, 90)
    $path.AddArc($panelX+$panelW-$radius*2, $panelY+$panelH-$radius*2, $radius*2, $radius*2, 0, 90)
    $path.AddArc($panelX, $panelY+$panelH-$radius*2, $radius*2, $radius*2, 90, 90)
    $path.CloseAllFigures()
    $g.FillPath($bgBrush, $path)

    $borderPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(180, 74, 158, 255), [float](1.2*$scale))
    $g.DrawPath($borderPen, $path)

    $lineColors = @(
        [System.Drawing.Color]::FromArgb(255, 74, 158, 255),
        [System.Drawing.Color]::FromArgb(200, 180, 196, 240),
        [System.Drawing.Color]::FromArgb(160, 130, 146, 190),
        [System.Drawing.Color]::FromArgb(200, 180, 196, 240)
    )
    $lineY = [int](8 * $scale)
    $lineH = [int](4 * $scale)
    $lineX = [int](12 * $scale)
    $lineW = [int](14 * $scale)

    for ($i = 0; $i -lt 4; $i++) {
        $lc = $lineColors[$i]
        $lb = New-Object System.Drawing.SolidBrush($lc)

        if ($i -eq 0) {
            $selBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(80, 42, 58, 90))
            $selPath = New-Object System.Drawing.Drawing2D.GraphicsPath
            $sr = [int](2 * $scale)
            $sx = [int](9 * $scale)
            $sw = [int](20 * $scale)
            $sy = $lineY - [int](1 * $scale)
            $sh = $lineH + [int](2 * $scale)
            $selPath.AddArc($sx, $sy, $sr*2, $sr*2, 180, 90)
            $selPath.AddArc($sx+$sw-$sr*2, $sy, $sr*2, $sr*2, 270, 90)
            $selPath.AddArc($sx+$sw-$sr*2, $sy+$sh-$sr*2, $sr*2, $sr*2, 0, 90)
            $selPath.AddArc($sx, $sy+$sh-$sr*2, $sr*2, $sr*2, 90, 90)
            $selPath.CloseAllFigures()
            $g.FillPath($selBrush, $selPath)
        }

        $dotBrush = New-Object System.Drawing.SolidBrush($lc)
        $dotSz = [int](2.5 * $scale)
        $g.FillEllipse($dotBrush, [int]($lineX - 4*$scale), $lineY + [int](0.75*$scale), $dotSz, $dotSz)

        $rectH = [int](2.2 * $scale)
        $rectY = $lineY + [int](1 * $scale)
        $g.FillRectangle($lb, $lineX, $rectY, $lineW, $rectH)

        $lineY += [int](5.5 * $scale)
    }

    $sepY  = [int](18.5 * $scale)
    $sepPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(100, 60, 76, 120), [float](0.8*$scale))
    $g.DrawLine($sepPen, [int](10*$scale), $sepY, [int](28*$scale), $sepY)

    if ($sz -ge 32) {
        $arrowBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(240, 255, 255, 255))
        $arrowPts = @(
            [System.Drawing.PointF]::new([float](2*$scale), [float](2*$scale)),
            [System.Drawing.PointF]::new([float](2*$scale), [float](12*$scale)),
            [System.Drawing.PointF]::new([float](5*$scale), [float](9.5*$scale)),
            [System.Drawing.PointF]::new([float](7*$scale), [float](14*$scale)),
            [System.Drawing.PointF]::new([float](8.5*$scale), [float](13.5*$scale)),
            [System.Drawing.PointF]::new([float](6.5*$scale), [float](8.5*$scale)),
            [System.Drawing.PointF]::new([float](9*$scale), [float](8.5*$scale))
        )
        $g.FillPolygon($arrowBrush, $arrowPts)
        $arrowPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(180, 0, 0, 0), [float](0.7*$scale))
        $g.DrawPolygon($arrowPen, $arrowPts)
    }

    $g.Dispose()
    return $bmp
}

$sizes   = @(256, 48, 32, 16)
$bitmaps = @{}
foreach ($sz in $sizes) {
    $bitmaps[$sz] = New-CtxMenuBitmap $sz
}

$stream = New-Object System.IO.MemoryStream
$writer = New-Object System.IO.BinaryWriter($stream)

$writer.Write([uint16]0)
$writer.Write([uint16]1)
$writer.Write([uint16]$sizes.Count)

$pngDataList = @()
foreach ($sz in $sizes) {
    $ms = New-Object System.IO.MemoryStream
    $bitmaps[$sz].Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $pngDataList += ,($ms.ToArray())
}

$headerSize = 6 + 16 * $sizes.Count
$offsets = @()
$offset  = $headerSize
foreach ($data in $pngDataList) {
    $offsets += $offset
    $offset  += $data.Length
}

for ($i = 0; $i -lt $sizes.Count; $i++) {
    $szVal = $sizes[$i]
    if ($szVal -ge 256) {
        $writer.Write([byte]0)
        $writer.Write([byte]0)
    } else {
        $writer.Write([byte]$szVal)
        $writer.Write([byte]$szVal)
    }
    $writer.Write([byte]0)
    $writer.Write([byte]0)
    $writer.Write([uint16]1)
    $writer.Write([uint16]32)
    $writer.Write([uint32]$pngDataList[$i].Length)
    $writer.Write([uint32]$offsets[$i])
}

foreach ($data in $pngDataList) {
    $writer.Write($data)
}

$writer.Flush()
$dir = Split-Path $Out -Parent
if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir | Out-Null }
[System.IO.File]::WriteAllBytes($Out, $stream.ToArray())

foreach ($bmp in $bitmaps.Values) { $bmp.Dispose() }
$stream.Dispose()

Write-Host "Icon created: $Out"
