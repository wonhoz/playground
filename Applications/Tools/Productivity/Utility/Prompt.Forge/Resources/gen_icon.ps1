Add-Type -AssemblyName System.Drawing

function New-PromptForgeIcon {
    param([int]$sz)
    $bmp = New-Object System.Drawing.Bitmap($sz, $sz)
    $g   = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
    $g.Clear([System.Drawing.Color]::Transparent)

    # 배경 둥근 사각형
    $bg  = [System.Drawing.Color]::FromArgb(255, 19, 19, 31)
    $bgb = New-Object System.Drawing.SolidBrush($bg)
    $r   = [int]($sz * 0.18)
    $g.FillRectangle($bgb, 0, 0, $sz, $sz)

    # 퍼플 액센트 상단 바
    $acc  = [System.Drawing.Color]::FromArgb(255, 124, 58, 237)
    $accb = New-Object System.Drawing.SolidBrush($acc)
    $barH = [int]($sz * 0.12)
    $g.FillRectangle($accb, 0, 0, $sz, $barH)

    # 텍스트 라인 (프롬프트 표현)
    $linePen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255, 160, 160, 210), [float]([int]($sz / 20)))
    $lm = [int]($sz * 0.15)
    $lineY1 = [int]($sz * 0.42)
    $lineY2 = [int]($sz * 0.58)
    $lineY3 = [int]($sz * 0.74)
    $lw1 = [int]($sz * 0.7)
    $lw2 = [int]($sz * 0.5)
    $lw3 = [int]($sz * 0.35)
    $g.DrawLine($linePen, $lm, $lineY1, $lm + $lw1, $lineY1)
    $g.DrawLine($linePen, $lm, $lineY2, $lm + $lw2, $lineY2)
    $g.DrawLine($linePen, $lm, $lineY3, $lm + $lw3, $lineY3)

    # 커서 (보라색 점)
    $cursorBrush = New-Object System.Drawing.SolidBrush($acc)
    $cw = [int]($sz * 0.06)
    $g.FillRectangle($cursorBrush, $lm + $lw3 + [int]($sz*0.02), $lineY3 - [int]($sz*0.05), $cw, [int]($sz*0.13))

    $g.Dispose()
    $bgb.Dispose()
    $accb.Dispose()
    $linePen.Dispose()
    $cursorBrush.Dispose()
    return $bmp
}

$sizes   = @(256, 48, 32, 16)
$bitmaps = @{}
foreach ($sz in $sizes) {
    $bitmaps[$sz] = New-PromptForgeIcon -sz $sz
}

$outPath = Join-Path $PSScriptRoot "app.ico"
$ms      = New-Object System.IO.MemoryStream
$writer  = New-Object System.IO.BinaryWriter($ms)

$writer.Write([uint16]0)
$writer.Write([uint16]1)
$writer.Write([uint16]$sizes.Count)

$imgStreams = @{}
foreach ($sz in $sizes) {
    $s = New-Object System.IO.MemoryStream
    $bitmaps[$sz].Save($s, [System.Drawing.Imaging.ImageFormat]::Png)
    $imgStreams[$sz] = $s
}

$dirSize = 6 + 16 * $sizes.Count
$offset  = $dirSize

foreach ($sz in $sizes) {
    $imgData = $imgStreams[$sz].ToArray()
    $w = if ($sz -ge 256) { 0 } else { $sz }
    $h = if ($sz -ge 256) { 0 } else { $sz }
    $writer.Write([byte]$w)
    $writer.Write([byte]$h)
    $writer.Write([byte]0)
    $writer.Write([byte]0)
    $writer.Write([uint16]1)
    $writer.Write([uint16]32)
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
