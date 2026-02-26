# Mock.Server 앱 아이콘 생성 스크립트
# 디자인: 다크 배경 + 서버 랙 (Indigo #818CF8)

Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms

$outputDir = Join-Path $PSScriptRoot "Resources"
if (-not (Test-Path $outputDir)) { New-Item -ItemType Directory -Path $outputDir | Out-Null }

$icoPath = Join-Path $outputDir "app.ico"
$sizes   = @(16, 32, 48, 256)

function New-IconBitmap([int]$sz) {
    $bmp = New-Object System.Drawing.Bitmap($sz, $sz)
    $g   = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode    = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit

    # 배경: 다크 #0D0D16 라운드 사각형
    $bgBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(0x0D, 0x0D, 0x16))
    $radius  = [int]($sz * 0.18)
    $rect    = [System.Drawing.Rectangle]::new(0, 0, $sz, $sz)
    $path    = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddArc($rect.X, $rect.Y, $radius * 2, $radius * 2, 180, 90)
    $path.AddArc($rect.Right - $radius * 2, $rect.Y, $radius * 2, $radius * 2, 270, 90)
    $path.AddArc($rect.Right - $radius * 2, $rect.Bottom - $radius * 2, $radius * 2, $radius * 2, 0, 90)
    $path.AddArc($rect.X, $rect.Bottom - $radius * 2, $radius * 2, $radius * 2, 90, 90)
    $path.CloseFigure()
    $g.FillPath($bgBrush, $path)

    # 서버 랙 색상 (Indigo 계열)
    $ind1 = [System.Drawing.Color]::FromArgb(0x81, 0x8C, 0xF8)  # #818CF8 밝은 Indigo
    $ind2 = [System.Drawing.Color]::FromArgb(0x63, 0x66, 0xF1)  # #6366F1 중간 Indigo
    $ind3 = [System.Drawing.Color]::FromArgb(0x43, 0x38, 0xCA)  # #4338CA 어두운 Indigo
    $led  = [System.Drawing.Color]::FromArgb(0x22, 0xC5, 0x5E)  # #22C55E 녹색 LED

    if ($sz -ge 48) {
        $padX = [int]($sz * 0.15)
        $padY = [int]($sz * 0.14)
        $w    = $sz - $padX * 2
        $gap  = [int]($sz * 0.04)
        $bh   = [int](($sz - $padY * 2 - $gap * 2) / 3)  # 블록 높이

        # 블록 1 (상단 - 가장 밝음)
        $y1 = $padY
        $br1 = [System.Drawing.SolidBrush]::new($ind1)
        $g.FillRectangle($br1, $padX, $y1, $w, $bh)

        # 블록 2 (중간)
        $y2 = $padY + $bh + $gap
        $br2 = [System.Drawing.SolidBrush]::new($ind2)
        $g.FillRectangle($br2, $padX, $y2, $w, $bh)

        # 블록 3 (하단 - 가장 어두움)
        $y3 = $padY + ($bh + $gap) * 2
        $br3 = [System.Drawing.SolidBrush]::new($ind3)
        $g.FillRectangle($br3, $padX, $y3, $w, $bh)

        # LED (좌측 원 - 블록 1만)
        $ledR  = [int]([Math]::Max(2, $sz * 0.06))
        $ledX  = $padX + [int]($sz * 0.06)
        $ledY1 = $y1 + ($bh - $ledR * 2) / 2
        $brLed = [System.Drawing.SolidBrush]::new($led)
        $g.FillEllipse($brLed, $ledX, $ledY1, $ledR * 2, $ledR * 2)

        $br1.Dispose(); $br2.Dispose(); $br3.Dispose(); $brLed.Dispose()
    } else {
        # 소형 아이콘: 단순 3선 (수평 사각형)
        $padX = [int]($sz * 0.18)
        $padY = [int]($sz * 0.16)
        $w    = $sz - $padX * 2
        $lh   = [int]([Math]::Max(1, $sz * 0.13))
        $gap  = [int]([Math]::Max(1, $sz * 0.07))

        $br1 = [System.Drawing.SolidBrush]::new($ind1)
        $br2 = [System.Drawing.SolidBrush]::new($ind2)
        $br3 = [System.Drawing.SolidBrush]::new($ind3)

        $totalH = $lh * 3 + $gap * 2
        $startY = ($sz - $totalH) / 2

        $g.FillRectangle($br1, $padX, $startY, $w, $lh)
        $g.FillRectangle($br2, $padX, $startY + $lh + $gap, $w, $lh)
        $g.FillRectangle($br3, $padX, $startY + ($lh + $gap) * 2, $w, $lh)

        $br1.Dispose(); $br2.Dispose(); $br3.Dispose()
    }

    $bgBrush.Dispose(); $g.Dispose()
    return $bmp
}

# ICO 파일 생성
$ms     = New-Object System.IO.MemoryStream
$writer = New-Object System.IO.BinaryWriter($ms)

$bitmaps = $sizes | ForEach-Object { New-IconBitmap $_ }

# ICO 헤더
$writer.Write([uint16]0)            # 예약
$writer.Write([uint16]1)            # 타입 (1 = ICO)
$writer.Write([uint16]$sizes.Count) # 이미지 수

# 각 크기별 PNG 바이트 배열 생성
$pngBytes = $bitmaps | ForEach-Object {
    $pms = New-Object System.IO.MemoryStream
    $_.Save($pms, [System.Drawing.Imaging.ImageFormat]::Png)
    $pms.ToArray()
}

$dataOffset     = 6 + $sizes.Count * 16
$currentOffset  = $dataOffset

for ($i = 0; $i -lt $sizes.Count; $i++) {
    $szVal = $sizes[$i]
    if ($szVal -ge 256) { $displaySz = 0 } else { $displaySz = $szVal }
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

foreach ($bytes in $pngBytes) {
    $writer.Write($bytes)
}

[System.IO.File]::WriteAllBytes($icoPath, $ms.ToArray())
$writer.Dispose(); $ms.Dispose()

# WPF 창 아이콘용 32px PNG
$png32Path = Join-Path $outputDir "icon32.png"
$bitmaps[1].Save($png32Path, [System.Drawing.Imaging.ImageFormat]::Png)

$bitmaps | ForEach-Object { $_.Dispose() }

Write-Host "아이콘 생성 완료: $icoPath / $png32Path"
