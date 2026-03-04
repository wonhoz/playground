Add-Type -AssemblyName System.Drawing

$outPath = "$PSScriptRoot\Resources\app.ico"
$sizes = @(256, 48, 32, 16)
$bitmaps = [System.Collections.ArrayList]::new()

foreach ($sz in $sizes) {
    $bmp = New-Object System.Drawing.Bitmap($sz, $sz)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias

    # 배경 원 (다크 블루)
    $bgBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 15, 40, 90))
    $g.FillEllipse($bgBrush, 2, 2, $sz - 4, $sz - 4)
    $bgBrush.Dispose()

    # 테두리
    $borderW = [float][Math]::Max(1.5, $sz / 40.0)
    $penBorder = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(220, 79, 195, 247), $borderW)
    $g.DrawEllipse($penBorder, 2, 2, $sz - 4, $sz - 4)
    $penBorder.Dispose()

    # 돋보기 원
    $cx = [int]($sz * 0.40)
    $cy = [int]($sz * 0.40)
    $r  = [int]($sz * 0.26)
    $lensW = [float][Math]::Max(2.0, $sz / 18.0)
    $penLens = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255, 79, 195, 247), $lensW)
    $g.DrawEllipse($penLens, $cx - $r, $cy - $r, $r * 2, $r * 2)
    $penLens.Dispose()

    # 손잡이
    $x1 = [int]($cx + $r * 0.72)
    $y1 = [int]($cy + $r * 0.72)
    $x2 = [int]($sz * 0.84)
    $y2 = [int]($sz * 0.84)
    $handleW = [float][Math]::Max(2.5, $sz / 14.0)
    $penHandle = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255, 79, 195, 247), $handleW)
    $penHandle.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $penHandle.EndCap   = [System.Drawing.Drawing2D.LineCap]::Round
    $g.DrawLine($penHandle, $x1, $y1, $x2, $y2)
    $penHandle.Dispose()

    # 내부 막대 그래프 (32px 이상)
    if ($sz -ge 32) {
        $barColors = @(
            [System.Drawing.Color]::FromArgb(200, 79, 195, 247),
            [System.Drawing.Color]::FromArgb(180, 41, 121, 255),
            [System.Drawing.Color]::FromArgb(160, 102, 187, 106)
        )
        $barHeights = @(0.55, 0.35, 0.70)
        $barW = [int][Math]::Max(2, $r * 0.3)
        $gap  = [int]($barW * 1.5)
        $startX = $cx - $gap
        for ($i = 0; $i -lt 3; $i++) {
            $bh = [int]($r * $barHeights[$i])
            $bx = $startX + $i * $gap
            $by = $cy + [int]($r * 0.5) - $bh
            $barBrush = New-Object System.Drawing.SolidBrush($barColors[$i])
            $g.FillRectangle($barBrush, $bx, $by, $barW, $bh)
            $barBrush.Dispose()
        }
    }

    $g.Dispose()
    [void]$bitmaps.Add($bmp)
}

# ICO 바이너리 생성
$ms = New-Object System.IO.MemoryStream
$bw = New-Object System.IO.BinaryWriter($ms)

$count = $bitmaps.Count
$bw.Write([uint16]0)
$bw.Write([uint16]1)
$bw.Write([uint16]$count)

$imageStreams = [System.Collections.ArrayList]::new()
foreach ($bmp in $bitmaps) {
    $ims = New-Object System.IO.MemoryStream
    $bmp.Save($ims, [System.Drawing.Imaging.ImageFormat]::Png)
    [void]$imageStreams.Add($ims)
}

$headerSize = 6 + $count * 16
$offset = $headerSize
for ($i = 0; $i -lt $count; $i++) {
    $bmp = $bitmaps[$i]
    $w = if ($bmp.Width -ge 256) { [byte]0 } else { [byte]$bmp.Width }
    $h = if ($bmp.Height -ge 256) { [byte]0 } else { [byte]$bmp.Height }
    $bw.Write($w)
    $bw.Write($h)
    $bw.Write([byte]0)
    $bw.Write([byte]0)
    $bw.Write([uint16]1)
    $bw.Write([uint16]32)
    $bw.Write([uint32]$imageStreams[$i].Length)
    $bw.Write([uint32]$offset)
    $offset += $imageStreams[$i].Length
}
foreach ($ims in $imageStreams) {
    $bw.Write($ims.ToArray())
    $ims.Dispose()
}

[System.IO.File]::WriteAllBytes($outPath, $ms.ToArray())
$bw.Dispose()
$ms.Dispose()
foreach ($bmp in $bitmaps) { $bmp.Dispose() }

Write-Host "ICO 생성 완료: $outPath ($([System.IO.FileInfo]$outPath).Length bytes)"
