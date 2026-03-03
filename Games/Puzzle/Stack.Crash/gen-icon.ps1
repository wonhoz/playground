# Stack.Crash 아이콘 생성 스크립트
# 무너지는 탑 + 폭발 파편 형태의 아이콘

Add-Type -AssemblyName System.Drawing

$outDir = "$PSScriptRoot\Resources"
if (!(Test-Path $outDir)) { New-Item -ItemType Directory -Path $outDir | Out-Null }

function Make-Bitmap([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap($size, $size)
    $g   = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode     = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit

    # 배경 (다크)
    $g.Clear([System.Drawing.Color]::FromArgb(0xFF, 0x0D, 0x11, 0x17))

    $s = $size

    # 블록 그리기 헬퍼
    function Draw-Block($x, $y, $w, $h, $fillHex, $strokeHex, $angle) {
        $fill   = [System.Drawing.ColorTranslator]::FromHtml($fillHex)
        $stroke = [System.Drawing.ColorTranslator]::FromHtml($strokeHex)
        $fb = New-Object System.Drawing.SolidBrush($fill)
        $sp = New-Object System.Drawing.Pen($stroke, [int]([math]::Max(1, $size * 0.02)))

        $state = $g.Save()
        $g.TranslateTransform($x + $w/2, $y + $h/2)
        $g.RotateTransform($angle)
        $rect = New-Object System.Drawing.RectangleF(-$w/2, -$h/2, $w, $h)
        $g.FillRectangle($fb, $rect)
        $g.DrawRectangle($sp, [System.Drawing.Rectangle]::Round($rect))
        $g.Restore($state)
        $fb.Dispose(); $sp.Dispose()
    }

    $unit = [int]($s * 0.12)
    $cx   = [int]($s * 0.5)
    $base = [int]($s * 0.85)

    # 지면 선
    $gp = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(0xFF, 0x2D, 0x33, 0x3B), [int]([math]::Max(2, $s * 0.04)))
    $g.DrawLine($gp, 0, $base, $s, $base)
    $gp.Dispose()

    # 무너지는 블록들 (기울어진 상태)
    # 바닥 큰 블록 (돌, 안정)
    Draw-Block ($cx - $unit*1.5) ($base - $unit*1.2) ($unit*3) ($unit) "#7A7A8C" "#454552" 0

    # 2층 블록 (나무, 살짝 기울어짐)
    Draw-Block ($cx - $unit) ($base - $unit*2.4) ($unit*2) ($unit) "#C8883A" "#7A4A18" -8

    # 3층 블록 (나무, 많이 기울어짐)
    Draw-Block ($cx - $unit*0.5) ($base - $unit*3.5) ($unit*1.5) ($unit) "#C8883A" "#7A4A18" 18

    # 파편 블록들
    Draw-Block ($cx + [int]($unit*1.8)) ($base - $unit*1.5) ([int]($unit*0.8)) ([int]($unit*0.8)) "#C8883A" "#7A4A18" 35
    Draw-Block ($cx - [int]($unit*2.2)) ($base - $unit*1.0) ([int]($unit*0.7)) ([int]($unit*0.7)) "#7A7A8C" "#454552" -20

    # 폭발 효과 (오른쪽 상단)
    $expX  = [int]($s * 0.72)
    $expY  = [int]($s * 0.20)
    $expR  = [int]($s * 0.14)

    # 폭발 원
    $expBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(0xFF, 0xE8, 0x50, 0x40))
    $g.FillEllipse($expBrush, $expX - $expR, $expY - $expR, $expR*2, $expR*2)
    $expBrush.Dispose()

    # 폭발 광선
    $rayPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(0xFF, 0xFF, 0xCC, 0x00), [int]([math]::Max(2, $s*0.03)))
    $rays = @(0, 45, 90, 135, 180, 225, 270, 315)
    foreach ($deg in $rays) {
        $rad = $deg * [math]::PI / 180
        $rx1 = $expX + [int](($expR + 2) * [math]::Cos($rad))
        $ry1 = $expY + [int](($expR + 2) * [math]::Sin($rad))
        $rx2 = $expX + [int](($expR + [int]($s*0.07)) * [math]::Cos($rad))
        $ry2 = $expY + [int](($expR + [int]($s*0.07)) * [math]::Sin($rad))
        $g.DrawLine($rayPen, $rx1, $ry1, $rx2, $ry2)
    }
    $rayPen.Dispose()

    $g.Dispose()
    return $bmp
}

# ICO 파일 생성 (16, 32, 48, 256)
$sizes   = @(16, 32, 48, 256)
$bitmaps = $sizes | ForEach-Object { Make-Bitmap $_ }

$icoPath = "$outDir\app.ico"
$stream  = [System.IO.File]::OpenWrite($icoPath)
$writer  = New-Object System.IO.BinaryWriter($stream)

$writer.Write([uint16]0)
$writer.Write([uint16]1)
$writer.Write([uint16]$sizes.Count)

$pngStreams = @()
foreach ($bmp in $bitmaps) {
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $pngStreams += $ms
}

$offset = 6 + $sizes.Count * 16
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $sz = $sizes[$i]
    $w  = if ($sz -ge 256) { 0 } else { $sz }
    $h  = if ($sz -ge 256) { 0 } else { $sz }
    $writer.Write([byte]$w)
    $writer.Write([byte]$h)
    $writer.Write([byte]0)
    $writer.Write([byte]0)
    $writer.Write([uint16]1)
    $writer.Write([uint16]32)
    $writer.Write([uint32]$pngStreams[$i].Length)
    $writer.Write([uint32]$offset)
    $offset += $pngStreams[$i].Length
}

foreach ($ms in $pngStreams) {
    $writer.Write($ms.ToArray())
    $ms.Dispose()
}

$writer.Close()
$stream.Close()
foreach ($bmp in $bitmaps) { $bmp.Dispose() }

Write-Host "아이콘 생성 완료: $icoPath" -ForegroundColor Green
