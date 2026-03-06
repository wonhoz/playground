Add-Type -AssemblyName System.Drawing

$sizes = @(256, 48, 32, 16)
$bitmaps = @{}

foreach ($size in $sizes) {
    $bmp = New-Object System.Drawing.Bitmap($size, $size)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.Clear([System.Drawing.Color]::Transparent)

    $cx = $size / 2.0
    $cy = $size / 2.0
    $pad = [int]($size * 0.06)

    # 배경 원 (다크 그린 그라디언트)
    $bgBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        [System.Drawing.PointF]::new(0, 0),
        [System.Drawing.PointF]::new($size, $size),
        [System.Drawing.Color]::FromArgb(255, 10, 30, 15),
        [System.Drawing.Color]::FromArgb(255, 20, 50, 25)
    )
    $g.FillEllipse($bgBrush, $pad, $pad, $size - $pad*2, $size - $pad*2)
    $bgBrush.Dispose()

    if ($size -ge 32) {
        # 컨베이어 벨트 (수평선 3줄)
        $beltY1 = [int]($cy - $size * 0.18)
        $beltY2 = [int]($cy)
        $beltY3 = [int]($cy + $size * 0.18)
        $beltX1 = [int]($cx - $size * 0.35)
        $beltX2 = [int]($cx + $size * 0.35)
        $beltPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255, 80, 200, 100), [float]([Math]::Max(2, $size * 0.04)))
        $g.DrawLine($beltPen, $beltX1, $beltY1, $beltX2, $beltY1)
        $g.DrawLine($beltPen, $beltX1, $beltY2, $beltX2, $beltY2)
        $g.DrawLine($beltPen, $beltX1, $beltY3, $beltX2, $beltY3)
        $beltPen.Dispose()

        # 화살표 (오른쪽)
        $arrowLen = [int]($size * 0.12)
        $arrowX = [int]($cx + $size * 0.1)
        $arrowPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255, 140, 255, 140), [float]([Math]::Max(2, $size * 0.035)))
        $arrowPen.EndCap = [System.Drawing.Drawing2D.LineCap]::ArrowAnchor
        $g.DrawLine($arrowPen, $arrowX, $beltY2, $arrowX + $arrowLen, $beltY2)
        $arrowPen.Dispose()

        # 기어 원 (왼쪽)
        $gearR = [int]($size * 0.14)
        $gearX = [int]($beltX1 - $gearR * 0.3)
        $gearY = [int]($cy - $gearR)
        $gearBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(200, 255, 180, 50))
        $gearPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255, 255, 200, 80), [float]([Math]::Max(2, $size * 0.03)))
        $g.FillEllipse($gearBrush, $gearX, $gearY, $gearR * 2, $gearR * 2)
        $g.DrawEllipse($gearPen, $gearX, $gearY, $gearR * 2, $gearR * 2)
        $gearBrush.Dispose(); $gearPen.Dispose()

        # 아이템 (오른쪽에 컬러 원)
        $itemR = [int]($size * 0.09)
        $itemX = [int]($beltX2 - $itemR * 1.5)
        $itemY = [int]($beltY1 - $itemR)
        $itemBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 80, 150, 255))
        $g.FillEllipse($itemBrush, $itemX, $itemY, $itemR * 2, $itemR * 2)
        $itemBrush.Dispose()

        $itemY2 = [int]($beltY2 - $itemR)
        $itemBrush2 = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 255, 80, 80))
        $g.FillEllipse($itemBrush2, $itemX, $itemY2, $itemR * 2, $itemR * 2)
        $itemBrush2.Dispose()
    } else {
        # 소형: 단순 기어
        $gearR = [int]($size * 0.32)
        $gearBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(220, 80, 200, 100))
        $g.FillEllipse($gearBrush, [int]($cx - $gearR), [int]($cy - $gearR), $gearR*2, $gearR*2)
        $gearBrush.Dispose()
    }

    $g.Dispose()
    $bitmaps[$size] = $bmp
}

$ms = New-Object System.IO.MemoryStream
$writer = New-Object System.IO.BinaryWriter($ms)
$writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$sizes.Count)

$imageData = @()
foreach ($size in $sizes) {
    $imgMs = New-Object System.IO.MemoryStream
    $bitmaps[$size].Save($imgMs, [System.Drawing.Imaging.ImageFormat]::Png)
    $imageData += ,$imgMs.ToArray()
    $imgMs.Dispose()
}

$dataOffset = 6 + ($sizes.Count * 16)
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $size = $sizes[$i]; $data = $imageData[$i]
    $wb = if ($size -eq 256) { 0 } else { $size }
    $writer.Write([byte]$wb); $writer.Write([byte]$wb)
    $writer.Write([byte]0); $writer.Write([byte]0)
    $writer.Write([uint16]1); $writer.Write([uint16]32)
    $writer.Write([uint32]$data.Length)
    $writer.Write([uint32]$dataOffset)
    $dataOffset += $data.Length
}
foreach ($data in $imageData) { $writer.Write($data) }

$writer.Flush()
[System.IO.File]::WriteAllBytes("Resources\app.ico", $ms.ToArray())
$ms.Dispose(); $writer.Dispose()
foreach ($bmp in $bitmaps.Values) { $bmp.Dispose() }
Write-Host "아이콘 생성 완료"
