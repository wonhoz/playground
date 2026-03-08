param([string]$Out = "$PSScriptRoot\Resources\app.ico")

Add-Type -AssemblyName System.Drawing

function New-BatteryBitmap([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g   = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode   = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.Clear([System.Drawing.Color]::Transparent)

    $s = $size / 32.0

    # 배터리 외형 (파란색)
    $bodyColor = [System.Drawing.Color]::FromArgb(255, 50, 160, 255)
    $pen  = New-Object System.Drawing.Pen($bodyColor, [float]($s * 2))
    $g.DrawRectangle($pen, [float]($s*2), [float]($s*4), [float]($s*22), [float]($s*22))

    # 배터리 터미널
    $sb = New-Object System.Drawing.SolidBrush($bodyColor)
    $g.FillRectangle($sb, [float]($s*24), [float]($s*11), [float]($s*5), [float]($s*8))

    # 충전 레벨 (80% 기본)
    $fillColor = [System.Drawing.Color]::FromArgb(200, 50, 160, 255)
    $fillBrush = New-Object System.Drawing.SolidBrush($fillColor)
    $fillH = [int]($s * 18 * 0.8)
    $fillY = [int]($s*6 + ($s*18 - $fillH))
    $g.FillRectangle($fillBrush, [float]($s*4), [float]$fillY, [float]($s*18), [float]$fillH)

    # 번개 심볼 (충전 중 표시)
    $ltColor = [System.Drawing.Color]::FromArgb(255, 255, 230, 80)
    $ltBrush = New-Object System.Drawing.SolidBrush($ltColor)
    $pts = @(
        [System.Drawing.PointF]::new([float]($s*14), [float]($s*7)),
        [System.Drawing.PointF]::new([float]($s*10), [float]($s*16)),
        [System.Drawing.PointF]::new([float]($s*14), [float]($s*16)),
        [System.Drawing.PointF]::new([float]($s*11), [float]($s*25)),
        [System.Drawing.PointF]::new([float]($s*18), [float]($s*14)),
        [System.Drawing.PointF]::new([float]($s*14), [float]($s*14))
    )
    $g.FillPolygon($ltBrush, $pts)

    $g.Dispose()
    return $bmp
}

# ICO 파일 수동 생성
$sizes = @(16, 32, 48, 256)
$bitmaps = @{}
foreach ($sz in $sizes) {
    $bitmaps[$sz] = New-BatteryBitmap $sz
}

# ICO 바이너리 직접 생성
$ms = New-Object System.IO.MemoryStream

# ICO 헤더 (6 bytes)
$ms.Write([byte[]](0,0, 1,0, $sizes.Count,0), 0, 6)

# 각 이미지를 PNG로 메모리에 저장
$pngStreams = @{}
foreach ($sz in $sizes) {
    $ps = New-Object System.IO.MemoryStream
    $bitmaps[$sz].Save($ps, [System.Drawing.Imaging.ImageFormat]::Png)
    $pngStreams[$sz] = $ps
}

# ICONDIRENTRY 계산 (오프셋 = 6 + 16*count)
$dataOffset = 6 + 16 * $sizes.Count

# ICONDIRENTRY (16 bytes each)
$offsets = @{}
$currentOffset = $dataOffset
foreach ($sz in $sizes) {
    $w  = if ($sz -ge 256) { 0 } else { $sz }
    $h  = if ($sz -ge 256) { 0 } else { $sz }
    $dataSize = [int]$pngStreams[$sz].Length
    $ms.WriteByte($w)
    $ms.WriteByte($h)
    $ms.WriteByte(0)   # color count
    $ms.WriteByte(0)   # reserved
    $ms.Write([System.BitConverter]::GetBytes([uint16]1), 0, 2)   # planes
    $ms.Write([System.BitConverter]::GetBytes([uint16]32), 0, 2)  # bit count
    $ms.Write([System.BitConverter]::GetBytes([uint32]$dataSize), 0, 4)
    $ms.Write([System.BitConverter]::GetBytes([uint32]$currentOffset), 0, 4)
    $offsets[$sz] = $currentOffset
    $currentOffset += $dataSize
}

# PNG 데이터
foreach ($sz in $sizes) {
    $pngStreams[$sz].Position = 0
    $pngStreams[$sz].CopyTo($ms)
}

# 파일 저장
$bytes = $ms.ToArray()
[System.IO.File]::WriteAllBytes($Out, $bytes)

Write-Host "아이콘 생성 완료: $Out ($($bytes.Length) bytes)"
