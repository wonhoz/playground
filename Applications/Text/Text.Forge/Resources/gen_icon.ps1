Add-Type -AssemblyName System.Drawing

$outIco = "$PSScriptRoot\app.ico"
$sizes  = @(16, 32, 48, 256)

$pngs = @()
foreach ($sz in $sizes) {
    $bmp = New-Object System.Drawing.Bitmap $sz, $sz
    $g   = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode     = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.Clear([System.Drawing.Color]::FromArgb(0, 0, 0, 0))

    $f = $sz / 32.0

    # 배경 원 (#1E1E2E 다크 네이비)
    $brushBg = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(30, 30, 46))
    $g.FillEllipse($brushBg, [float](1*$f), [float](1*$f), [float](30*$f), [float](30*$f))
    $brushBg.Dispose()

    # 테두리 원 (#F59E0B 앰버)
    $penBorder = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(245, 158, 11), [float](1.2*$f))
    $g.DrawEllipse($penBorder, [float](1*$f), [float](1*$f), [float](30*$f), [float](30*$f))
    $penBorder.Dispose()

    $amber = [System.Drawing.Color]::FromArgb(245, 158, 11)

    # T 가로획 (상단)
    $tBarX = [float](7   * $f)
    $tBarY = [float](8.5 * $f)
    $tBarW = [float](18  * $f)
    $tBarH = [float](3   * $f)
    $brushBar = New-Object System.Drawing.SolidBrush($amber)
    $g.FillRectangle($brushBar, $tBarX, $tBarY, $tBarW, $tBarH)
    $brushBar.Dispose()

    # T 세로획 (중앙)
    $tStemX = [float](14.5 * $f)
    $tStemY = $tBarY + $tBarH
    $tStemW = [float](3    * $f)
    $tStemH = [float](8    * $f)
    $brushStem = New-Object System.Drawing.SolidBrush($amber)
    $g.FillRectangle($brushStem, $tStemX, $tStemY, $tStemW, $tStemH)
    $brushStem.Dispose()

    # 텍스트 라인 3줄 (하단 - 텍스트 처리 표현)
    $lineColor = [System.Drawing.Color]::FromArgb(80, 100, 145)
    $lineX = [float](7   * $f)
    $lineH = [float](1.5 * $f)
    $brushLine = New-Object System.Drawing.SolidBrush($lineColor)
    $g.FillRectangle($brushLine, $lineX,           [float](22.0 * $f), [float](18 * $f), $lineH)
    $g.FillRectangle($brushLine, $lineX,           [float](24.5 * $f), [float](13 * $f), $lineH)
    $g.FillRectangle($brushLine, $lineX,           [float](27.0 * $f), [float](16 * $f), $lineH)
    $brushLine.Dispose()

    $g.Dispose()

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

Write-Host "Text.Forge 아이콘 생성 완료: $outIco ($($pngs.Count)개 크기)"
