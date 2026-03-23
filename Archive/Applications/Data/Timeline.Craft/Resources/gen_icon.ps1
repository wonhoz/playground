Add-Type -AssemblyName System.Drawing

function New-TimelineIcon {
    param([int]$sz)
    $bmp = New-Object System.Drawing.Bitmap($sz, $sz)
    $g   = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.Clear([System.Drawing.Color]::Transparent)

    # 배경
    $bgb = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255,19,19,31))
    $g.FillRectangle($bgb, 0, 0, $sz, $sz)

    $pad = [int]($sz * 0.1)
    $w   = $sz - $pad * 2
    $h   = $sz - $pad * 2

    # 시간축 수평선
    $axisY = $pad + [int]($h * 0.25)
    $axisPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255,80,80,120), [float]([int]($sz/24)))
    $g.DrawLine($axisPen, $pad, $axisY, $sz - $pad, $axisY)

    # 이벤트 블록 1 (파랑)
    $blueB = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255,59,130,246))
    $r1x = $pad + [int]($w * 0.05)
    $r1y = $axisY + [int]($h * 0.08)
    $r1w = [int]($w * 0.45)
    $r1h = [int]($h * 0.2)
    $g.FillRectangle($blueB, $r1x, $r1y, $r1w, $r1h)

    # 이벤트 블록 2 (녹색, 두 번째 레인)
    $greenB = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255,16,185,129))
    $r2x = $pad + [int]($w * 0.3)
    $r2y = $r1y + [int]($h * 0.28)
    $r2w = [int]($w * 0.55)
    $r2h = [int]($h * 0.2)
    $g.FillRectangle($greenB, $r2x, $r2y, $r2w, $r2h)

    # 마일스톤 다이아몬드 (주황)
    $milX = $pad + [int]($w * 0.5)
    $milY = $r2y + [int]($h * 0.32)
    $milR = [int]($sz * 0.07)
    $milPts = @(
        [System.Drawing.Point]::new($milX, $milY - $milR),
        [System.Drawing.Point]::new($milX + $milR, $milY),
        [System.Drawing.Point]::new($milX, $milY + $milR),
        [System.Drawing.Point]::new($milX - $milR, $milY)
    )
    $orangeB = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255,245,158,11))
    $g.FillPolygon($orangeB, $milPts)

    $g.Dispose(); $bgb.Dispose(); $axisPen.Dispose()
    $blueB.Dispose(); $greenB.Dispose(); $orangeB.Dispose()
    return $bmp
}

$sizes = @(256, 48, 32, 16)
$bitmaps = @{}
foreach ($sz in $sizes) { $bitmaps[$sz] = New-TimelineIcon -sz $sz }

$outPath = Join-Path $PSScriptRoot "app.ico"
$ms = New-Object System.IO.MemoryStream
$writer = New-Object System.IO.BinaryWriter($ms)
$writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$sizes.Count)

$imgStreams = @{}
foreach ($sz in $sizes) {
    $s = New-Object System.IO.MemoryStream
    $bitmaps[$sz].Save($s, [System.Drawing.Imaging.ImageFormat]::Png)
    $imgStreams[$sz] = $s
}

$offset = 6 + 16 * $sizes.Count
foreach ($sz in $sizes) {
    $imgData = $imgStreams[$sz].ToArray()
    $w = if ($sz -ge 256) { 0 } else { $sz }
    $h = if ($sz -ge 256) { 0 } else { $sz }
    $writer.Write([byte]$w); $writer.Write([byte]$h)
    $writer.Write([byte]0); $writer.Write([byte]0)
    $writer.Write([uint16]1); $writer.Write([uint16]32)
    $writer.Write([uint32]$imgData.Length); $writer.Write([uint32]$offset)
    $offset += $imgData.Length
}
foreach ($sz in $sizes) { $writer.Write($imgStreams[$sz].ToArray()) }

[System.IO.File]::WriteAllBytes($outPath, $ms.ToArray())
Write-Host "app.ico 생성 완료: $outPath"
foreach ($sz in $sizes) { $bitmaps[$sz].Dispose() }
