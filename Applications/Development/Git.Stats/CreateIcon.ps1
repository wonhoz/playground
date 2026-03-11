Add-Type -AssemblyName System.Drawing

function Make-Icon {
    param([int]$sz)
    $bmp = New-Object System.Drawing.Bitmap($sz, $sz)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'

    $g.Clear([System.Drawing.Color]::FromArgb(13, 17, 23))

    $cx = [int]($sz * 0.5)
    $cy = [int]($sz * 0.5)

    # 막대 차트 배경
    $bgBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(22, 27, 34))
    $pad = [int]($sz * 0.08)
    $g.FillRectangle($bgBrush, $pad, $pad, $sz - $pad*2, $sz - $pad*2)

    # 막대들 (히스토그램)
    $barColors = @(
        [System.Drawing.Color]::FromArgb(63, 185, 80),
        [System.Drawing.Color]::FromArgb(63, 185, 80),
        [System.Drawing.Color]::FromArgb(57, 197, 187),
        [System.Drawing.Color]::FromArgb(88, 166, 255),
        [System.Drawing.Color]::FromArgb(88, 166, 255)
    )
    $heights = @(0.35, 0.55, 0.70, 0.45, 0.80)
    $barCount = 5
    $barW = [int](($sz - $pad*2) / ($barCount * 1.6))
    $gap  = [int]($barW * 0.6)
    $baseY = [int]($sz - $pad * 1.5)
    $maxH  = [int]($sz * 0.55)

    for ($i = 0; $i -lt $barCount; $i++) {
        $bh = [int]($maxH * $heights[$i])
        $bx = $pad + [int](($i * ($barW + $gap)) + $gap/2)
        $by = $baseY - $bh
        $barBrush = New-Object System.Drawing.SolidBrush($barColors[$i])
        $rc = New-Object System.Drawing.Rectangle($bx, $by, $barW, $bh)
        $g.FillRectangle($barBrush, $rc)
    }

    # 베이스 라인
    $linePen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(48, 54, 61), [int]([Math]::Max(1,$sz*0.025)))
    $g.DrawLine($linePen, $pad, $baseY, $sz - $pad, $baseY)

    $g.Dispose()
    return $bmp
}

$sizes = @(256, 48, 32, 16)
$bitmaps = $sizes | ForEach-Object { Make-Icon $_ }

$outPath = 'C:\Users\admin\source\repos\+Playground\Applications\Development\Git.Stats\Resources\app.ico'
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
