Add-Type -AssemblyName System.Drawing

function New-TrayStatsIcon {
    param([int]$sz)
    $bmp = New-Object System.Drawing.Bitmap($sz, $sz)
    $g   = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.Clear([System.Drawing.Color]::Transparent)

    # 배경
    $bg = [System.Drawing.Color]::FromArgb(255, 19, 19, 31)
    $bgb = New-Object System.Drawing.SolidBrush($bg)
    $g.FillEllipse($bgb, 1, 1, $sz-2, $sz-2)

    # 호 (CPU 60%)
    $arc = [System.Drawing.Color]::FromArgb(255, 74, 222, 128)
    $pen = New-Object System.Drawing.Pen($arc, [float]($sz / 8))
    $margin = [int]($sz * 0.16)
    $g.DrawArc($pen, $margin, $margin, $sz-$margin*2, $sz-$margin*2, -90.0, 216.0)

    # 중앙 텍스트
    $font  = New-Object System.Drawing.Font("Segoe UI", [float]($sz / 5), [System.Drawing.FontStyle]::Bold)
    $brush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 224, 224, 224))
    $fmt   = New-Object System.Drawing.StringFormat
    $fmt.Alignment     = [System.Drawing.StringAlignment]::Center
    $fmt.LineAlignment = [System.Drawing.StringAlignment]::Center
    $g.DrawString("%", $font, $brush, [System.Drawing.RectangleF]::new(0, 0, $sz, $sz), $fmt)

    $g.Dispose()
    $bgb.Dispose()
    $pen.Dispose()
    $brush.Dispose()
    $font.Dispose()
    return $bmp
}

$sizes   = @(256, 48, 32, 16)
$bitmaps = @{}
foreach ($sz in $sizes) {
    $bitmaps[$sz] = New-TrayStatsIcon -sz $sz
}

$outPath = Join-Path $PSScriptRoot "app.ico"
$ms      = New-Object System.IO.MemoryStream

# ICO 헤더
$writer = New-Object System.IO.BinaryWriter($ms)
$writer.Write([uint16]0)       # reserved
$writer.Write([uint16]1)       # type: ICO
$writer.Write([uint16]$sizes.Count)

$imgStreams = @{}
foreach ($sz in $sizes) {
    $s = New-Object System.IO.MemoryStream
    $bitmaps[$sz].Save($s, [System.Drawing.Imaging.ImageFormat]::Png)
    $imgStreams[$sz] = $s
}

# 디렉터리 오프셋 계산
$dirSize    = 6 + 16 * $sizes.Count
$offset     = $dirSize

foreach ($sz in $sizes) {
    $imgData = $imgStreams[$sz].ToArray()
    $w = if ($sz -ge 256) { 0 } else { $sz }
    $h = if ($sz -ge 256) { 0 } else { $sz }
    $writer.Write([byte]$w)
    $writer.Write([byte]$h)
    $writer.Write([byte]0)     # color count
    $writer.Write([byte]0)     # reserved
    $writer.Write([uint16]1)   # planes
    $writer.Write([uint16]32)  # bit count
    $writer.Write([uint32]$imgData.Length)
    $writer.Write([uint32]$offset)
    $offset += $imgData.Length
}

foreach ($sz in $sizes) {
    $writer.Write($imgStreams[$sz].ToArray())
}

[System.IO.File]::WriteAllBytes($outPath, $ms.ToArray())
Write-Host "app.ico 생성 완료: $outPath"

foreach ($sz in $sizes) { $bitmaps[$sz].Dispose() }
