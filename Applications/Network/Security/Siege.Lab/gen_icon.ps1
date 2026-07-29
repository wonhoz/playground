Add-Type -AssemblyName System.Drawing

$outPath = "$PSScriptRoot\Resources\app.ico"
$null = New-Item -ItemType Directory -Force -Path "$PSScriptRoot\Resources"
$sizes = @(16, 32, 48, 256)

function New-Point([float]$x, [float]$y, [float]$f) {
    return New-Object System.Drawing.PointF([float]($x * $f), [float]($y * $f))
}

$pngs = @()
foreach ($sz in $sizes) {
    $bmp = New-Object System.Drawing.Bitmap $sz, $sz
    $g   = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode     = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.InterpolationMode  = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.Clear([System.Drawing.Color]::Transparent)

    $f = $sz / 32.0

    # Shield polygon (bright cyan)
    $shield = @(
        (New-Point 16 2  $f), (New-Point 28 7  $f), (New-Point 28 16 $f),
        (New-Point 16 30 $f), (New-Point 4  16 $f), (New-Point 4  7  $f)
    )
    $fill = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(34, 211, 238))
    $g.FillPolygon($fill, [System.Drawing.PointF[]]$shield)
    $fill.Dispose()

    # Shield border (darker cyan)
    $pen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(8, 145, 178), [float](1.6 * $f))
    $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
    $g.DrawPolygon($pen, [System.Drawing.PointF[]]$shield)
    $pen.Dispose()

    # Lightning bolt (dark navy) = stress/siege
    $bolt = @(
        (New-Point 18 7  $f), (New-Point 11 17 $f), (New-Point 15 17 $f),
        (New-Point 13 25 $f), (New-Point 22 13 $f), (New-Point 17 13 $f)
    )
    $boltBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(11, 18, 32))
    $g.FillPolygon($boltBrush, [System.Drawing.PointF[]]$bolt)
    $boltBrush.Dispose()

    $g.Dispose()

    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $pngs += ,$ms.ToArray()
    $ms.Dispose()
    $bmp.Dispose()
}

$fs = [System.IO.File]::Create($outPath)
$bw = New-Object System.IO.BinaryWriter($fs)
$bw.Write([uint16]0)
$bw.Write([uint16]1)
$bw.Write([uint16]$pngs.Count)
$offset = 6 + $pngs.Count * 16
for ($i = 0; $i -lt $pngs.Count; $i++) {
    $szVal = $sizes[$i]
    if ($szVal -ge 256) { $szByte = [byte]0 } else { $szByte = [byte]$szVal }
    $bw.Write($szByte)
    $bw.Write($szByte)
    $bw.Write([byte]0)
    $bw.Write([byte]0)
    $bw.Write([uint16]1)
    $bw.Write([uint16]32)
    $bw.Write([uint32]$pngs[$i].Length)
    $bw.Write([uint32]$offset)
    $offset += $pngs[$i].Length
}
foreach ($png in $pngs) { $bw.Write($png) }
$bw.Close()
$fs.Close()

Write-Host "Siege.Lab icon created: $outPath"
