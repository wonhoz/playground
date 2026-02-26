# Log.Tail 앱 아이콘 생성 스크립트
# 디자인: 다크 배경 + 터미널 스크롤 라인 (그린 #22C55E)

Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms

$outputDir = Join-Path $PSScriptRoot "Resources"
if (-not (Test-Path $outputDir)) { New-Item -ItemType Directory -Path $outputDir | Out-Null }

$icoPath = Join-Path $outputDir "app.ico"
$sizes   = @(16, 32, 48, 256)

function New-IconBitmap([int]$sz) {
    $bmp = New-Object System.Drawing.Bitmap($sz, $sz)
    $g   = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode   = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit

    # 배경: 다크 #0D0D14 라운드 사각형
    $bgBrush  = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(0x0D, 0x0D, 0x14))
    $radius   = [int]($sz * 0.18)
    $rect     = [System.Drawing.Rectangle]::new(0, 0, $sz, $sz)
    $path     = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddArc($rect.X, $rect.Y, $radius * 2, $radius * 2, 180, 90)
    $path.AddArc($rect.Right - $radius * 2, $rect.Y, $radius * 2, $radius * 2, 270, 90)
    $path.AddArc($rect.Right - $radius * 2, $rect.Bottom - $radius * 2, $radius * 2, $radius * 2, 0, 90)
    $path.AddArc($rect.X, $rect.Bottom - $radius * 2, $radius * 2, $radius * 2, 90, 90)
    $path.CloseFigure()
    $g.FillPath($bgBrush, $path)

    # 터미널 라인 — 3개의 수평선 (그린 계열, 밝기 차등)
    $green1 = [System.Drawing.Color]::FromArgb(0x22, 0xC5, 0x5E)  # 밝은 그린 (활성 라인)
    $green2 = [System.Drawing.Color]::FromArgb(0x16, 0x80, 0x40)  # 중간 그린
    $green3 = [System.Drawing.Color]::FromArgb(0x0E, 0x4A, 0x28)  # 어두운 그린

    if ($sz -ge 48) {
        $lw  = [int]([Math]::Max(1, $sz * 0.07))  # 라인 두께
        $pad = [int]($sz * 0.18)                   # 좌우 여백
        $w3  = $sz - $pad * 2                      # 라인 폭

        # 라인 1 (상단 - 가장 밝음, 짧음 → 활성 커서 라인)
        $y1  = [int]($sz * 0.33)
        $pen1 = [System.Drawing.Pen]::new($green1, $lw)
        $pen1.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
        $pen1.EndCap   = [System.Drawing.Drawing2D.LineCap]::Round
        $g.DrawLine($pen1, $pad, $y1, $pad + [int]($w3 * 0.55), $y1)

        # 커서 블록 (라인 1 끝에 붙은 사각형)
        $cw = [int]($sz * 0.07)
        $cx = $pad + [int]($w3 * 0.55) + $lw
        $cBrush = [System.Drawing.SolidBrush]::new($green1)
        $g.FillRectangle($cBrush, $cx, $y1 - $lw, $cw, $lw * 2)

        # 라인 2 (중간)
        $y2  = [int]($sz * 0.52)
        $pen2 = [System.Drawing.Pen]::new($green2, $lw)
        $pen2.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
        $pen2.EndCap   = [System.Drawing.Drawing2D.LineCap]::Round
        $g.DrawLine($pen2, $pad, $y2, $pad + $w3, $y2)

        # 라인 3 (하단 - 가장 어두움)
        $y3  = [int]($sz * 0.70)
        $pen3 = [System.Drawing.Pen]::new($green3, $lw)
        $pen3.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
        $pen3.EndCap   = [System.Drawing.Drawing2D.LineCap]::Round
        $g.DrawLine($pen3, $pad, $y3, $pad + [int]($w3 * 0.78), $y3)

        $pen1.Dispose(); $pen2.Dispose(); $pen3.Dispose(); $cBrush.Dispose()
    } else {
        # 소형 아이콘: 간단한 3선 표시
        $lw  = [int]([Math]::Max(1, $sz * 0.12))
        $pad = [int]($sz * 0.2)
        $w3  = $sz - $pad * 2
        $pen1 = [System.Drawing.Pen]::new($green1, $lw)
        $pen2 = [System.Drawing.Pen]::new($green2, $lw)
        $pen3 = [System.Drawing.Pen]::new($green3, $lw)
        $g.DrawLine($pen1, $pad, [int]($sz * 0.3),  $pad + [int]($w3 * 0.6), [int]($sz * 0.3))
        $g.DrawLine($pen2, $pad, [int]($sz * 0.5),  $pad + $w3,              [int]($sz * 0.5))
        $g.DrawLine($pen3, $pad, [int]($sz * 0.7),  $pad + [int]($w3 * 0.8), [int]($sz * 0.7))
        $pen1.Dispose(); $pen2.Dispose(); $pen3.Dispose()
    }

    $bgBrush.Dispose(); $g.Dispose()
    return $bmp
}

# ICO 파일 생성
$ms = New-Object System.IO.MemoryStream
$writer = New-Object System.IO.BinaryWriter($ms)

$bitmaps = $sizes | ForEach-Object { New-IconBitmap $_ }

# ICO 헤더
$writer.Write([uint16]0)       # 예약
$writer.Write([uint16]1)       # 타입 (1 = ICO)
$writer.Write([uint16]$sizes.Count) # 이미지 수

# 디렉터리 엔트리 (각 크기별 오프셋 계산을 위해 먼저 PNG 바이트 배열 생성)
$pngBytes = $bitmaps | ForEach-Object {
    $pms = New-Object System.IO.MemoryStream
    $_.Save($pms, [System.Drawing.Imaging.ImageFormat]::Png)
    $pms.ToArray()
}

$dataOffset = 6 + $sizes.Count * 16  # 헤더(6) + 디렉터리(16 * N)
$currentOffset = $dataOffset

for ($i = 0; $i -lt $sizes.Count; $i++) {
    $sz = $sizes[$i]
    $displaySz = if ($sz -ge 256) { 0 } else { $sz }  # 256+ → 0
    $writer.Write([byte]$displaySz)   # Width
    $writer.Write([byte]$displaySz)   # Height
    $writer.Write([byte]0)            # 색상 팔레트 수
    $writer.Write([byte]0)            # 예약
    $writer.Write([uint16]1)          # 색상 플레인
    $writer.Write([uint16]32)         # 비트 깊이
    $writer.Write([uint32]$pngBytes[$i].Length) # 데이터 크기
    $writer.Write([uint32]$currentOffset)       # 데이터 오프셋
    $currentOffset += $pngBytes[$i].Length
}

foreach ($bytes in $pngBytes) {
    $writer.Write($bytes)
}

[System.IO.File]::WriteAllBytes($icoPath, $ms.ToArray())
$writer.Dispose(); $ms.Dispose()
$bitmaps | ForEach-Object { $_.Dispose() }

Write-Host "아이콘 생성 완료: $icoPath"
