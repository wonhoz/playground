param([string]$Out = "$PSScriptRoot\Resources\app.ico")

Add-Type -AssemblyName System.Drawing

function New-SpecBitmap([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g   = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode   = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
    $g.Clear([System.Drawing.Color]::Transparent)

    $s = $size / 32.0

    # 배경 둥근 사각형
    $bgColor  = [System.Drawing.Color]::FromArgb(255, 18, 18, 30)
    $bgBrush  = New-Object System.Drawing.SolidBrush($bgColor)
    $bgRect   = New-Object System.Drawing.RectangleF(0, 0, $size, $size)
    $g.FillRectangle($bgBrush, $bgRect)

    # 모니터 외형 (파란색 테두리)
    $monColor = [System.Drawing.Color]::FromArgb(255, 50, 128, 255)
    $monPen   = New-Object System.Drawing.Pen($monColor, [float]($s * 1.8))
    # 모니터 본체
    $monX = [float]($s * 3)
    $monY = [float]($s * 4)
    $monW = [float]($s * 26)
    $monH = [float]($s * 18)
    $g.DrawRectangle($monPen, $monX, $monY, $monW, $monH)

    # 모니터 스탠드
    $standBrush = New-Object System.Drawing.SolidBrush($monColor)
    $g.FillRectangle($standBrush, [float]($s*13), [float]($s*22), [float]($s*6), [float]($s*3))
    $g.FillRectangle($standBrush, [float]($s*10), [float]($s*25), [float]($s*12), [float]($s*2))

    # 화면 내부 배경
    $screenColor = [System.Drawing.Color]::FromArgb(255, 26, 26, 42)
    $screenBrush = New-Object System.Drawing.SolidBrush($screenColor)
    $g.FillRectangle($screenBrush, [float]($s*5), [float]($s*6), [float]($s*22), [float]($s*14))

    # 스펙 라인들 (녹색)
    $lineColor  = [System.Drawing.Color]::FromArgb(255, 80, 220, 120)
    $dotColor   = [System.Drawing.Color]::FromArgb(255, 50, 128, 255)
    $dim2Color  = [System.Drawing.Color]::FromArgb(160, 80, 220, 120)
    $lineBrush  = New-Object System.Drawing.SolidBrush($lineColor)
    $dim2Brush  = New-Object System.Drawing.SolidBrush($dim2Color)
    $dotBrush   = New-Object System.Drawing.SolidBrush($dotColor)

    # 라인 1 (긴)
    $dotR = [float]($s * 1.5)
    $g.FillEllipse($dotBrush,  [float]($s*6),   [float]($s*7.5), $dotR*2, $dotR*2)
    $g.FillRectangle($lineBrush, [float]($s*9), [float]($s*8), [float]($s*13), [float]($s*1))

    # 라인 2 (중간)
    $g.FillEllipse($dotBrush,  [float]($s*6),   [float]($s*11), $dotR*2, $dotR*2)
    $g.FillRectangle($lineBrush, [float]($s*9), [float]($s*11.5), [float]($s*9), [float]($s*1))

    # 라인 3 (짧음)
    $g.FillEllipse($dotBrush,  [float]($s*6),   [float]($s*14.5), $dotR*2, $dotR*2)
    $g.FillRectangle($dim2Brush, [float]($s*9), [float]($s*15), [float]($s*6), [float]($s*1))

    $g.Dispose()
    return $bmp
}

$sizes   = @(16, 32, 48, 256)
$bitmaps = @{}
foreach ($sz in $sizes) { $bitmaps[$sz] = New-SpecBitmap $sz }

$ms = New-Object System.IO.MemoryStream
$ms.Write([byte[]](0,0, 1,0, $sizes.Count,0), 0, 6)

$pngStreams = @{}
foreach ($sz in $sizes) {
    $ps = New-Object System.IO.MemoryStream
    $bitmaps[$sz].Save($ps, [System.Drawing.Imaging.ImageFormat]::Png)
    $pngStreams[$sz] = $ps
}

$dataOffset    = 6 + 16 * $sizes.Count
$currentOffset = $dataOffset
foreach ($sz in $sizes) {
    $w = if ($sz -ge 256) { 0 } else { $sz }
    $h = if ($sz -ge 256) { 0 } else { $sz }
    $dataSize = [int]$pngStreams[$sz].Length
    $ms.WriteByte($w); $ms.WriteByte($h); $ms.WriteByte(0); $ms.WriteByte(0)
    $ms.Write([System.BitConverter]::GetBytes([uint16]1),  0, 2)
    $ms.Write([System.BitConverter]::GetBytes([uint16]32), 0, 2)
    $ms.Write([System.BitConverter]::GetBytes([uint32]$dataSize),      0, 4)
    $ms.Write([System.BitConverter]::GetBytes([uint32]$currentOffset), 0, 4)
    $currentOffset += $dataSize
}
foreach ($sz in $sizes) { $pngStreams[$sz].Position = 0; $pngStreams[$sz].CopyTo($ms) }

$dir = Split-Path $Out -Parent
if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir | Out-Null }
[System.IO.File]::WriteAllBytes($Out, $ms.ToArray())
Write-Host "아이콘 생성 완료: $Out"
