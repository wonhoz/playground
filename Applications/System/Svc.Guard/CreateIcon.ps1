Add-Type -AssemblyName System.Drawing

function Make-Icon {
    param([int]$sz)
    $bmp = New-Object System.Drawing.Bitmap($sz, $sz)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'

    # 배경
    $g.Clear([System.Drawing.Color]::FromArgb(18, 19, 28))

    $cx = [int]($sz * 0.5)
    $cy = [int]($sz * 0.5)
    $r  = [int]($sz * 0.40)

    # 방패 모양 배경
    $shieldBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(26, 42, 64))
    $shieldPts = @(
        [System.Drawing.PointF]::new($cx, $cy - $r),
        [System.Drawing.PointF]::new($cx + $r, $cy - [int]($r * 0.5)),
        [System.Drawing.PointF]::new($cx + $r, $cy + [int]($r * 0.2)),
        [System.Drawing.PointF]::new($cx, $cy + $r),
        [System.Drawing.PointF]::new($cx - $r, $cy + [int]($r * 0.2)),
        [System.Drawing.PointF]::new($cx - $r, $cy - [int]($r * 0.5))
    )
    $g.FillPolygon($shieldBrush, $shieldPts)

    # 방패 테두리 (파란색)
    $shieldPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(74, 158, 255), [int]([Math]::Max(2, $sz * 0.06)))
    $g.DrawPolygon($shieldPen, $shieldPts)

    # 체크 마크
    $checkPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(80, 224, 128), [int]([Math]::Max(2, $sz * 0.08)))
    $checkPen.StartCap = 'Round'
    $checkPen.EndCap   = 'Round'
    $checkPen.LineJoin = 'Round'
    $p1 = [System.Drawing.PointF]::new($cx - [int]($r*0.35), $cy)
    $p2 = [System.Drawing.PointF]::new($cx - [int]($r*0.05), $cy + [int]($r*0.30))
    $p3 = [System.Drawing.PointF]::new($cx + [int]($r*0.35), $cy - [int]($r*0.25))
    $g.DrawLines($checkPen, @($p1, $p2, $p3))

    $g.Dispose()
    return $bmp
}

$sizes = @(256, 48, 32, 16)
$bitmaps = $sizes | ForEach-Object { Make-Icon $_ }

$outPath = 'C:\Users\admin\source\repos\+Playground\Applications\System\Svc.Guard\Resources\app.ico'
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
