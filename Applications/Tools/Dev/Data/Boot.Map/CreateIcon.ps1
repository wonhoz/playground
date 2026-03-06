Add-Type -AssemblyName System.Drawing

$sizes = @(256, 48, 32, 16)
$bitmaps = @{}

foreach ($size in $sizes) {
    $bmp = New-Object System.Drawing.Bitmap($size, $size)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.Clear([System.Drawing.Color]::Transparent)

    $pad = [int]($size * 0.05)
    $cx = $size / 2.0
    $cy = $size / 2.0

    # 배경 원 (진한 다크 블루)
    $bgBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        [System.Drawing.PointF]::new(0, 0),
        [System.Drawing.PointF]::new($size, $size),
        [System.Drawing.Color]::FromArgb(255, 10, 20, 40),
        [System.Drawing.Color]::FromArgb(255, 20, 40, 70)
    )
    $g.FillEllipse($bgBrush, $pad, $pad, $size - $pad * 2, $size - $pad * 2)
    $bgBrush.Dispose()

    # 시계 원형 테두리
    $clockR = [int]($size * 0.36)
    $clockX = [int]($cx - $clockR)
    $clockY = [int]($cy - $clockR)
    $clockPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255, 80, 160, 255), [float]([Math]::Max(2, $size * 0.04)))
    $g.DrawEllipse($clockPen, $clockX, $clockY, $clockR * 2, $clockR * 2)
    $clockPen.Dispose()

    # 시계 시침 (짧음, 밝은 흰색)
    if ($size -ge 32) {
        $hourLen = [float]($clockR * 0.5)
        $hourAngle = -60.0 * [Math]::PI / 180.0  # 10시 방향
        $hx2 = [float]($cx + $hourLen * [Math]::Sin($hourAngle))
        $hy2 = [float]($cy - $hourLen * [Math]::Cos($hourAngle))
        $hourPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255, 255, 255, 255), [float]([Math]::Max(2, $size * 0.05)))
        $hourPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
        $hourPen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
        $g.DrawLine($hourPen, [float]$cx, [float]$cy, $hx2, $hy2)
        $hourPen.Dispose()
    }

    # 시계 분침 (길음, 청록색)
    $minLen = [float]($clockR * 0.72)
    $minAngle = 60.0 * [Math]::PI / 180.0  # 2시 방향
    $mx2 = [float]($cx + $minLen * [Math]::Sin($minAngle))
    $my2 = [float]($cy - $minLen * [Math]::Cos($minAngle))
    $minPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255, 80, 200, 255), [float]([Math]::Max(2, $size * 0.04)))
    $minPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $minPen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $g.DrawLine($minPen, [float]$cx, [float]$cy, $mx2, $my2)
    $minPen.Dispose()

    # 타임라인 바 (오른쪽 아래, 주황~청록 그라디언트)
    if ($size -ge 32) {
        $barY = [int]($cy + $clockR * 0.7)
        $barX = [int]($cx - $clockR * 0.6)
        $barW = [int]($clockR * 1.2)
        $barH = [int]([Math]::Max(3, $size * 0.06))

        # 배경 바
        $barBgBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(80, 80, 120, 160))
        $g.FillRectangle($barBgBrush, $barX, $barY, $barW, $barH)
        $barBgBrush.Dispose()

        # 진행 바 (주황→청록 그라디언트)
        $barFillW = [int]($barW * 0.65)
        $barFillBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
            [System.Drawing.PointF]::new($barX, $barY),
            [System.Drawing.PointF]::new($barX + $barFillW, $barY),
            [System.Drawing.Color]::FromArgb(255, 255, 140, 0),
            [System.Drawing.Color]::FromArgb(255, 0, 180, 255)
        )
        $g.FillRectangle($barFillBrush, $barX, $barY, $barFillW, $barH)
        $barFillBrush.Dispose()
    }

    # 중심점
    $dotR = [int]([Math]::Max(2, $size * 0.04))
    $dotBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 255, 255, 255))
    $g.FillEllipse($dotBrush, [int]($cx - $dotR), [int]($cy - $dotR), $dotR * 2, $dotR * 2)
    $dotBrush.Dispose()

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
