Add-Type -AssemblyName System.Drawing

function Make-Icon {
    param([int]$sz)
    $bmp = New-Object System.Drawing.Bitmap($sz, $sz)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'

    $g.Clear([System.Drawing.Color]::FromArgb(10, 14, 26))

    $cx = [int]($sz * 0.5)
    $cy = [int]($sz * 0.5)
    $r  = [int]($sz * 0.40)

    # 노트북 모양
    $notebookBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(31, 41, 55))
    $nbx = [int]($sz * 0.12)
    $nby = [int]($sz * 0.22)
    $nbw = [int]($sz * 0.76)
    $nbh = [int]($sz * 0.44)
    $g.FillRectangle($notebookBrush, $nbx, $nby, $nbw, $nbh)

    # 노트북 테두리
    $notebookPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(99, 102, 241), [int]([Math]::Max(2,$sz*0.04)))
    $g.DrawRectangle($notebookPen, $nbx, $nby, $nbw, $nbh)

    # 화면 안 코드 라인들
    $codePen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(99, 102, 241), [int]([Math]::Max(1,$sz*0.03)))
    $codeGreenPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(16, 185, 129), [int]([Math]::Max(1,$sz*0.03)))
    $ly1 = $nby + [int]($nbh * 0.22)
    $ly2 = $nby + [int]($nbh * 0.44)
    $ly3 = $nby + [int]($nbh * 0.66)
    $lx1 = $nbx + [int]($nbw*0.12)
    $g.DrawLine($codePen, $lx1, $ly1, $lx1+[int]($nbw*0.40), $ly1)
    $g.DrawLine($codeGreenPen, $lx1, $ly2, $lx1+[int]($nbw*0.60), $ly2)
    $g.DrawLine($codePen, $lx1, $ly3, $lx1+[int]($nbw*0.30), $ly3)

    # 별 (프레스티지 상징)
    $starBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(245, 158, 11))
    $sr = [int]($sz * 0.10)
    $scx = $nbx + $nbw - [int]($nbw*0.15)
    $scy = $nby + [int]($nbh * 0.22)
    $starPts = @()
    for ($i = 0; $i -lt 10; $i++) {
        $angle = [Math]::PI * $i / 5 - [Math]::PI/2
        $rad = if ($i % 2 -eq 0) { $sr } else { $sr * 0.4 }
        $starPts += [System.Drawing.PointF]::new(
            $scx + $rad * [Math]::Cos($angle),
            $scy + $rad * [Math]::Sin($angle))
    }
    $g.FillPolygon($starBrush, $starPts)

    # 베이스
    $baseBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(31, 41, 55))
    $g.FillRectangle($baseBrush, [int]($sz*0.22), [int]($sz*0.66), [int]($sz*0.56), [int]($sz*0.08))

    $g.Dispose()
    return $bmp
}

$sizes = @(256, 48, 32, 16)
$bitmaps = $sizes | ForEach-Object { Make-Icon $_ }

$outPath = 'C:\Users\admin\source\repos\+Playground\Games\Idle\Code.Idle\Resources\app.ico'
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
