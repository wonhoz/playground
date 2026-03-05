Add-Type -AssemblyName System.Drawing

function Make-Bitmap {
    param([int]$sz)
    $bmp = New-Object System.Drawing.Bitmap($sz, $sz)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit

    $bgBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 0x1A, 0x1A, 0x2E))
    $g.FillRectangle($bgBrush, 0, 0, $sz, $sz)

    $pageBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 0xE8, 0xE8, 0xF0))
    $px = [int]($sz * 0.22)
    $py = [int]($sz * 0.12)
    $pw = [int]($sz * 0.56)
    $ph = [int]($sz * 0.76)
    $g.FillRectangle($pageBrush, $px, $py, $pw, $ph)

    $foldSz = [int]($sz * 0.16)
    $foldBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 0x5B, 0x8F, 0xFF))
    $p1 = [System.Drawing.Point]::new($px + $pw - $foldSz, $py)
    $p2 = [System.Drawing.Point]::new($px + $pw, $py + $foldSz)
    $p3 = [System.Drawing.Point]::new($px + $pw - $foldSz, $py + $foldSz)
    $pts = [System.Drawing.Point[]]@($p1, $p2, $p3)
    $g.FillPolygon($foldBrush, $pts)

    if ($sz -ge 32) {
        $fontSize = [float]($sz * 0.18)
        $font = New-Object System.Drawing.Font("Arial", $fontSize, [System.Drawing.FontStyle]::Bold)
        $textBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 0x5B, 0x8F, 0xFF))
        $sf = New-Object System.Drawing.StringFormat
        $sf.Alignment = [System.Drawing.StringAlignment]::Center
        $sf.LineAlignment = [System.Drawing.StringAlignment]::Center
        $rect = New-Object System.Drawing.RectangleF($px, ($py + $ph * 0.45), $pw, ($ph * 0.3))
        $g.DrawString("PDF", $font, $textBrush, $rect, $sf)
        $font.Dispose()
        $textBrush.Dispose()
        $sf.Dispose()
    }

    $g.Dispose()
    $bgBrush.Dispose()
    $pageBrush.Dispose()
    $foldBrush.Dispose()
    return $bmp
}

$sizes = @(16, 32, 48, 256)
$pngDataList = New-Object System.Collections.Generic.List[byte[]]

foreach ($sz in $sizes) {
    $bmp = Make-Bitmap -sz $sz
    $s = New-Object System.IO.MemoryStream
    $bmp.Save($s, [System.Drawing.Imaging.ImageFormat]::Png)
    $pngDataList.Add($s.ToArray())
    $s.Dispose()
    $bmp.Dispose()
}

$outPath = Join-Path $PSScriptRoot "app.ico"
$ms = New-Object System.IO.MemoryStream
$writer = New-Object System.IO.BinaryWriter($ms)

$writer.Write([uint16]0)
$writer.Write([uint16]1)
$writer.Write([uint16]$sizes.Count)

$dataOffset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $sz = $sizes[$i]
    if ($sz -ge 256) {
        $szVal = 0
    } else {
        $szVal = $sz
    }
    $writer.Write([byte]$szVal)
    $writer.Write([byte]$szVal)
    $writer.Write([byte]0)
    $writer.Write([byte]0)
    $writer.Write([uint16]1)
    $writer.Write([uint16]32)
    $writer.Write([uint32]$pngDataList[$i].Length)
    $writer.Write([uint32]$dataOffset)
    $dataOffset += $pngDataList[$i].Length
}

for ($i = 0; $i -lt $pngDataList.Count; $i++) {
    $writer.Write($pngDataList[$i])
}

[System.IO.File]::WriteAllBytes($outPath, $ms.ToArray())
$writer.Dispose()
$ms.Dispose()

Write-Host "app.ico 생성 완료: $outPath"
