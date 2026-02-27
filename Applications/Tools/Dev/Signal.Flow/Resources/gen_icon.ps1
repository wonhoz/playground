# Signal.Flow 아이콘 생성 스크립트
# 모티프: 물결/흐름(Flow) + 화살표 — 청록(#06B6D4) 계열
# 출력: Resources\app.ico (16/32/48/256px)

Add-Type -AssemblyName System.Drawing

$sizes   = @(16, 32, 48, 256)
$outPath = Join-Path $PSScriptRoot "app.ico"
$bmps    = @()

foreach ($sz in $sizes) {
    $bmp = New-Object System.Drawing.Bitmap($sz, $sz)
    $g   = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode   = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit

    # 배경 — 짙은 남색
    $bgBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 10, 15, 20))
    $g.FillRectangle($bgBrush, 0, 0, $sz, $sz)
    $bgBrush.Dispose()

    # 물결 호(arc) — 청록색 계열 3겹
    $colors = @(
        [System.Drawing.Color]::FromArgb(80,  6, 182, 212),
        [System.Drawing.Color]::FromArgb(160, 6, 182, 212),
        [System.Drawing.Color]::FromArgb(255, 6, 182, 212)
    )
    $sw  = [float]($sz * 0.09)
    $pad = [float]($sz * 0.08)

    for ($i = 0; $i -lt 3; $i++) {
        $p   = New-Object System.Drawing.Pen($colors[$i], $sw)
        $p.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
        $p.EndCap   = [System.Drawing.Drawing2D.LineCap]::Round

        $offset = [float](($i - 1) * $sz * 0.20)
        $y0 = [float]($sz * 0.25 + $offset)
        $rectSz = [float]($sz * 0.70)
        $x0 = [float](($sz - $rectSz) / 2.0)
        $rect = [System.Drawing.RectangleF]::new($x0, $y0, $rectSz, $rectSz)
        $g.DrawArc($p, $rect, 210, 120)
        $p.Dispose()
    }

    # 화살표 (오른쪽 하단) — 흰색
    if ($sz -ge 32) {
        $arrowPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(220, 255, 255, 255), [float]($sz * 0.09))
        $arrowPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
        $arrowPen.EndCap   = [System.Drawing.Drawing2D.LineCap]::ArrowAnchor

        $x1 = [float]($sz * 0.28)
        $x2 = [float]($sz * 0.72)
        $y  = [float]($sz * 0.72)
        $g.DrawLine($arrowPen, $x1, $y, $x2, $y)
        $arrowPen.Dispose()
    }

    $g.Dispose()
    $bmps += $bmp
}

# ICO 파일 직접 구성
$ms = New-Object System.IO.MemoryStream
$bw = New-Object System.IO.BinaryWriter($ms)

$count = $bmps.Count

# ICO 헤더
$bw.Write([uint16]0)       # 예약
$bw.Write([uint16]1)       # 타입 (1 = ICO)
$bw.Write([uint16]$count)  # 이미지 수

# 이미지 데이터 저장
$imgStreams = @()
foreach ($b in $bmps) {
    $ims = New-Object System.IO.MemoryStream
    $b.Save($ims, [System.Drawing.Imaging.ImageFormat]::Png)
    $imgStreams += $ims
}

# 디렉토리 엔트리 오프셋 계산
$dirSize    = 16 * $count
$headerSize = 6
$offset     = $headerSize + $dirSize

foreach ($i in 0..($count-1)) {
    $sz  = $sizes[$i]
    $ims = $imgStreams[$i]
    $szVal = if ($sz -ge 256) { 0 } else { $sz }
    $bw.Write([byte]$szVal)       # 폭
    $bw.Write([byte]$szVal)       # 높이
    $bw.Write([byte]0)            # 색상 수
    $bw.Write([byte]0)            # 예약
    $bw.Write([uint16]1)          # 색상 플레인
    $bw.Write([uint16]32)         # bpp
    $bw.Write([uint32]$ims.Length)
    $bw.Write([uint32]$offset)
    $offset += $ims.Length
}

foreach ($ims in $imgStreams) {
    $bw.Write($ims.ToArray())
    $ims.Dispose()
}

$bw.Flush()

[System.IO.File]::WriteAllBytes($outPath, $ms.ToArray())
$ms.Dispose()
$bw.Dispose()

foreach ($b in $bmps) { $b.Dispose() }

Write-Host "app.ico 생성 완료: $outPath" -ForegroundColor Cyan
