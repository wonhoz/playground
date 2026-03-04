Add-Type -AssemblyName System.Drawing

function Make-ClothBitmap([int]$sz) {
    $bmp = New-Object System.Drawing.Bitmap($sz, $sz)
    $g   = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.Clear([System.Drawing.Color]::FromArgb(26, 26, 46))

    $pad  = [int]($sz * 0.06)
    $cloth = [System.Drawing.Color]::FromArgb(212, 165, 116)   # 베이지 천
    $cut   = [System.Drawing.Color]::FromArgb(255, 80,  80)    # 절단선 빨강
    $pin   = [System.Drawing.Color]::FromArgb(255, 215, 0)     # 핀 골드

    # 천 격자 (간단하게 가로선 몇 줄)
    $pCloth = New-Object System.Drawing.Pen($cloth, [float]([Math]::Max(1, $sz * 0.03)))
    $rows = 4
    $cols = 5
    $cw = [int](($sz - $pad*2) / ($cols - 1))
    $ch = [int](($sz - $pad*2) * 0.55 / ($rows - 1))
    $startY = $pad

    # 격자 선 (가로)
    for ($r = 0; $r -lt $rows; $r++) {
        $y = $startY + $r * $ch
        $g.DrawLine($pCloth, $pad, $y, $sz - $pad, $y)
    }
    # 격자 선 (세로) - 살짝 처진 느낌
    for ($c = 0; $c -lt $cols; $c++) {
        $x = $pad + $c * $cw
        $sag = [int]($sz * 0.04 * [Math]::Sin([Math]::PI * $c / ($cols - 1)))
        $g.DrawLine($pCloth, $x, $startY + $sag, $x, $startY + ($rows-1)*$ch + $sag)
    }
    $pCloth.Dispose()

    # 절단선 (대각선)
    $pCut = New-Object System.Drawing.Pen($cut, [float]([Math]::Max(1.5, $sz * 0.035)))
    $pCut.DashStyle = [System.Drawing.Drawing2D.DashStyle]::Dash
    $midY = $startY + [int](($rows-1) * $ch * 0.5)
    $g.DrawLine($pCut, $pad, $midY, $sz - $pad, $midY)
    $pCut.Dispose()

    # 핀 (상단 점들)
    $pinSize = [int]([Math]::Max(3, $sz * 0.08))
    $bPin = New-Object System.Drawing.SolidBrush($pin)
    foreach ($c in @(1, 3)) {
        $x = $pad + $c * $cw - $pinSize / 2
        $g.FillEllipse($bPin, $x, $startY - $pinSize/2, $pinSize, $pinSize)
    }
    $bPin.Dispose()

    # 하단 저울 (간단한 사다리꼴 2개)
    $scaleY = $startY + ($rows-1)*$ch + [int]($sz * 0.12)
    $sw     = [int]($sz * 0.25)
    $sh     = [int]($sz * 0.12)
    $bScale = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(100, 100, 140))
    $g.FillRectangle($bScale, $pad, $scaleY, $sw, $sh)
    $g.FillRectangle($bScale, $sz - $pad - $sw, $scaleY, $sw, $sh)
    $bScale.Dispose()

    $g.Dispose()
    return $bmp
}

$sizes   = @(16, 32, 48, 256)
$bitmaps = @()
foreach ($s in $sizes) { $bitmaps += Make-ClothBitmap $s }

$outPath = Join-Path $PSScriptRoot "app.ico"
$stream  = [System.IO.File]::Create($outPath)
$writer  = New-Object System.IO.BinaryWriter($stream)
$count   = $bitmaps.Count

$writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$count)

$pngArrays = @()
foreach ($bmp in $bitmaps) {
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $pngArrays += ,$ms.ToArray(); $ms.Dispose()
}

$offset = 6 + 16 * $count
for ($i = 0; $i -lt $count; $i++) {
    $sz  = $sizes[$i]; $byt = $pngArrays[$i]
    $w   = if ($sz -ge 256) { 0 } else { $sz }
    $h   = if ($sz -ge 256) { 0 } else { $sz }
    $writer.Write([byte]$w); $writer.Write([byte]$h)
    $writer.Write([byte]0);  $writer.Write([byte]0)
    $writer.Write([uint16]1); $writer.Write([uint16]32)
    $writer.Write([uint32]$byt.Length); $writer.Write([uint32]$offset)
    $offset += $byt.Length
}
foreach ($byt in $pngArrays) { $writer.Write($byt) }
$writer.Close(); $stream.Close()
foreach ($bmp in $bitmaps) { $bmp.Dispose() }
Write-Host "app.ico 생성 완료: $outPath"
