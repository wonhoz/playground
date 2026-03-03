Add-Type -AssemblyName System.Drawing

function Make-GearBitmap([int]$sz) {
    $bmp = New-Object System.Drawing.Bitmap($sz, $sz)
    $g   = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.Clear([System.Drawing.Color]::FromArgb(26, 26, 46))

    $cx = $sz / 2.0; $cy = $sz / 2.0
    $r  = $sz * 0.36   # 외곽 반지름
    $rI = $sz * 0.28   # 이빨 안쪽
    $rH = $sz * 0.10   # 중앙 구멍

    $n       = 10       # 이빨 수
    $da      = [Math]::PI * 2 / $n
    $halfT   = $da * 0.22
    $halfV   = $da * 0.28

    $colGear = [System.Drawing.Color]::FromArgb(180, 160, 80)   # 황동색
    $colHole = [System.Drawing.Color]::FromArgb(26, 26, 46)
    $colStroke = [System.Drawing.Color]::FromArgb(100, 80, 40)

    # 기어 폴리곤 포인트 계산
    $pts = New-Object System.Collections.Generic.List[System.Drawing.PointF]
    for ($i = 0; $i -lt $n; $i++) {
        $center = $da * $i
        $p0 = @{ X = $cx + $rI * [Math]::Cos($center - $halfV - $halfT); Y = $cy + $rI * [Math]::Sin($center - $halfV - $halfT) }
        $p1 = @{ X = $cx + $r  * [Math]::Cos($center - $halfT);           Y = $cy + $r  * [Math]::Sin($center - $halfT) }
        $p2 = @{ X = $cx + $r  * [Math]::Cos($center + $halfT);           Y = $cy + $r  * [Math]::Sin($center + $halfT) }
        $p3 = @{ X = $cx + $rI * [Math]::Cos($center + $halfT + $halfV); Y = $cy + $rI * [Math]::Sin($center + $halfT + $halfV) }
        foreach ($p in @($p0, $p1, $p2, $p3)) {
            $pts.Add([System.Drawing.PointF]::new([float]$p.X, [float]$p.Y))
        }
    }

    $brush = New-Object System.Drawing.SolidBrush($colGear)
    $pen   = New-Object System.Drawing.Pen($colStroke, [float]([Math]::Max(0.5, $sz * 0.01)))

    $g.FillPolygon($brush, $pts.ToArray())
    $g.DrawPolygon($pen, $pts.ToArray())

    # 중앙 구멍
    $hb = New-Object System.Drawing.SolidBrush($colHole)
    $g.FillEllipse($hb, [float]($cx - $rH), [float]($cy - $rH), [float]($rH * 2), [float]($rH * 2))

    # 중앙 점
    $dot = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(200, 180, 100))
    $ds  = [float]([Math]::Max(1, $sz * 0.05))
    $g.FillEllipse($dot, [float]($cx - $ds/2), [float]($cy - $ds/2), $ds, $ds)

    $brush.Dispose(); $pen.Dispose(); $hb.Dispose(); $dot.Dispose()
    $g.Dispose()
    return $bmp
}

$sizes = @(16, 32, 48, 256)
$bitmaps = @()
foreach ($s in $sizes) { $bitmaps += Make-GearBitmap $s }

$outPath = Join-Path $PSScriptRoot "app.ico"
$stream  = [System.IO.File]::Create($outPath)
$writer  = New-Object System.IO.BinaryWriter($stream)
$count   = $bitmaps.Count
$writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$count)

$pngArrays = @()
foreach ($bmp in $bitmaps) {
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $pngArrays += ,$ms.ToArray(); $ms.Dispose()
}
$offset = 6 + 16 * $count
for ($i = 0; $i -lt $count; $i++) {
    $s = $sizes[$i]; $b = $pngArrays[$i]
    if ($s -ge 256) { $wb = [byte]0 } else { $wb = [byte]$s }
    $writer.Write($wb); $writer.Write($wb)
    $writer.Write([byte]0); $writer.Write([byte]0)
    $writer.Write([uint16]1); $writer.Write([uint16]32)
    $writer.Write([uint32]$b.Length); $writer.Write([uint32]$offset)
    $offset += $b.Length
}
foreach ($b in $pngArrays) { $writer.Write($b) }
$writer.Close(); $stream.Close()
foreach ($bmp in $bitmaps) { $bmp.Dispose() }
Write-Host "app.ico 생성 완료: $outPath"
