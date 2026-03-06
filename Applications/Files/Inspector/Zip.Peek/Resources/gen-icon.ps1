Add-Type -AssemblyName System.Drawing

function Make-Bitmap {
    param([int]$sz)
    $bmp = New-Object System.Drawing.Bitmap($sz, $sz)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit

    # 배경 #111118
    $bgBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 0x11, 0x11, 0x18))
    $g.FillRectangle($bgBrush, 0, 0, $sz, $sz)

    # 지퍼 모양 아카이브 박스
    $boxBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 0x1C, 0x1C, 0x28))
    $bx = [int]($sz * 0.15)
    $by = [int]($sz * 0.10)
    $bw = [int]($sz * 0.70)
    $bh = [int]($sz * 0.80)
    $g.FillRectangle($boxBrush, $bx, $by, $bw, $bh)

    # 테두리
    $penBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 0xF5, 0x9E, 0x0B))
    $pen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255, 0xF5, 0x9E, 0x0B), [float]([Math]::Max(1, $sz * 0.04)))
    $g.DrawRectangle($pen, $bx, $by, $bw, $bh)

    # 지퍼 줄 (중앙 세로선)
    if ($sz -ge 24) {
        $cx = $bx + $bw / 2
        $zy = $by + [int]($bh * 0.15)
        $zh = [int]($bh * 0.70)
        $zipPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255, 0xF5, 0x9E, 0x0B), [float]([Math]::Max(1, $sz * 0.06)))
        $g.DrawLine($zipPen, $cx, $zy, $cx, $zy + $zh)
        $zipPen.Dispose()

        # 지퍼 톱니 (작은 가로선들)
        if ($sz -ge 32) {
            $toothPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(180, 0xF5, 0x9E, 0x0B), [float]([Math]::Max(1, $sz * 0.03)))
            $tw = [int]($sz * 0.10)
            $gap = [int]($bh * 0.12)
            for ($yi = 0; $yi -lt 5; $yi++) {
                $ty = $zy + $gap * $yi
                $g.DrawLine($toothPen, $cx - $tw, $ty, $cx + $tw, $ty)
            }
            $toothPen.Dispose()
        }
    }

    $g.Dispose()
    $bgBrush.Dispose()
    $boxBrush.Dispose()
    $penBrush.Dispose()
    $pen.Dispose()
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
    if ($sz -ge 256) { $szVal = 0 } else { $szVal = $sz }
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
