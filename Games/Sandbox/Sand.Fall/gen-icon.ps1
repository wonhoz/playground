# Sand.Fall 아이콘 생성 스크립트
# 떨어지는 모래 입자 + 물 흐름 형태의 아이콘

Add-Type -AssemblyName System.Drawing

$outDir = "$PSScriptRoot\Resources"
if (!(Test-Path $outDir)) { New-Item -ItemType Directory -Path $outDir | Out-Null }

function Make-Bitmap([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap($size, $size)
    $g   = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias

    # 배경 (다크)
    $g.Clear([System.Drawing.Color]::FromArgb(0xFF, 0x0D, 0x11, 0x17))

    $s    = $size
    $unit = [int]($s / 16)

    function Draw-Pixel($px, $py, $pw, $ph, $r, $gv, $b) {
        $br = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(0xFF, $r, $gv, $b))
        $g.FillRectangle($br, $px, $py, $pw, $ph)
        $br.Dispose()
    }

    # 모래 입자 그룹 (상단에서 떨어지는 중)
    $sandColor = @(0xE8, 0xC8, 0x78)
    $pileY = [int]($s * 0.65)

    # 모래 더미 (하단)
    $moundPts = @(
        [System.Drawing.PointF]::new([int]($s*0.08), $s),
        [System.Drawing.PointF]::new([int]($s*0.15), $pileY),
        [System.Drawing.PointF]::new([int]($s*0.5),  [int]($pileY - $s*0.1)),
        [System.Drawing.PointF]::new([int]($s*0.85), $pileY),
        [System.Drawing.PointF]::new([int]($s*0.92), $s)
    )
    $sandBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(0xFF, 0xE8, 0xC8, 0x78))
    $g.FillPolygon($sandBrush, $moundPts)
    $sandBrush.Dispose()

    # 하단 물 (왼쪽 절반)
    $waterBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(0xB0, 0x34, 0x68, 0xC8))
    $waterRect  = New-Object System.Drawing.Rectangle(0, [int]($s * 0.80), [int]($s * 0.45), [int]($s * 0.20))
    $g.FillRectangle($waterBrush, $waterRect)
    $waterBrush.Dispose()

    # 모래 낙하 입자들
    $rng = New-Object System.Random(42)
    for ($i = 0; $i -lt 12; $i++) {
        $px = [int]($s * 0.3 + $rng.NextDouble() * $s * 0.4)
        $py = [int]($rng.NextDouble() * $pileY * 0.8)
        $pw = [int]([math]::Max(2, $s * 0.04))
        $g.FillRectangle(
            (New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(0xFF, 0xE8, 0xC8, 0x78))),
            $px, $py, $pw, $pw
        )
    }

    # 불꽃 (오른쪽 상단)
    $fireBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(0xFF, 0xFF, 0x65, 0x00))
    $flameH    = [int]($s * 0.3)
    $flameX    = [int]($s * 0.70)
    $flameW    = [int]($s * 0.22)
    $g.FillEllipse($fireBrush, $flameX, [int]($s * 0.35), $flameW, $flameH)
    $fireBrush.Dispose()

    $fire2 = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(0xFF, 0xFF, 0xCC, 0x00))
    $g.FillEllipse($fire2, [int]($flameX + $flameW*0.15), [int]($s * 0.45), [int]($flameW*0.7), [int]($flameH*0.6))
    $fire2.Dispose()

    # 얼음 결정 (왼쪽 상단)
    $icePen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(0xFF, 0xA8, 0xD8, 0xF0), [int]([math]::Max(1, $s * 0.025)))
    $iceX = [int]($s * 0.18)
    $iceY = [int]($s * 0.18)
    $iceR = [int]($s * 0.10)
    # 얼음 결정 6각형 선
    for ($angle = 0; $angle -lt 360; $angle += 60) {
        $rad = $angle * [math]::PI / 180
        $ex  = $iceX + [int]($iceR * [math]::Cos($rad))
        $ey  = $iceY + [int]($iceR * [math]::Sin($rad))
        $g.DrawLine($icePen, $iceX, $iceY, $ex, $ey)
    }
    $icePen.Dispose()

    $g.Dispose()
    return $bmp
}

# ICO 생성
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
