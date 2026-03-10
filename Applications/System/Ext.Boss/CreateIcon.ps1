Add-Type -AssemblyName System.Drawing

function Make-Icon {
    param([int]$sz)
    $bmp = New-Object System.Drawing.Bitmap($sz, $sz)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'

    # 배경
    $g.Clear([System.Drawing.Color]::FromArgb(18, 19, 28))

    # 패널 배경
    $panelBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(26, 30, 46))
    $pad = [int]($sz * 0.08)
    $g.FillRectangle($panelBrush, $pad, $pad, $sz - $pad*2, $sz - $pad*2)

    $fw = [int]($sz * 0.28)
    $fh = [int]($sz * 0.36)
    $fx = [int]($sz * 0.12)
    $fy = [int]($sz * 0.32)

    # 왼쪽 파일 박스 (파란색)
    $filePen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(74, 158, 255), [int]([Math]::Max(1,$sz*0.05)))
    $g.DrawRectangle($filePen, $fx, $fy, $fw, $fh)

    # 파일 안 줄
    $linePen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(60, 74, 158, 255), [int]([Math]::Max(1,$sz*0.04)))
    $lx1 = $fx + [int]($sz*0.05); $lx2 = $fx + $fw - [int]($sz*0.05)
    $g.DrawLine($linePen, $lx1, $fy+[int]($sz*0.12), $lx2, $fy+[int]($sz*0.12))
    $g.DrawLine($linePen, $lx1, $fy+[int]($sz*0.20), $lx2, $fy+[int]($sz*0.20))
    $g.DrawLine($linePen, $lx1, $fy+[int]($sz*0.28), [int]($lx1+($lx2-$lx1)*0.6), $fy+[int]($sz*0.28))

    # 가운데 화살표 (노란색)
    $arrowPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255, 184, 64), [int]([Math]::Max(2,$sz*0.07)))
    $arrowPen.EndCap = 'ArrowAnchor'
    $cy = [int]($sz * 0.5)
    $g.DrawLine($arrowPen, $fx+$fw+[int]($sz*0.04), $cy, $sz-$fx-$fw-[int]($sz*0.06), $cy)

    # 오른쪽 앱 박스 (초록색)
    $greenPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(80, 224, 128), [int]([Math]::Max(1,$sz*0.05)))
    $ax = $sz - $fx - $fw
    $g.DrawRectangle($greenPen, $ax, $fy, $fw, $fh)

    # 앱 박스 안 원 (기어 상징)
    $greenBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(80, 224, 128))
    $gr = [int]([Math]::Max(2,$sz * 0.07))
    $gcx = $ax + [int]($fw/2)
    $gcy = $fy + [int]($fh/2)
    $g.FillEllipse($greenBrush, $gcx-$gr, $gcy-$gr, $gr*2, $gr*2)

    $g.Dispose()
    return $bmp
}

$sizes = @(256, 48, 32, 16)
$bitmaps = $sizes | ForEach-Object { Make-Icon $_ }

$outPath = 'C:\Users\admin\source\repos\+Playground\Applications\System\Ext.Boss\Resources\app.ico'
$ms = New-Object System.IO.MemoryStream
$writer = New-Object System.IO.BinaryWriter($ms)

$writer.Write([uint16]0)
$writer.Write([uint16]1)
$writer.Write([uint16]$sizes.Count)

$imgDataList = @()
foreach ($bmp in $bitmaps) {
    $imgMs = New-Object System.IO.MemoryStream
    $bmp.Save($imgMs, [System.Drawing.Imaging.ImageFormat]::Png)
    $imgDataList += ,($imgMs.ToArray())
    $imgMs.Dispose()
}

$headerSize = 6
$dirEntrySize = 16 * $sizes.Count
$dataOffset = $headerSize + $dirEntrySize

foreach ($i in 0..($sizes.Count-1)) {
    $sz = $sizes[$i]
    $data = $imgDataList[$i]
    $w = if ($sz -ge 256) { 0 } else { $sz }
    $h = if ($sz -ge 256) { 0 } else { $sz }
    $writer.Write([byte]$w)
    $writer.Write([byte]$h)
    $writer.Write([byte]0)
    $writer.Write([byte]0)
    $writer.Write([uint16]1)
    $writer.Write([uint16]32)
    $writer.Write([uint32]$data.Length)
    $writer.Write([uint32]$dataOffset)
    $dataOffset += $data.Length
}

foreach ($data in $imgDataList) {
    $writer.Write($data)
}

[System.IO.File]::WriteAllBytes($outPath, $ms.ToArray())
$writer.Dispose()
$ms.Dispose()
foreach ($bmp in $bitmaps) { $bmp.Dispose() }

Write-Host "아이콘 생성 완료: $outPath"
