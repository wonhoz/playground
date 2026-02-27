# Mock.Server 앱 아이콘 생성 스크립트
# 디자인: 다크 배경 + 서버 랙 3단 (Indigo) + 요청/응답 화살표 (Cyan) + LED (Green)

Add-Type -AssemblyName System.Drawing

$outputDir = Join-Path $PSScriptRoot "Resources"
if (-not (Test-Path $outputDir)) { New-Item -ItemType Directory -Path $outputDir | Out-Null }

$icoPath  = Join-Path $outputDir "app.ico"
$png32Path = Join-Path $outputDir "icon32.png"
$sizes    = @(16, 32, 48, 256)

# 색상 팔레트
$colBg    = [System.Drawing.Color]::FromArgb(0x0D, 0x0D, 0x1A)  # 배경 다크 네이비
$colInd1  = [System.Drawing.Color]::FromArgb(0x81, 0x8C, 0xF8)  # Indigo 밝음
$colInd2  = [System.Drawing.Color]::FromArgb(0x63, 0x66, 0xF1)  # Indigo 중간
$colInd3  = [System.Drawing.Color]::FromArgb(0x4F, 0x46, 0xE5)  # Indigo 어두움
$colLed   = [System.Drawing.Color]::FromArgb(0x22, 0xC5, 0x5E)  # 녹색 LED
$colArrow = [System.Drawing.Color]::FromArgb(0x22, 0xD3, 0xEE)  # Cyan 화살표

function Add-RoundRect([System.Drawing.Graphics]$g, [System.Drawing.Brush]$brush,
                       [float]$x, [float]$y, [float]$w, [float]$h, [float]$r) {
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddArc($x,           $y,           $r*2, $r*2, 180, 90)
    $path.AddArc($x+$w-$r*2,  $y,           $r*2, $r*2, 270, 90)
    $path.AddArc($x+$w-$r*2,  $y+$h-$r*2,  $r*2, $r*2,   0, 90)
    $path.AddArc($x,           $y+$h-$r*2,  $r*2, $r*2,  90, 90)
    $path.CloseFigure()
    $g.FillPath($brush, $path)
    $path.Dispose()
}

function New-IconBitmap([int]$sz) {
    $bmp = New-Object System.Drawing.Bitmap($sz, $sz)
    $g   = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode     = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.Clear([System.Drawing.Color]::Transparent)

    # ── 배경 (라운드 사각형) ────────────────────────────────────────
    $bgBrush = New-Object System.Drawing.SolidBrush($colBg)
    $bgR     = [float]([Math]::Max(1, $sz * 0.18))
    Add-RoundRect $g $bgBrush 0 0 $sz $sz $bgR
    $bgBrush.Dispose()

    if ($sz -ge 48) {
        # ── 서버 랙 3단 ─────────────────────────────────────────────
        $padX = [float]($sz * 0.14)
        $padY = [float]($sz * 0.13)
        $rw   = [float]($sz - $padX * 2)          # 랙 폭
        $gap  = [float]($sz * 0.045)
        $rh   = [float](($sz - $padY * 2 - $gap * 2) / 3)  # 블록 높이
        $rr   = [float]([Math]::Max(1.5, $sz * 0.04))       # 블록 코너

        $colors = @($colInd1, $colInd2, $colInd3)
        for ($i = 0; $i -lt 3; $i++) {
            $ry = [float]($padY + ($rh + $gap) * $i)
            $rb = New-Object System.Drawing.SolidBrush($colors[$i])
            Add-RoundRect $g $rb $padX $ry $rw $rh $rr
            $rb.Dispose()

            # LED 도트 2개 (우측)
            $ledR  = [float]([Math]::Max(1.5, $sz * 0.05))
            $ledGap = [float]($sz * 0.05)
            $ledY  = [float]($ry + ($rh - $ledR * 2) / 2)
            $ledX2 = [float]($padX + $rw - $ledR * 2 - [float]($sz * 0.05))
            $ledX1 = [float]($ledX2 - $ledR * 2 - $ledGap)
            $ledBrush = New-Object System.Drawing.SolidBrush($colLed)
            $g.FillEllipse($ledBrush, $ledX1, $ledY, $ledR * 2, $ledR * 2)
            # 두 번째 LED: 상단 랙은 밝게, 나머지는 어둡게
            $ledBrush2Col = if ($i -eq 0) { $colLed } else { [System.Drawing.Color]::FromArgb(0x1A, 0x44, 0x2C) }
            $ledBrush2 = New-Object System.Drawing.SolidBrush($ledBrush2Col)
            $g.FillEllipse($ledBrush2, $ledX2, $ledY, $ledR * 2, $ledR * 2)
            $ledBrush.Dispose(); $ledBrush2.Dispose()
        }

        # ── 요청/응답 화살표 오버레이 (하단 랙 위) ───────────────────
        # 상단 랙 위에 작은 ↔ 화살표: Mock 개념 표현
        $arrowBrush = New-Object System.Drawing.SolidBrush($colArrow)
        $arrowPen   = New-Object System.Drawing.Pen($colArrow, [float]([Math]::Max(1, $sz * 0.035)))
        $arrowPen.SetLineCap(
            [System.Drawing.Drawing2D.LineCap]::ArrowAnchor,
            [System.Drawing.Drawing2D.LineCap]::ArrowAnchor,
            [System.Drawing.Drawing2D.DashCap]::Round)

        # 상단 랙 내부 중앙에 수평 양방향 화살표
        $ay   = [float]($padY + $rh / 2)
        $ax1  = [float]($padX + $sz * 0.12)
        $ax2  = [float]($padX + $rw - $sz * 0.30)  # LED 영역 제외
        $midX = [float](($ax1 + $ax2) / 2)
        $arrowH = [float]([Math]::Max(1.5, $sz * 0.06))

        # 위쪽 화살표 (→)
        $arrowPen2 = New-Object System.Drawing.Pen($colArrow, [float]([Math]::Max(1, $sz * 0.03)))
        $arrowPen2.CustomEndCap = New-Object System.Drawing.Drawing2D.AdjustableArrowCap(
            [float]([Math]::Max(1.5, $sz * 0.05)),
            [float]([Math]::Max(1.5, $sz * 0.05)), $true)
        $g.DrawLine($arrowPen2, $ax1, [float]($ay - $arrowH * 0.6), $ax2, [float]($ay - $arrowH * 0.6))

        # 아래쪽 화살표 (←)
        $arrowPen3 = New-Object System.Drawing.Pen($colArrow, [float]([Math]::Max(1, $sz * 0.03)))
        $arrowPen3.CustomEndCap = New-Object System.Drawing.Drawing2D.AdjustableArrowCap(
            [float]([Math]::Max(1.5, $sz * 0.05)),
            [float]([Math]::Max(1.5, $sz * 0.05)), $true)
        # 반대 방향: ax2→ax1
        $g.DrawLine($arrowPen3, $ax2, [float]($ay + $arrowH * 0.6), $ax1, [float]($ay + $arrowH * 0.6))

        $arrowBrush.Dispose()
        $arrowPen.Dispose(); $arrowPen2.Dispose(); $arrowPen3.Dispose()

    } else {
        # ── 소형 아이콘 (16/32): 단순 3선 + 우측 LED ───────────────
        $padX = [float]($sz * 0.16)
        $padY = [float]($sz * 0.14)
        $rw   = [float]($sz - $padX * 2)
        $lh   = [float]([Math]::Max(2, $sz * 0.14))
        $gap  = [float]([Math]::Max(1, $sz * 0.06))
        $rr   = [float]([Math]::Max(1, $sz * 0.04))

        $totalH = $lh * 3 + $gap * 2
        $startY = [float](($sz - $totalH) / 2)

        $colors = @($colInd1, $colInd2, $colInd3)
        for ($i = 0; $i -lt 3; $i++) {
            $ry = [float]($startY + ($lh + $gap) * $i)
            $rb = New-Object System.Drawing.SolidBrush($colors[$i])
            Add-RoundRect $g $rb $padX $ry $rw $lh $rr
            $rb.Dispose()

            # LED (상단 블록만 표시)
            if ($i -eq 0 -and $sz -ge 24) {
                $ledR = [float]([Math]::Max(1, $sz * 0.06))
                $ledX = [float]($padX + $rw - $ledR * 2 - $sz * 0.05)
                $ledY = [float]($ry + ($lh - $ledR * 2) / 2)
                $lb   = New-Object System.Drawing.SolidBrush($colLed)
                $g.FillEllipse($lb, $ledX, $ledY, $ledR * 2, $ledR * 2)
                $lb.Dispose()
            }
        }
    }

    $g.Dispose()
    return $bmp
}

# ── ICO 파일 생성 ────────────────────────────────────────────────────
$ms     = New-Object System.IO.MemoryStream
$writer = New-Object System.IO.BinaryWriter($ms)

$bitmaps = $sizes | ForEach-Object { New-IconBitmap $_ }

# 각 크기별 PNG 바이트 배열
$pngBytes = $bitmaps | ForEach-Object {
    $pms = New-Object System.IO.MemoryStream
    $_.Save($pms, [System.Drawing.Imaging.ImageFormat]::Png)
    $pms.ToArray()
}

# ICO 헤더
$writer.Write([uint16]0)
$writer.Write([uint16]1)
$writer.Write([uint16]$sizes.Count)

$dataOffset    = 6 + $sizes.Count * 16
$currentOffset = $dataOffset

for ($i = 0; $i -lt $sizes.Count; $i++) {
    $szVal     = $sizes[$i]
    $displaySz = if ($szVal -ge 256) { 0 } else { $szVal }
    $writer.Write([byte]$displaySz)
    $writer.Write([byte]$displaySz)
    $writer.Write([byte]0)
    $writer.Write([byte]0)
    $writer.Write([uint16]1)
    $writer.Write([uint16]32)
    $writer.Write([uint32]$pngBytes[$i].Length)
    $writer.Write([uint32]$currentOffset)
    $currentOffset += $pngBytes[$i].Length
}
foreach ($bytes in $pngBytes) { $writer.Write($bytes) }

[System.IO.File]::WriteAllBytes($icoPath, $ms.ToArray())
$writer.Dispose(); $ms.Dispose()

# WPF 창 아이콘용 32px PNG (index 1 = 32px)
$bitmaps[1].Save($png32Path, [System.Drawing.Imaging.ImageFormat]::Png)
$bitmaps | ForEach-Object { $_.Dispose() }

$icoSize = (Get-Item $icoPath).Length
Write-Host "아이콘 생성 완료: $icoPath ($icoSize bytes) / $png32Path"
