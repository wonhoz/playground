# Drive.Bench 아이콘 생성 — 녹색 드라이브/속도계 모티프
# 출력: Resources/app.ico (16/32/48/256px)

Add-Type -AssemblyName System.Drawing

$outDir = Join-Path $PSScriptRoot "Resources"
if (-not (Test-Path $outDir)) { New-Item -ItemType Directory -Path $outDir | Out-Null }

function Draw-Icon($size) {
    $bmp = New-Object System.Drawing.Bitmap($size, $size)
    $g   = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias

    $bg     = [System.Drawing.Color]::FromArgb(255, 11,  15, 26)
    $green  = [System.Drawing.Color]::FromArgb(255, 34, 197, 94)
    $green2 = [System.Drawing.Color]::FromArgb(255, 16, 163, 74)
    $blue   = [System.Drawing.Color]::FromArgb(255, 96, 165, 250)
    $white  = [System.Drawing.Color]::FromArgb(255, 226, 232, 240)
    $gray   = [System.Drawing.Color]::FromArgb(255, 30,  41, 59)

    $g.Clear($bg)

    $s   = $size
    $pad = [int]($s * 0.08)
    $cx  = $s / 2.0
    $cy  = $s / 2.0

    # 외곽 원 (배경)
    $r1 = $s * 0.44
    $bgBrush = New-Object System.Drawing.SolidBrush($gray)
    $g.FillEllipse($bgBrush, [float]($cx - $r1), [float]($cy - $r1), [float]($r1 * 2), [float]($r1 * 2))
    $bgBrush.Dispose()

    # 속도계 호 (녹색 270도)
    $arcPen = New-Object System.Drawing.Pen($green, [float]($s * 0.07))
    $arcPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $arcPen.EndCap   = [System.Drawing.Drawing2D.LineCap]::Round
    $r2 = $s * 0.38
    $g.DrawArc($arcPen, [float]($cx - $r2), [float]($cy - $r2), [float]($r2 * 2), [float]($r2 * 2), 135, 270)
    $arcPen.Dispose()

    if ($s -ge 32) {
        # 속도 바늘 (오른쪽 상단 = 빠름)
        $angle = 45.0 * [Math]::PI / 180.0  # 45도 = 빠름 위치
        $needleLen = $s * 0.28
        $nx = $cx + $needleLen * [Math]::Cos($angle - [Math]::PI / 2)
        $ny = $cy + $needleLen * [Math]::Sin($angle - [Math]::PI / 2)
        $needlePen = New-Object System.Drawing.Pen($white, [float]($s * 0.05))
        $needlePen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
        $needlePen.EndCap   = [System.Drawing.Drawing2D.LineCap]::Round
        $g.DrawLine($needlePen, [float]$cx, [float]$cy, [float]$nx, [float]$ny)
        $needlePen.Dispose()

        # 중심 원
        $dotR = $s * 0.07
        $dotBrush = New-Object System.Drawing.SolidBrush($green)
        $g.FillEllipse($dotBrush, [float]($cx - $dotR), [float]($cy - $dotR), [float]($dotR * 2), [float]($dotR * 2))
        $dotBrush.Dispose()
    }

    # 하단 HDD/SSD 실린더 (소형 아이콘용)
    if ($s -ge 48) {
        $cylW = $s * 0.30
        $cylH = $s * 0.12
        $cylX = $cx - $cylW / 2
        $cylY = $cy + $s * 0.22
        $cylBrush = New-Object System.Drawing.SolidBrush($blue)
        $g.FillRectangle($cylBrush, [float]$cylX, [float]$cylY, [float]$cylW, [float]$cylH)
        $cylBrush.Dispose()

        $ellBrush = New-Object System.Drawing.SolidBrush($blue)
        $g.FillEllipse($ellBrush, [float]$cylX, [float]($cylY - $cylH * 0.4), [float]$cylW, [float]($cylH * 0.8))
        $ellBrush.Dispose()
    }

    $g.Dispose()
    return $bmp
}

# 멀티사이즈 ICO 생성
$sizes  = @(256, 48, 32, 16)
$bitmaps = @()
foreach ($sz in $sizes) { $bitmaps += Draw-Icon $sz }

$icoPath = Join-Path $outDir "app.ico"
$stream  = New-Object System.IO.FileStream($icoPath, [System.IO.FileMode]::Create)
$writer  = New-Object System.IO.BinaryWriter($stream)

$count = $bitmaps.Count

# ICO 헤더
$writer.Write([uint16]0)      # reserved
$writer.Write([uint16]1)      # type: ICO
$writer.Write([uint16]$count) # image count

# 각 이미지를 PNG로 인코더
$pngStreams = @()
foreach ($bmp in $bitmaps) {
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $pngStreams += $ms
}

# 디렉터리 계산
$dirSize    = 16 * $count
$headerSize = 6
$offset     = $headerSize + $dirSize

foreach ($i in 0..($count - 1)) {
    $sz   = $sizes[$i]
    $data = $pngStreams[$i].ToArray()
    $szVal = if ($sz -ge 256) { 0 } else { $sz }
    $writer.Write([byte]$szVal)   # width
    $writer.Write([byte]$szVal)   # height
    $writer.Write([byte]0)        # color count
    $writer.Write([byte]0)        # reserved
    $writer.Write([uint16]1)      # planes
    $writer.Write([uint16]32)     # bit count
    $writer.Write([uint32]$data.Length)
    $writer.Write([uint32]$offset)
    $offset += $data.Length
}

foreach ($ms in $pngStreams) {
    $writer.Write($ms.ToArray())
    $ms.Dispose()
}

$writer.Dispose()
$stream.Dispose()
foreach ($bmp in $bitmaps) { $bmp.Dispose() }

Write-Host "아이콘 생성 완료: $icoPath"
