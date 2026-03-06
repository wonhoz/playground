Add-Type -AssemblyName System.Drawing

function Make-Bitmap {
    param([int]$sz)

    $bmp = New-Object System.Drawing.Bitmap($sz, $sz)
    $g   = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode   = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic

    # 배경 #111118
    $g.Clear([System.Drawing.Color]::FromArgb(0xFF, 0x11, 0x11, 0x18))

    $cx   = $sz / 2.0
    $cy   = $sz / 2.0

    # 스피커 본체 (보라색 #A855F7)
    $accent = [System.Drawing.Color]::FromArgb(0xFF, 0xA8, 0x55, 0xF7)
    $dark   = [System.Drawing.Color]::FromArgb(0xFF, 0x16, 0x16, 0x1F)
    $brushAcc = New-Object System.Drawing.SolidBrush($accent)
    $brushDark = New-Object System.Drawing.SolidBrush($dark)
    $penAcc = New-Object System.Drawing.Pen($accent, [float]($sz * 0.055))

    $pad = $sz * 0.12

    # 스피커 사각형 본체
    $spW = $sz * 0.25
    $spH = $sz * 0.38
    $spX = $cx - $spW * 1.5
    $spY = $cy - $spH / 2
    $spRect = [System.Drawing.RectangleF]::new($spX, $spY, $spW, $spH)
    $g.FillRectangle($brushAcc, $spRect)

    # 스피커 콘 (삼각형)
    $conePts = @(
        [System.Drawing.PointF]::new($spX + $spW, $cy - $spH * 0.6),
        [System.Drawing.PointF]::new($spX + $spW, $cy + $spH * 0.6),
        [System.Drawing.PointF]::new($cx + $sz * 0.06, $cy + $spH * 0.25),
        [System.Drawing.PointF]::new($cx + $sz * 0.06, $cy - $spH * 0.25)
    )
    $g.FillPolygon($brushAcc, $conePts)

    # 음파 (호 3개)
    $waveR1 = $sz * 0.28
    $waveR2 = $sz * 0.38
    $waveR3 = $sz * 0.48
    $arcX1 = $cx + $sz * 0.06 - $waveR1
    $arcY1 = $cy - $waveR1
    $arcX2 = $cx + $sz * 0.06 - $waveR2
    $arcY2 = $cy - $waveR2
    $arcX3 = $cx + $sz * 0.06 - $waveR3
    $arcY3 = $cy - $waveR3

    $penWave1 = New-Object System.Drawing.Pen($accent, [float]($sz * 0.055))
    $penWave1.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $penWave1.EndCap   = [System.Drawing.Drawing2D.LineCap]::Round
    $penWave2 = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(0xCC, 0xA8, 0x55, 0xF7), [float]($sz * 0.045))
    $penWave2.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $penWave2.EndCap   = [System.Drawing.Drawing2D.LineCap]::Round
    $penWave3 = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(0x88, 0xA8, 0x55, 0xF7), [float]($sz * 0.04))
    $penWave3.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $penWave3.EndCap   = [System.Drawing.Drawing2D.LineCap]::Round

    $g.DrawArc($penWave1, $arcX1, $arcY1, $waveR1 * 2, $waveR1 * 2, -45, 90)
    $g.DrawArc($penWave2, $arcX2, $arcY2, $waveR2 * 2, $waveR2 * 2, -50, 100)
    $g.DrawArc($penWave3, $arcX3, $arcY3, $waveR3 * 2, $waveR3 * 2, -55, 110)

    $g.Dispose()
    return $bmp
}

$sizes  = @(16, 32, 48, 256)
$outPath = Join-Path $PSScriptRoot "app.ico"

$pngs = @{}
foreach ($sz in $sizes) {
    $bmp   = Make-Bitmap $sz
    $pngMs = New-Object System.IO.MemoryStream
    $bmp.Save($pngMs, [System.Drawing.Imaging.ImageFormat]::Png)
    $pngs[$sz] = $pngMs.ToArray()
    $pngMs.Dispose()
    $bmp.Dispose()
}

$ms     = New-Object System.IO.MemoryStream
$writer = New-Object System.IO.BinaryWriter($ms)

$writer.Write([uint16]0)
$writer.Write([uint16]1)
$writer.Write([uint16]$sizes.Count)

$offset = 6 + 16 * $sizes.Count

foreach ($sz in $sizes) {
    $data = $pngs[$sz]
    $w    = if ($sz -ge 256) { 0 } else { $sz }
    $h    = if ($sz -ge 256) { 0 } else { $sz }
    $writer.Write([byte]$w)
    $writer.Write([byte]$h)
    $writer.Write([byte]0)
    $writer.Write([byte]0)
    $writer.Write([uint16]1)
    $writer.Write([uint16]32)
    $writer.Write([uint32]$data.Length)
    $writer.Write([uint32]$offset)
    $offset += $data.Length
}
foreach ($sz in $sizes) { $writer.Write($pngs[$sz]) }

$writer.Flush()
[System.IO.File]::WriteAllBytes($outPath, $ms.ToArray())
$ms.Dispose()

Write-Host "app.ico 생성 완료: $outPath"
