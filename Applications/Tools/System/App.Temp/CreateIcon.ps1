param([string]$Out = "$PSScriptRoot\Resources\app.ico")

Add-Type -AssemblyName System.Drawing

function New-SandboxBitmap([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g   = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.Clear([System.Drawing.Color]::Transparent)
    $s = $size / 32.0

    # 박스 외형 (파랑 계열)
    $boxColor = [System.Drawing.Color]::FromArgb(255, 50, 128, 255)
    $pen = New-Object System.Drawing.Pen($boxColor, [float]($s * 1.5))
    $g.DrawRectangle($pen, [float]($s*3), [float]($s*6), [float]($s*22), [float]($s*20))

    # 박스 상단 뚜껑 라인
    $g.DrawLine($pen, [float]($s*3), [float]($s*6), [float]($s*14), [float]($s*2))
    $g.DrawLine($pen, [float]($s*14), [float]($s*2), [float]($s*25), [float]($s*6))

    # 내부 채우기 (반투명)
    $fillBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(60, 50, 128, 255))
    $pts = @(
        [System.Drawing.PointF]::new([float]($s*3), [float]($s*6)),
        [System.Drawing.PointF]::new([float]($s*25), [float]($s*6)),
        [System.Drawing.PointF]::new([float]($s*25), [float]($s*26)),
        [System.Drawing.PointF]::new([float]($s*3), [float]($s*26))
    )
    $g.FillPolygon($fillBrush, $pts)

    # 화살표 (실행 아이콘, 초록)
    $arrowColor = [System.Drawing.Color]::FromArgb(255, 80, 220, 120)
    $arrowBrush = New-Object System.Drawing.SolidBrush($arrowColor)
    $arrowPts = @(
        [System.Drawing.PointF]::new([float]($s*11), [float]($s*12)),
        [System.Drawing.PointF]::new([float]($s*11), [float]($s*20)),
        [System.Drawing.PointF]::new([float]($s*20), [float]($s*16))
    )
    $g.FillPolygon($arrowBrush, $arrowPts)

    $g.Dispose()
    return $bmp
}

$sizes = @(16, 32, 48, 256)
$bitmaps = @{}
foreach ($sz in $sizes) { $bitmaps[$sz] = New-SandboxBitmap $sz }

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
    $ms.WriteByte($w)
    $ms.WriteByte($h)
    $ms.WriteByte(0)
    $ms.WriteByte(0)
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
