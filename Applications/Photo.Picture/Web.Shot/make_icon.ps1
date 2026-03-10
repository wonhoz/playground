# Web.Shot 아이콘 생성 스크립트
# 웹 카메라/캡처 테마 (카메라 렌즈 + 브라우저 창 + 저장 화살표)
Add-Type -AssemblyName System.Drawing

$outIco = Join-Path $PSScriptRoot "Resources\app.ico"
$sizes  = @(16, 32, 48, 256)
$bitmaps = [System.Collections.Generic.List[System.Drawing.Bitmap]]::new()

foreach ($sz in $sizes) {
    $bmp = [System.Drawing.Bitmap]::new($sz, $sz, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g   = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode   = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.Clear([System.Drawing.Color]::Transparent)

    $s  = $sz
    $cx = $s / 2.0
    $cy = $s / 2.0

    if ($sz -ge 48) {
        # 큰 사이즈: 상세 브라우저+카메라 디자인
        $pad = [int]($s * 0.06)

        # 배경 — 다크 블루 그라디언트
        $bgTop = [System.Drawing.Color]::FromArgb(255, 15, 25, 50)
        $bgBot = [System.Drawing.Color]::FromArgb(255, 8, 15, 35)
        $bgBrush = [System.Drawing.Drawing2D.LinearGradientBrush]::new(
            [System.Drawing.Point]::new(0, 0),
            [System.Drawing.Point]::new(0, $s),
            $bgTop, $bgBot)
        $bgPath = [System.Drawing.Drawing2D.GraphicsPath]::new()
        $r = [int]($s * 0.18)
        $bgPath.AddRoundedRectangle = $null
        # 수동 라운드 rect
        $bgPath.StartFigure()
        $bgPath.AddArc($pad, $pad, $r*2, $r*2, 180, 90)
        $bgPath.AddArc($s - $pad - $r*2, $pad, $r*2, $r*2, 270, 90)
        $bgPath.AddArc($s - $pad - $r*2, $s - $pad - $r*2, $r*2, $r*2, 0, 90)
        $bgPath.AddArc($pad, $s - $pad - $r*2, $r*2, $r*2, 90, 90)
        $bgPath.CloseFigure()
        $g.FillPath($bgBrush, $bgPath)
        $bgBrush.Dispose()

        # 브라우저 창 외곽 — 회색
        $winW = [int]($s * 0.72)
        $winH = [int]($s * 0.58)
        $winX = [int]($cx - $winW / 2.0)
        $winY = [int]($cy - $winH / 2.0 - $s * 0.06)
        $winR = [int]($s * 0.07)
        $winPath = [System.Drawing.Drawing2D.GraphicsPath]::new()
        $winPath.StartFigure()
        $winPath.AddArc($winX, $winY, $winR*2, $winR*2, 180, 90)
        $winPath.AddArc($winX + $winW - $winR*2, $winY, $winR*2, $winR*2, 270, 90)
        $winPath.AddArc($winX + $winW - $winR*2, $winY + $winH - $winR*2, $winR*2, $winR*2, 0, 90)
        $winPath.AddArc($winX, $winY + $winH - $winR*2, $winR*2, $winR*2, 90, 90)
        $winPath.CloseFigure()
        $g.FillPath([System.Drawing.Brushes]::DimGray, $winPath)

        # 브라우저 상단 바 (타이틀바)
        $barH = [int]($s * 0.13)
        $barPath = [System.Drawing.Drawing2D.GraphicsPath]::new()
        $barPath.StartFigure()
        $barPath.AddArc($winX, $winY, $winR*2, $winR*2, 180, 90)
        $barPath.AddArc($winX + $winW - $winR*2, $winY, $winR*2, $winR*2, 270, 90)
        $barPath.AddLine($winX + $winW, $winY + $barH, $winX, $winY + $barH)
        $barPath.CloseFigure()
        $barColor = [System.Drawing.Color]::FromArgb(255, 45, 60, 90)
        $g.FillPath([System.Drawing.SolidBrush]::new($barColor), $barPath)

        # 컨텐츠 영역 (흰색/연한 배경)
        $contY = $winY + $barH
        $contH = $winH - $barH
        $contPath = [System.Drawing.Drawing2D.GraphicsPath]::new()
        $contPath.StartFigure()
        $contPath.AddLine($winX, $contY, $winX + $winW, $contY)
        $contPath.AddArc($winX + $winW - $winR*2, $contY + $contH - $winR*2, $winR*2, $winR*2, 0, 90)
        $contPath.AddArc($winX, $contY + $contH - $winR*2, $winR*2, $winR*2, 90, 90)
        $contPath.CloseFigure()
        $contColor = [System.Drawing.Color]::FromArgb(255, 18, 28, 55)
        $g.FillPath([System.Drawing.SolidBrush]::new($contColor), $contPath)

        # 카메라 아이콘 (원형 렌즈)
        $camR = [int]($s * 0.16)
        $camX = [int]($cx - $camR)
        $camY = [int]($contY + $contH * 0.25 - $camR)
        # 외부 원 (흰 테두리)
        $g.DrawEllipse([System.Drawing.Pens]::White, $camX, $camY, $camR*2, $camR*2)
        # 렌즈 (청록 원)
        $lensColor = [System.Drawing.Color]::FromArgb(255, 30, 180, 230)
        $innerR = [int]($camR * 0.65)
        $g.FillEllipse([System.Drawing.SolidBrush]::new($lensColor), $cx-$innerR, $contY+$contH*0.25-$innerR, $innerR*2, $innerR*2)

        # 저장 화살표 (아래쪽)
        $arrW = [int]($s * 0.22)
        $arrH = [int]($s * 0.14)
        $arrX = [int]($cx - $arrW * 0.5)
        $arrY = [int]($contY + $contH * 0.62)
        $arrowColor = [System.Drawing.Color]::FromArgb(255, 80, 220, 130)
        $arrowBrush = [System.Drawing.SolidBrush]::new($arrowColor)
        # 화살표 shaft
        $shaftW = [int]($arrW * 0.35)
        $shaftX = [int]($cx - $shaftW * 0.5)
        $g.FillRectangle($arrowBrush, $shaftX, $arrY, $shaftW, [int]($arrH * 0.55))
        # 화살표 머리 (삼각형)
        $tipY = [int]($arrY + $arrH * 0.55)
        $headPts = @(
            [System.Drawing.Point]::new($arrX, $tipY),
            [System.Drawing.Point]::new($arrX + $arrW, $tipY),
            [System.Drawing.Point]::new([int]$cx, $arrY + $arrH)
        )
        $g.FillPolygon($arrowBrush, $headPts)
        $arrowBrush.Dispose()

    } elseif ($sz -ge 32) {
        # 중간 사이즈
        $pad = 2

        # 배경 원형
        $bgColor = [System.Drawing.Color]::FromArgb(255, 10, 20, 45)
        $g.FillEllipse([System.Drawing.SolidBrush]::new($bgColor), $pad, $pad, $s - $pad*2, $s - $pad*2)

        # 브라우저 창 사각형
        $bx = [int]($s * 0.12)
        $by = [int]($s * 0.15)
        $bw = [int]($s * 0.76)
        $bh = [int]($s * 0.55)
        $g.FillRectangle([System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255,50,70,110)), $bx, $by, $bw, $bh)
        # 타이틀바
        $barH2 = [int]($s * 0.14)
        $g.FillRectangle([System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255,70,100,160)), $bx, $by, $bw, $barH2)

        # 카메라 렌즈 원
        $camR2 = [int]($s * 0.13)
        $lColor = [System.Drawing.Color]::FromArgb(255, 40, 200, 240)
        $g.FillEllipse([System.Drawing.SolidBrush]::new($lColor), [int]($cx-$camR2), [int]($by+$barH2+($bh-$barH2)*0.15), $camR2*2, $camR2*2)

        # 화살표
        $aColor = [System.Drawing.Color]::FromArgb(255, 80, 220, 130)
        $aY = [int]($by + $bh * 0.77)
        $aW = [int]($s * 0.16)
        $aPts = @(
            [System.Drawing.Point]::new([int]($cx-$aW), $aY),
            [System.Drawing.Point]::new([int]($cx+$aW), $aY),
            [System.Drawing.Point]::new([int]$cx, $aY + [int]($s*0.14))
        )
        $g.FillPolygon([System.Drawing.SolidBrush]::new($aColor), $aPts)

    } else {
        # 소형 16px — 단순 디자인
        $bgColor = [System.Drawing.Color]::FromArgb(255, 10, 20, 50)
        $g.FillRectangle([System.Drawing.SolidBrush]::new($bgColor), 0, 0, $s, $s)
        # 카메라 동그라미 (청록)
        $r2 = 5
        $lColor = [System.Drawing.Color]::FromArgb(255, 40, 200, 240)
        $g.FillEllipse([System.Drawing.SolidBrush]::new($lColor), [int]($cx-$r2), [int]($cy-$r2-1), $r2*2, $r2*2)
        # 화살표
        $aColor = [System.Drawing.Color]::FromArgb(255, 80, 220, 130)
        $aY2 = [int]($cy + 3)
        $aPts2 = @(
            [System.Drawing.Point]::new([int]($cx-3), $aY2),
            [System.Drawing.Point]::new([int]($cx+3), $aY2),
            [System.Drawing.Point]::new([int]$cx, $aY2 + 3)
        )
        $g.FillPolygon([System.Drawing.SolidBrush]::new($aColor), $aPts2)
    }

    $g.Dispose()
    $bitmaps.Add($bmp)
}

# ICO 파일 직접 작성
$stream = [System.IO.File]::Create($outIco)
$writer = [System.IO.BinaryWriter]::new($stream)

$count = $bitmaps.Count
# ICONDIR
$writer.Write([uint16]0)   # Reserved
$writer.Write([uint16]1)   # Type (1=ICO)
$writer.Write([uint16]$count)

# 각 비트맵을 PNG로 인코딩
$pngData = [System.Collections.Generic.List[byte[]]]::new()
foreach ($bmp in $bitmaps) {
    $ms = [System.IO.MemoryStream]::new()
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $pngData.Add($ms.ToArray())
    $ms.Dispose()
}

# 헤더 크기: 6 + 16*count
$headerSize = 6 + 16 * $count
$offset = $headerSize

# ICONDIRENTRY
for ($i = 0; $i -lt $count; $i++) {
    $szVal = $sizes[$i]
    if ($szVal -ge 256) {
        $writer.Write([byte]0)
    } else {
        $writer.Write([byte]$szVal)
    }
    if ($szVal -ge 256) {
        $writer.Write([byte]0)
    } else {
        $writer.Write([byte]$szVal)
    }
    $writer.Write([byte]0)    # ColorCount
    $writer.Write([byte]0)    # Reserved
    $writer.Write([uint16]1)  # Planes
    $writer.Write([uint16]32) # BitCount
    $writer.Write([uint32]$pngData[$i].Length)
    $writer.Write([uint32]$offset)
    $offset += $pngData[$i].Length
}

# PNG 데이터
foreach ($data in $pngData) {
    $writer.Write($data)
}

$writer.Close()
$stream.Close()
foreach ($bmp in $bitmaps) { $bmp.Dispose() }

Write-Host "아이콘 생성 완료: $outIco"
