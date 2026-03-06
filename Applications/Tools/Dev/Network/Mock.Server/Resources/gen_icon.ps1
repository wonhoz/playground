Add-Type -AssemblyName System.Drawing

$outIco = "$PSScriptRoot\app.ico"
$outPng = "$PSScriptRoot\icon32.png"
$sizes  = @(16, 32, 48, 256)

$pngs = @()
foreach ($sz in $sizes) {
    $bmp = New-Object System.Drawing.Bitmap $sz, $sz
    $g   = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode     = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.Clear([System.Drawing.Color]::FromArgb(0, 0, 0, 0))

    $f = $sz / 32.0

    # 배경 원 (#131321)
    $brushBg = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(19, 19, 33))
    $g.FillEllipse($brushBg, [float](1*$f), [float](1*$f), [float](30*$f), [float](30*$f))
    $brushBg.Dispose()

    # 테두리 원 (#818CF8 Indigo)
    $penBorder = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(129, 140, 248), [float](1.2*$f))
    $g.DrawEllipse($penBorder, [float](1*$f), [float](1*$f), [float](30*$f), [float](30*$f))
    $penBorder.Dispose()

    # 랙 크기
    $rackX = [float](6 * $f)
    $rackW = [float](20 * $f)
    $rackH = [float](4.5 * $f)

    # 랙 Y 위치 (3단)
    $rackY0 = [float](8.5  * $f)
    $rackY1 = [float](13.75 * $f)
    $rackY2 = [float](19.0 * $f)

    $colDim    = [System.Drawing.Color]::FromArgb(34, 34, 58)
    $colAccent = [System.Drawing.Color]::FromArgb(129, 140, 248)
    $colLedGrn = [System.Drawing.Color]::FromArgb(34, 197, 94)
    $colLedGry = [System.Drawing.Color]::FromArgb(85, 85, 112)
    $colDark   = [System.Drawing.Color]::FromArgb(13, 13, 22)

    # 랙 0 (상단 - dim)
    $br0 = New-Object System.Drawing.SolidBrush($colDim)
    $g.FillRectangle($br0, $rackX, $rackY0, $rackW, $rackH)
    $br0.Dispose()

    # 랙 1 (중단 - accent)
    $br1 = New-Object System.Drawing.SolidBrush($colAccent)
    $g.FillRectangle($br1, $rackX, $rackY1, $rackW, $rackH)
    $br1.Dispose()

    # 랙 2 (하단 - dim)
    $br2 = New-Object System.Drawing.SolidBrush($colDim)
    $g.FillRectangle($br2, $rackX, $rackY2, $rackW, $rackH)
    $br2.Dispose()

    # LED (각 랙 좌측)
    $ledD = [float](2.8 * $f)
    $ledR = $ledD / 2.0
    $ledOffX = $rackX + [float](2.2 * $f) - $ledR
    $ledOffY0 = $rackY0 + ($rackH / 2.0) - $ledR
    $ledOffY1 = $rackY1 + ($rackH / 2.0) - $ledR
    $ledOffY2 = $rackY2 + ($rackH / 2.0) - $ledR

    $blGrn = New-Object System.Drawing.SolidBrush($colLedGrn)
    $g.FillEllipse($blGrn, $ledOffX, $ledOffY0, $ledD, $ledD)
    $blGrn.Dispose()

    $blDrk = New-Object System.Drawing.SolidBrush($colDark)
    $g.FillEllipse($blDrk, $ledOffX, $ledOffY1, $ledD, $ledD)
    $blDrk.Dispose()

    $blGry = New-Object System.Drawing.SolidBrush($colLedGry)
    $g.FillEllipse($blGry, $ledOffX, $ledOffY2, $ledD, $ledD)
    $blGry.Dispose()

    # ▶ 심볼 (중단 랙 우측)
    $triMid = $rackY1 + ($rackH / 2.0)
    $triX   = $rackX + $rackW - [float](5.5 * $f)
    $triS   = [float](1.5 * $f)

    $pt0 = New-Object System.Drawing.PointF($triX, ($triMid - $triS))
    $pt1 = New-Object System.Drawing.PointF(($triX + $triS * 1.5), $triMid)
    $pt2 = New-Object System.Drawing.PointF($triX, ($triMid + $triS))
    $pts = [System.Drawing.PointF[]]@($pt0, $pt1, $pt2)

    $brTri = New-Object System.Drawing.SolidBrush($colDark)
    $g.FillPolygon($brTri, $pts)
    $brTri.Dispose()

    $g.Dispose()

    if ($sz -eq 32) {
        $bmp.Save($outPng, [System.Drawing.Imaging.ImageFormat]::Png)
        Write-Host "PNG 저장: $outPng"
    }

    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $arr = $ms.ToArray()
    $pngs += , $arr
    $ms.Dispose()
    $bmp.Dispose()
}

# ICO 파일 직접 작성
$fs = [System.IO.File]::Create($outIco)
$bw = New-Object System.IO.BinaryWriter($fs)

$bw.Write([uint16]0)
$bw.Write([uint16]1)
$bw.Write([uint16]$pngs.Count)

$dataOffset = 6 + $pngs.Count * 16
for ($i = 0; $i -lt $pngs.Count; $i++) {
    $szVal = $sizes[$i]
    if ($szVal -ge 256) {
        $szByte = [byte]0
    } else {
        $szByte = [byte]$szVal
    }
    $bw.Write($szByte)
    $bw.Write($szByte)
    $bw.Write([byte]0)
    $bw.Write([byte]0)
    $bw.Write([uint16]1)
    $bw.Write([uint16]32)
    $bw.Write([uint32]$pngs[$i].Length)
    $bw.Write([uint32]$dataOffset)
    $dataOffset += $pngs[$i].Length
}

foreach ($png in $pngs) {
    $bw.Write($png)
}
$bw.Close()
$fs.Close()

Write-Host "Mock.Server 아이콘 생성 완료: $outIco ($($pngs.Count)개 크기)"
