Add-Type -AssemblyName System.Drawing

$sizes = @(256, 48, 32, 16)
$bitmaps = @{}

foreach ($size in $sizes) {
    $bmp = New-Object System.Drawing.Bitmap($size, $size)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.Clear([System.Drawing.Color]::Transparent)

    $pad = [int]($size * 0.06)
    $w = $size - $pad * 2
    $h = $size - $pad * 2
    $cx = $size / 2.0
    $cy = $size / 2.0
    $r = $w / 2.0 - [int]($size * 0.02)

    # 배경 원 (다크 블루-퍼플)
    $bgBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        [System.Drawing.PointF]::new($pad, $pad),
        [System.Drawing.PointF]::new($size - $pad, $size - $pad),
        [System.Drawing.Color]::FromArgb(255, 30, 40, 80),
        [System.Drawing.Color]::FromArgb(255, 60, 30, 100)
    )
    $g.FillEllipse($bgBrush, $pad, $pad, $w, $h)
    $bgBrush.Dispose()

    # 지구본 위선/경선
    $lw = [float]([Math]::Max(1, $size * 0.015))
    $linePen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(120, 100, 180, 255), $lw)
    $ri = $r - [int]($size * 0.02)

    foreach ($ratio in @(0.3, 0.5, 0.7)) {
        $y = $pad + $h * $ratio
        $dy = $y - $cy
        $halfW = [Math]::Sqrt([Math]::Max(0, $ri * $ri - $dy * $dy))
        if ($halfW -gt 2) {
            $g.DrawEllipse($linePen, [float]($cx - $halfW), [float]($y - $halfW * 0.3), [float]($halfW * 2), [float]($halfW * 0.6))
        }
    }
    $g.DrawEllipse($linePen, [float]($cx - $ri * 0.35), [float]($pad + [int]($size*0.02)), [float]($ri * 0.7), [float]($h - [int]($size*0.04)))
    $g.DrawArc($linePen, [float]($pad + [int]($size*0.02)), [float]($pad + [int]($size*0.02)), [float]($w - [int]($size*0.04)), [float]($h - [int]($size*0.04)), 0, 360)
    $linePen.Dispose()

    # 열쇠 심볼 (황금색)
    $keySize = [int]($size * 0.38)
    $kx = [int]($cx + $ri * 0.15)
    $ky = [int]($cy + $ri * 0.15)
    $keyBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 255, 200, 50))

    $kHeadR = [int]($keySize * 0.28)
    $kHeadX = $kx - $kHeadR
    $kHeadY = $ky - $kHeadR
    $g.FillEllipse($keyBrush, $kHeadX, $kHeadY, $kHeadR * 2, $kHeadR * 2)

    $holeBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 40, 30, 80))
    $holeR = [int]($kHeadR * 0.45)
    $g.FillEllipse($holeBrush, $kHeadX + $kHeadR - $holeR, $kHeadY + $kHeadR - $holeR, $holeR * 2, $holeR * 2)
    $holeBrush.Dispose()

    $shaftLen = [int]($keySize * 0.6)
    $shaftW = [int]($keySize * 0.13)
    $shaftY = $ky - [int]($shaftW / 2)
    $g.FillRectangle($keyBrush, $kx, $shaftY, $shaftLen, $shaftW)

    $toothH = [int]($shaftW * 0.7)
    $toothW = [int]($shaftW * 0.45)
    $g.FillRectangle($keyBrush, $kx + [int]($shaftLen * 0.45), $shaftY + $shaftW, $toothW, $toothH)
    $g.FillRectangle($keyBrush, $kx + [int]($shaftLen * 0.65), $shaftY + $shaftW, $toothW, [int]($toothH * 0.6))
    $keyBrush.Dispose()

    $g.Dispose()
    $bitmaps[$size] = $bmp
}

# ICO 파일 생성
$ms = New-Object System.IO.MemoryStream
$writer = New-Object System.IO.BinaryWriter($ms)

$writer.Write([uint16]0)
$writer.Write([uint16]1)
$writer.Write([uint16]$sizes.Count)

$imageData = @()
foreach ($size in $sizes) {
    $imgMs = New-Object System.IO.MemoryStream
    $bitmaps[$size].Save($imgMs, [System.Drawing.Imaging.ImageFormat]::Png)
    $bytes = $imgMs.ToArray()
    $imageData += ,$bytes
    $imgMs.Dispose()
}

$dataOffset = 6 + ($sizes.Count * 16)
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $size = $sizes[$i]
    $data = $imageData[$i]
    $wb = if ($size -eq 256) { 0 } else { $size }
    $hb = if ($size -eq 256) { 0 } else { $size }
    $writer.Write([byte]$wb)
    $writer.Write([byte]$hb)
    $writer.Write([byte]0)
    $writer.Write([byte]0)
    $writer.Write([uint16]1)
    $writer.Write([uint16]32)
    $writer.Write([uint32]$data.Length)
    $writer.Write([uint32]$dataOffset)
    $dataOffset += $data.Length
}

foreach ($data in $imageData) {
    $writer.Write($data)
}

$writer.Flush()
[System.IO.File]::WriteAllBytes("Resources\app.ico", $ms.ToArray())
$ms.Dispose()
$writer.Dispose()
foreach ($bmp in $bitmaps.Values) { $bmp.Dispose() }
Write-Host "아이콘 생성 완료"
