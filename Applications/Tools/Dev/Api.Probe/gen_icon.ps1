Add-Type -AssemblyName System.Drawing

$outPath = "$PSScriptRoot\Resources\app.ico"
$sizes = @(16, 32, 48, 256)

$pngs = @()
foreach ($sz in $sizes) {
    $bmp = New-Object System.Drawing.Bitmap $sz,$sz
    $g   = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode    = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic

    $f = $sz / 32.0
    $g.Clear([System.Drawing.Color]::FromArgb(15,15,23))

    # 배경 원
    $bg = [System.Drawing.Color]::FromArgb(26,26,46)
    $brush = New-Object System.Drawing.SolidBrush($bg)
    $g.FillEllipse($brush, [float](2*$f), [float](2*$f), [float](28*$f), [float](28*$f))
    $brush.Dispose()

    # 테두리 원
    $pen0 = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(60,20,184,166), [float]($f))
    $g.DrawEllipse($pen0, [float](2*$f), [float](2*$f), [float](28*$f), [float](28*$f))
    $pen0.Dispose()

    # 전송 화살표 (→)
    $accent = [System.Drawing.Color]::FromArgb(20,184,166)
    $pen = New-Object System.Drawing.Pen($accent, [float](2.5*$f))
    $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $pen.EndCap   = [System.Drawing.Drawing2D.LineCap]::Round

    # 수평선
    $g.DrawLine($pen, [float](8*$f), [float](16*$f), [float](22*$f), [float](16*$f))
    # 화살촉
    $g.DrawLine($pen, [float](22*$f), [float](16*$f), [float](15*$f), [float](10*$f))
    $g.DrawLine($pen, [float](22*$f), [float](16*$f), [float](15*$f), [float](22*$f))
    $pen.Dispose()

    # 보조 선 (요청 헤더 느낌)
    $pen2 = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(100,20,184,166), [float](1.5*$f))
    $g.DrawLine($pen2, [float](8*$f), [float](11*$f), [float](16*$f), [float](11*$f))
    $g.DrawLine($pen2, [float](8*$f), [float](21*$f), [float](16*$f), [float](21*$f))
    $pen2.Dispose()

    $g.Dispose()

    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $pngs += ,$ms.ToArray()
    $ms.Dispose()
    $bmp.Dispose()
}

# ICO 파일 직접 작성
$fs = [System.IO.File]::Create($outPath)
$bw = New-Object System.IO.BinaryWriter($fs)

# ICONDIR
$bw.Write([uint16]0)            # reserved
$bw.Write([uint16]1)            # type = ICO
$bw.Write([uint16]$pngs.Count) # count

$offset = 6 + $pngs.Count * 16
for ($i = 0; $i -lt $pngs.Count; $i++) {
    $szVal = $sizes[$i]
    if ($szVal -ge 256) { $szByte = [byte]0 } else { $szByte = [byte]$szVal }
    $bw.Write($szByte)           # width
    $bw.Write($szByte)           # height
    $bw.Write([byte]0)           # color count
    $bw.Write([byte]0)           # reserved
    $bw.Write([uint16]1)         # planes
    $bw.Write([uint16]32)        # bit count
    $bw.Write([uint32]$pngs[$i].Length)
    $bw.Write([uint32]$offset)
    $offset += $pngs[$i].Length
}

foreach ($png in $pngs) { $bw.Write($png) }
$bw.Close()
$fs.Close()

Write-Host "Api.Probe 아이콘 생성 완료: $outPath"
