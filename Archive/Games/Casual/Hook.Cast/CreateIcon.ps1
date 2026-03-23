param([string]$Out = "$PSScriptRoot\Resources\app.ico")

Add-Type -AssemblyName System.Drawing

function New-FishingBitmap([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g   = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.Clear([System.Drawing.Color]::Transparent)
    $s = $size / 32.0

    # 하늘 배경
    $skyBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 20, 50, 100))
    $g.FillRectangle($skyBrush, 0, 0, $size, [int]($size * 0.5))

    # 수면
    $waterBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 10, 60, 100))
    $g.FillRectangle($waterBrush, 0, [int]($size * 0.5), $size, [int]($size * 0.5))

    # 수면 라인
    $waterLinePen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(200, 60, 160, 220), [float]($s * 1.5))
    $g.DrawLine($waterLinePen, 0, [float]($size * 0.5), $size, [float]($size * 0.5))

    # 낚싯대 (왼쪽 상단에서 대각)
    $rodPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255, 200, 160, 80), [float]($s * 2))
    $g.DrawLine($rodPen, [float]($s*2), [float]($s*4), [float]($s*16), [float]($s*14))

    # 낚싯줄 (곡선 → 수면)
    $linePen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(200, 60, 160, 255), [float]($s * 1))
    $g.DrawBezier($linePen,
        [System.Drawing.PointF]::new([float]($s*16), [float]($s*14)),
        [System.Drawing.PointF]::new([float]($s*20), [float]($s*10)),
        [System.Drawing.PointF]::new([float]($s*24), [float]($s*14)),
        [System.Drawing.PointF]::new([float]($s*23), [float]($s*16)))

    # 물고기 (작은 타원, 물 속)
    $fishBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(200, 60, 200, 120))
    $g.FillEllipse($fishBrush, [float]($s*18), [float]($s*20), [float]($s*8), [float]($s*4))

    $g.Dispose()
    return $bmp
}

$sizes = @(16, 32, 48, 256)
$bitmaps = @{}
foreach ($sz in $sizes) { $bitmaps[$sz] = New-FishingBitmap $sz }

$ms = New-Object System.IO.MemoryStream
$ms.Write([byte[]](0,0, 1,0, $sizes.Count,0), 0, 6)

$pngStreams = @{}
foreach ($sz in $sizes) {
    $ps = New-Object System.IO.MemoryStream
    $bitmaps[$sz].Save($ps, [System.Drawing.Imaging.ImageFormat]::Png)
    $pngStreams[$sz] = $ps
}

$dataOffset = 6 + 16 * $sizes.Count
$currentOffset = $dataOffset

foreach ($sz in $sizes) {
    $w = if ($sz -ge 256) { 0 } else { $sz }
    $h = if ($sz -ge 256) { 0 } else { $sz }
    $dataSize = [int]$pngStreams[$sz].Length
    $ms.WriteByte($w); $ms.WriteByte($h); $ms.WriteByte(0); $ms.WriteByte(0)
    $ms.Write([System.BitConverter]::GetBytes([uint16]1), 0, 2)
    $ms.Write([System.BitConverter]::GetBytes([uint16]32), 0, 2)
    $ms.Write([System.BitConverter]::GetBytes([uint32]$dataSize), 0, 4)
    $ms.Write([System.BitConverter]::GetBytes([uint32]$currentOffset), 0, 4)
    $currentOffset += $dataSize
}

foreach ($sz in $sizes) {
    $pngStreams[$sz].Position = 0
    $pngStreams[$sz].CopyTo($ms)
}

[System.IO.File]::WriteAllBytes($Out, $ms.ToArray())
Write-Host "아이콘 생성 완료: $Out"
