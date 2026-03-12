Add-Type -AssemblyName System.Drawing

$sizes    = @(16, 32, 48, 256)
$iconPath = Join-Path $PSScriptRoot "Resources\app.ico"
$pngMap   = @{}

foreach ($size in $sizes) {
    $bmp = [System.Drawing.Bitmap]::new($size, $size)
    $g   = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.Clear([System.Drawing.Color]::Transparent)
    $s = [float]($size / 256.0)

    # Background circle - dark green
    $col1 = [System.Drawing.Color]::FromArgb(255, 27, 58, 31)
    $br1  = [System.Drawing.SolidBrush]::new($col1)
    $g.FillEllipse($br1, [int](8*$s), [int](8*$s), [int](240*$s), [int](240*$s))
    $br1.Dispose()

    # Border circle - light green
    $col2 = [System.Drawing.Color]::FromArgb(255, 102, 187, 106)
    $br2  = [System.Drawing.SolidBrush]::new($col2)
    $pen  = [System.Drawing.Pen]::new($br2, [float](6*$s))
    $g.DrawEllipse($pen, [int](8*$s), [int](8*$s), [int](240*$s), [int](240*$s))
    $pen.Dispose()
    $br2.Dispose()

    # Handle - brown
    $col3 = [System.Drawing.Color]::FromArgb(255, 121, 85, 72)
    $br3  = [System.Drawing.SolidBrush]::new($col3)
    $g.FillRectangle($br3, [int](115*$s), [int](40*$s), [int](26*$s), [int](120*$s))
    $br3.Dispose()

    # Head - green trapezoid
    $col4  = [System.Drawing.Color]::FromArgb(255, 102, 187, 106)
    $br4   = [System.Drawing.SolidBrush]::new($col4)
    $ptArr = [System.Drawing.PointF[]]::new(4)
    $ptArr[0] = [System.Drawing.PointF]::new([float](60*$s),  [float](155*$s))
    $ptArr[1] = [System.Drawing.PointF]::new([float](196*$s), [float](155*$s))
    $ptArr[2] = [System.Drawing.PointF]::new([float](220*$s), [float](210*$s))
    $ptArr[3] = [System.Drawing.PointF]::new([float](36*$s),  [float](210*$s))
    $g.FillPolygon($br4, $ptArr)
    $br4.Dispose()

    # Teeth
    $col5 = [System.Drawing.Color]::FromArgb(180, 200, 255, 200)
    $br5  = [System.Drawing.SolidBrush]::new($col5)
    for ($i = 0; $i -lt 5; $i++) {
        $tw = [int](12*$s); if ($tw -lt 1) { $tw = 1 }
        $th = [int](26*$s); if ($th -lt 1) { $th = 1 }
        $g.FillRectangle($br5, [int]((68+$i*30)*$s), [int](210*$s), $tw, $th)
    }
    $br5.Dispose()

    # Sparkle
    $col6 = [System.Drawing.Color]::FromArgb(255, 255, 235, 59)
    $br6  = [System.Drawing.SolidBrush]::new($col6)
    $starData = @(188, 72, 10,  210, 52, 7,  200, 90, 5)
    for ($i = 0; $i -lt 9; $i += 3) {
        $cx = $starData[$i]; $cy = $starData[$i+1]; $r = $starData[$i+2]
        $sx = [int](($cx-$r)*$s); $sy = [int](($cy-$r)*$s)
        $sd = [int](2*$r*$s); if ($sd -lt 1) { $sd = 1 }
        $g.FillEllipse($br6, $sx, $sy, $sd, $sd)
    }
    $br6.Dispose()
    $g.Dispose()

    # Encode to PNG bytes
    $pngMs = [System.IO.MemoryStream]::new()
    $bmp.Save($pngMs, [System.Drawing.Imaging.ImageFormat]::Png)
    $pngMap["$size"] = $pngMs.ToArray()
    $pngMs.Dispose()
    $bmp.Dispose()
}

# Build ICO file
$outMs  = [System.IO.MemoryStream]::new()
$bw     = [System.IO.BinaryWriter]::new($outMs)
$count  = $sizes.Count
$hdrSz  = 6 + 16 * $count

$offsets = @{}
$off = $hdrSz
foreach ($sz in $sizes) {
    $offsets["$sz"] = $off
    $off += $pngMap["$sz"].Length
}

$bw.Write([uint16]0)
$bw.Write([uint16]1)
$bw.Write([uint16]$count)

foreach ($sz in $sizes) {
    $w = if ($sz -ge 256) { 0 } else { $sz }
    $h = if ($sz -ge 256) { 0 } else { $sz }
    $bw.Write([byte]$w)
    $bw.Write([byte]$h)
    $bw.Write([byte]0)
    $bw.Write([byte]0)
    $bw.Write([uint16]1)
    $bw.Write([uint16]32)
    $bw.Write([uint32]($pngMap["$sz"].Length))
    $bw.Write([uint32]($offsets["$sz"]))
}

$bw.Flush()

foreach ($sz in $sizes) {
    $data = [byte[]]($pngMap["$sz"])
    $outMs.Write($data, 0, $data.Length)
}

[System.IO.File]::WriteAllBytes($iconPath, $outMs.ToArray())
$bw.Dispose()
$outMs.Dispose()

Write-Host "Icon created: $iconPath"
