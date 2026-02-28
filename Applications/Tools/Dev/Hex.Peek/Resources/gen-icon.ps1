Add-Type -AssemblyName System.Drawing

function New-HexBitmap {
    param([int]$sz)

    $bmp = New-Object System.Drawing.Bitmap($sz, $sz)
    $g   = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode     = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
    $g.Clear([System.Drawing.Color]::Transparent)

    # 배경 라운드 사각형
    $m  = 1
    $rw = $sz - 2
    $rh = $sz - 2
    $cr = [int]($sz * 0.18)
    $cr2 = $cr * 2

    $bgColor = [System.Drawing.Color]::FromArgb(255, 20, 20, 35)
    $bgBrush = New-Object System.Drawing.SolidBrush($bgColor)
    $path    = New-Object System.Drawing.Drawing2D.GraphicsPath

    $x1 = $m
    $y1 = $m
    $x2 = $m + $rw - $cr2
    $y2 = $m + $rh - $cr2

    $path.AddArc($x1, $y1, $cr2, $cr2, 180, 90)
    $path.AddArc($x2, $y1, $cr2, $cr2, 270, 90)
    $path.AddArc($x2, $y2, $cr2, $cr2,   0, 90)
    $path.AddArc($x1, $y2, $cr2, $cr2,  90, 90)
    $path.CloseFigure()
    $g.FillPath($bgBrush, $path)

    # 테두리
    $bpw = [float]($sz * 0.05)
    $borderPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(180, 0, 151, 200), $bpw)
    $g.DrawPath($borderPen, $path)

    $sf = New-Object System.Drawing.StringFormat
    $sf.Alignment     = [System.Drawing.StringAlignment]::Center
    $sf.LineAlignment = [System.Drawing.StringAlignment]::Center

    if ($sz -ge 32) {
        # 상단: "0F" (청록)
        $fszTop  = [float]($sz * 0.28)
        $fontTop = New-Object System.Drawing.Font("Consolas", $fszTop, [System.Drawing.FontStyle]::Bold)
        $br0F    = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 0, 188, 212))
        $rtTop   = [float]$sz * 0.08
        $rtH     = [float]$sz * 0.42
        $rectTop = New-Object System.Drawing.RectangleF([float]0, $rtTop, [float]$sz, $rtH)
        $g.DrawString("0F", $fontTop, $br0F, $rectTop, $sf)
        $fontTop.Dispose()
        $br0F.Dispose()

        # 하단: "HEX" (노랑)
        $fszBot  = [float]($sz * 0.22)
        $fontBot = New-Object System.Drawing.Font("Consolas", $fszBot, [System.Drawing.FontStyle]::Bold)
        $brHex   = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 241, 250, 140))
        $rbTop   = [float]$sz * 0.52
        $rbH     = [float]$sz * 0.40
        $rectBot = New-Object System.Drawing.RectangleF([float]0, $rbTop, [float]$sz, $rbH)
        $g.DrawString("HEX", $fontBot, $brHex, $rectBot, $sf)
        $fontBot.Dispose()
        $brHex.Dispose()
    } else {
        # 16px: "H" 한 글자
        $fsz1  = [float]($sz * 0.55)
        $font1 = New-Object System.Drawing.Font("Consolas", $fsz1, [System.Drawing.FontStyle]::Bold)
        $br1   = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 0, 188, 212))
        $rect1 = New-Object System.Drawing.RectangleF([float]0, [float]0, [float]$sz, [float]$sz)
        $g.DrawString("H", $font1, $br1, $rect1, $sf)
        $font1.Dispose()
        $br1.Dispose()
    }

    $g.Dispose()
    $bgBrush.Dispose()
    $borderPen.Dispose()
    $path.Dispose()
    $sf.Dispose()
    return $bmp
}

$sizes = @(16, 32, 48, 256)
$pngData = @{}
foreach ($s in $sizes) {
    $bmp = New-HexBitmap -sz $s
    $ms  = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $pngData[$s] = $ms.ToArray()
    $ms.Dispose()
    $bmp.Dispose()
}

# ICO 조립
$icoPath = Join-Path $PSScriptRoot "app.ico"
$out     = New-Object System.IO.MemoryStream
$bw      = New-Object System.IO.BinaryWriter($out)

# ICONDIR 헤더
$bw.Write([uint16]0)
$bw.Write([uint16]1)
$cnt = [uint16]$sizes.Count
$bw.Write($cnt)

# 디렉토리 오프셋 계산
$dirBytes = 16 * $sizes.Count
$hdrTotal = 6 + $dirBytes
$offsets  = @{}
$cur      = $hdrTotal
foreach ($s in $sizes) {
    $offsets[$s] = $cur
    $cur += $pngData[$s].Length
}

# ICONDIRENTRY 목록
foreach ($s in $sizes) {
    if ($s -ge 256) { $wb = [byte]0 } else { $wb = [byte]$s }
    $bw.Write($wb)          # bWidth
    $bw.Write($wb)          # bHeight
    $bw.Write([byte]0)      # bColorCount
    $bw.Write([byte]0)      # bReserved
    $bw.Write([uint16]1)    # wPlanes
    $bw.Write([uint16]32)   # wBitCount
    $dl = [uint32]$pngData[$s].Length
    $bw.Write($dl)          # dwBytesInRes
    $do = [uint32]$offsets[$s]
    $bw.Write($do)          # dwImageOffset
}

# PNG 데이터
foreach ($s in $sizes) {
    $bw.Write($pngData[$s])
}

$bw.Flush()
[System.IO.File]::WriteAllBytes($icoPath, $out.ToArray())
$bw.Dispose()
$out.Dispose()

Write-Host "app.ico 생성 완료: $icoPath"
