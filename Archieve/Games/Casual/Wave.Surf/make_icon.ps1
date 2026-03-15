Add-Type -AssemblyName System.Drawing

function Make-WaveSurfIcon {
    param([int]$sz)
    $bmp = New-Object System.Drawing.Bitmap($sz, $sz)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias

    # 배경 (하늘 → 바다 그라디언트)
    $skyTop  = [System.Drawing.Color]::FromArgb(255, 30, 100, 200)
    $skyBot  = [System.Drawing.Color]::FromArgb(255, 0, 60, 140)
    $brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        [System.Drawing.Rectangle]::new(0, 0, $sz, $sz),
        $skyTop, $skyBot,
        [System.Drawing.Drawing2D.LinearGradientMode]::Vertical)
    $g.FillRectangle($brush, 0, 0, $sz, $sz)
    $brush.Dispose()

    # 파도 (사인 곡선 채우기)
    $waveBase = [int]($sz * 0.6)
    $amp      = [int]($sz * 0.12)
    $pts = New-Object System.Collections.Generic.List[System.Drawing.PointF]
    for ($x = 0; $x -le $sz; $x += [Math]::Max(1, $sz / 32)) {
        $y = $waveBase + $amp * [Math]::Sin($x / $sz * 2.5 * [Math]::PI)
        $pts.Add([System.Drawing.PointF]::new($x, $y))
    }
    $pts.Add([System.Drawing.PointF]::new($sz, $sz))
    $pts.Add([System.Drawing.PointF]::new(0, $sz))
    $waveBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 0, 100, 190))
    $g.FillPolygon($waveBrush, $pts.ToArray())
    $waveBrush.Dispose()

    # 파도 거품 선
    $foamPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(220, 200, 230, 255), [Math]::Max(1, $sz / 32))
    $foamPts = $pts | Select-Object -First ($pts.Count - 2)
    if ($foamPts.Count -ge 2) {
        $g.DrawCurve($foamPen, $foamPts)
    }
    $foamPen.Dispose()

    # 서퍼 실루엣 (파도 꼭대기 부근)
    if ($sz -ge 32) {
        $cx = [int]($sz * 0.45)
        $cyw = $waveBase - $amp * 0 - [int]($sz * 0.01)
        $sw = [Math]::Max(2, $sz / 8)   # 서핑보드 너비
        $sh = [Math]::Max(1, $sz / 28)  # 서핑보드 높이
        # 서핑보드
        $boardBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 255, 220, 80))
        $g.FillEllipse($boardBrush, $cx - $sw, $cyw - $sh, $sw * 2, $sh * 2)
        $boardBrush.Dispose()
        # 서퍼 몸통
        $bodyBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 40, 30, 20))
        $bodyH = [int]($sz * 0.15)
        $bodyW = [Math]::Max(2, $sz / 16)
        $g.FillEllipse($bodyBrush, $cx - $bodyW/2, $cyw - $bodyH - $sh, $bodyW, $bodyH)
        $bodyBrush.Dispose()
    }

    $g.Dispose()
    return $bmp
}

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$icoPath = Join-Path $root "Resources\app.ico"

$sizes = @(256, 48, 32, 16)
$bitmaps = $sizes | ForEach-Object { Make-WaveSurfIcon $_ }

# ICO 파일 수동 작성
$ms = New-Object System.IO.MemoryStream
$bw = New-Object System.IO.BinaryWriter($ms)

$count = $bitmaps.Count
$bw.Write([uint16]0)       # Reserved
$bw.Write([uint16]1)       # Type: ICO
$bw.Write([uint16]$count)  # Count

# 각 이미지의 PNG 데이터를 먼저 생성
$pngDatas = $bitmaps | ForEach-Object {
    $ms2 = New-Object System.IO.MemoryStream
    $_.Save($ms2, [System.Drawing.Imaging.ImageFormat]::Png)
    $ms2.ToArray()
}

$offset = 6 + $count * 16
foreach ($i in 0..($count - 1)) {
    $sz = $sizes[$i]
    $pngLen = $pngDatas[$i].Length
    $bw.Write([byte]$(if ($sz -ge 256) { 0 } else { $sz }))  # Width
    $bw.Write([byte]$(if ($sz -ge 256) { 0 } else { $sz }))  # Height
    $bw.Write([byte]0)    # Color count
    $bw.Write([byte]0)    # Reserved
    $bw.Write([uint16]1)  # Planes
    $bw.Write([uint16]32) # Bit count
    $bw.Write([uint32]$pngLen)
    $bw.Write([uint32]$offset)
    $offset += $pngLen
}
foreach ($data in $pngDatas) { $bw.Write($data) }

$bw.Flush()
[System.IO.File]::WriteAllBytes($icoPath, $ms.ToArray())

$bitmaps | ForEach-Object { $_.Dispose() }
Write-Host "app.ico 생성 완료: $icoPath"
