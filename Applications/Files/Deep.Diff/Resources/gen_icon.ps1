Add-Type -AssemblyName System.Drawing

function New-Bitmap($size) {
    $bmp = New-Object System.Drawing.Bitmap($size, $size)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.Clear([System.Drawing.Color]::FromArgb(255, 14, 14, 28))

    $p = [int]($size * 0.08)
    $w = $size - $p * 2
    $h = $size - $p * 2

    # Left panel (blue)
    $lb = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(220, 60, 110, 220))
    $g.FillRectangle($lb, $p, $p, [int]($w * 0.40), $h)

    # Right panel (green)
    $rb = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(220, 40, 185, 105))
    $rx = $p + [int]($w * 0.60)
    $g.FillRectangle($rb, $rx, $p, [int]($w * 0.40), $h)

    # Center diff arrows
    $ap = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255, 255, 195, 50), [float]([int]($size * 0.07)))
    $cx = $size / 2
    $y1 = [int]($size * 0.35)
    $y2 = [int]($size * 0.65)
    $ax = [int]($size * 0.10)
    $g.DrawLine($ap, ($cx - $ax), $y1, ($cx + $ax), $y1)
    $g.DrawLine($ap, ($cx + $ax), $y2, ($cx - $ax), $y2)

    # Arrow heads
    $ab = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 255, 195, 50))
    $aw = [int]($size * 0.07)
    $ah = [int]($size * 0.07)
    # Right arrow head (line 1 -> right)
    $pts1 = @(
        [System.Drawing.Point]::new($cx + $ax, $y1),
        [System.Drawing.Point]::new($cx + $ax - $aw, $y1 - $ah),
        [System.Drawing.Point]::new($cx + $ax - $aw, $y1 + $ah)
    )
    $g.FillPolygon($ab, $pts1)
    # Left arrow head (line 2 -> left)
    $pts2 = @(
        [System.Drawing.Point]::new($cx - $ax, $y2),
        [System.Drawing.Point]::new($cx - $ax + $aw, $y2 - $ah),
        [System.Drawing.Point]::new($cx - $ax + $aw, $y2 + $ah)
    )
    $g.FillPolygon($ab, $pts2)

    $g.Dispose()
    return $bmp
}

$outDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$icoPath = Join-Path $outDir "app.ico"
$sizes = @(16, 32, 48, 256)
$pngData = @{}
foreach ($sz in $sizes) {
    $bmp = New-Bitmap $sz
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $pngData[$sz] = $ms.ToArray()
    $ms.Close()
    $bmp.Dispose()
}

$stream = [System.IO.File]::OpenWrite($icoPath)
$writer = New-Object System.IO.BinaryWriter($stream)
$count = $sizes.Count
$writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$count)
$offset = 6 + 16 * $count
foreach ($sz in $sizes) {
    $ww = if ($sz -ge 256) { 0 } else { $sz }
    $writer.Write([byte]$ww); $writer.Write([byte]$ww)
    $writer.Write([byte]0); $writer.Write([byte]0)
    $writer.Write([uint16]1); $writer.Write([uint16]32)
    $writer.Write([uint32]$pngData[$sz].Length)
    $writer.Write([uint32]$offset)
    $offset += $pngData[$sz].Length
}
foreach ($sz in $sizes) { $writer.Write($pngData[$sz]) }
$writer.Close(); $stream.Close()
Write-Host "아이콘 생성 완료: $icoPath"
