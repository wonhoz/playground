# Spec.View 아이콘 생성 — 시안 CPU 칩셋 심볼 (16/32/48/256px)
Add-Type -AssemblyName System.Drawing

$outDir = Join-Path $PSScriptRoot "Resources"
if (-not (Test-Path $outDir)) { New-Item -ItemType Directory -Path $outDir | Out-Null }
$icoPath = Join-Path $outDir "app.ico"

$sizes   = @(16, 32, 48, 256)
$bitmaps = [System.Collections.Generic.List[System.Drawing.Bitmap]]::new()

foreach ($sz in $sizes) {
    $bmp = [System.Drawing.Bitmap]::new($sz, $sz)
    $g   = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.Clear([System.Drawing.Color]::Transparent)

    $bg     = [System.Drawing.Color]::FromArgb(255, 11, 15, 26)
    $accent = [System.Drawing.Color]::FromArgb(255, 0, 200, 224)

    # 배경 원형
    $bgBrush = [System.Drawing.SolidBrush]::new($bg)
    $g.FillEllipse($bgBrush, 1, 1, $sz - 2, $sz - 2)
    $bgBrush.Dispose()

    # 테두리
    $borderPen = [System.Drawing.Pen]::new($accent, [math]::Max(1, $sz / 20))
    $g.DrawEllipse($borderPen, 2, 2, $sz - 4, $sz - 4)
    $borderPen.Dispose()

    if ($sz -ge 32) {
        $cx    = $sz / 2
        $cy    = $sz / 2
        $chipW = $sz * 0.44
        $chipH = $sz * 0.44
        $chipX = $cx - $chipW / 2
        $chipY = $cy - $chipH / 2
        $pinLen = $sz * 0.08
        $pinW   = [math]::Max(1, $sz / 32)

        # 칩 배경
        $chipBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255, 17, 24, 39))
        $g.FillRectangle($chipBrush, $chipX, $chipY, $chipW, $chipH)
        $chipBrush.Dispose()

        # 칩 테두리
        $chipPen = [System.Drawing.Pen]::new($accent, [math]::Max(1, $sz / 24))
        $g.DrawRectangle($chipPen, $chipX, $chipY, $chipW, $chipH)
        $chipPen.Dispose()

        # 내부 격자
        $gridPen = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(80, 0, 200, 224), 1)
        $g.DrawLine($gridPen, $chipX + $chipW * 0.35, $chipY + 2, $chipX + $chipW * 0.35, $chipY + $chipH - 2)
        $g.DrawLine($gridPen, $chipX + $chipW * 0.65, $chipY + 2, $chipX + $chipW * 0.65, $chipY + $chipH - 2)
        $g.DrawLine($gridPen, $chipX + 2, $chipY + $chipH * 0.35, $chipX + $chipW - 2, $chipY + $chipH * 0.35)
        $g.DrawLine($gridPen, $chipX + 2, $chipY + $chipH * 0.65, $chipX + $chipW - 2, $chipY + $chipH * 0.65)
        $gridPen.Dispose()

        # 핀 (상/하/좌/우)
        $pinPen = [System.Drawing.Pen]::new($accent, $pinW)
        $numPins = 3
        for ($i = 0; $i -lt $numPins; $i++) {
            $t  = ($i + 1.0) / ($numPins + 1)
            $px = $chipX + $chipW * $t
            $py = $chipY + $chipH * $t
            $g.DrawLine($pinPen, $px, $chipY, $px, $chipY - $pinLen)
            $g.DrawLine($pinPen, $px, $chipY + $chipH, $px, $chipY + $chipH + $pinLen)
            $g.DrawLine($pinPen, $chipX, $py, $chipX - $pinLen, $py)
            $g.DrawLine($pinPen, $chipX + $chipW, $py, $chipX + $chipW + $pinLen, $py)
        }
        $pinPen.Dispose()

        # 중앙 점
        $dotR    = $sz * 0.055
        $dotBrush = [System.Drawing.SolidBrush]::new($accent)
        $g.FillEllipse($dotBrush, $cx - $dotR, $cy - $dotR, $dotR * 2, $dotR * 2)
        $dotBrush.Dispose()

    } else {
        # 16px: 십자
        $crossPen = [System.Drawing.Pen]::new($accent, 2)
        $m = $sz / 2
        $g.DrawLine($crossPen, $m, 3, $m, $sz - 3)
        $g.DrawLine($crossPen, 3, $m, $sz - 3, $m)
        $crossPen.Dispose()
    }

    $g.Dispose()
    $bitmaps.Add($bmp)
}

# ICO 파일 저장
$ms     = [System.IO.MemoryStream]::new()
$writer = [System.IO.BinaryWriter]::new($ms)
$writer.Write([uint16]0)
$writer.Write([uint16]1)
$writer.Write([uint16]$sizes.Count)

$imgStreams = [System.Collections.Generic.List[System.IO.MemoryStream]]::new()
foreach ($bmp in $bitmaps) {
    $ims = [System.IO.MemoryStream]::new()
    $bmp.Save($ims, [System.Drawing.Imaging.ImageFormat]::Png)
    $imgStreams.Add($ims)
}

$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $szVal = if ($sizes[$i] -ge 256) { 0 } else { $sizes[$i] }
    $writer.Write([byte]$szVal)
    $writer.Write([byte]$szVal)
    $writer.Write([byte]0)
    $writer.Write([byte]0)
    $writer.Write([uint16]1)
    $writer.Write([uint16]32)
    $writer.Write([uint32]$imgStreams[$i].Length)
    $writer.Write([uint32]$offset)
    $offset += $imgStreams[$i].Length
}
foreach ($ims in $imgStreams) { $writer.Write($ims.ToArray()) }

[System.IO.File]::WriteAllBytes($icoPath, $ms.ToArray())
Write-Host "아이콘 생성 완료: $icoPath"
foreach ($bmp in $bitmaps) { $bmp.Dispose() }
