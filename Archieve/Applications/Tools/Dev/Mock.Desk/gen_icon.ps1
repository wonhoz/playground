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
    $g.Clear([System.Drawing.Color]::FromArgb(15,15,10))

    # 배경 원
    $bg = [System.Drawing.Color]::FromArgb(26,26,14)
    $brush = New-Object System.Drawing.SolidBrush($bg)
    $g.FillEllipse($brush, [float](2*$f), [float](2*$f), [float](28*$f), [float](28*$f))
    $brush.Dispose()

    # 테두리 원
    $pen0 = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(80,245,158,11), [float]($f))
    $g.DrawEllipse($pen0, [float](2*$f), [float](2*$f), [float](28*$f), [float](28*$f))
    $pen0.Dispose()

    # 서버 아이콘: 직사각형 3개 (DB 실린더 느낌)
    $amber = [System.Drawing.Color]::FromArgb(245,158,11)
    $brush2 = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(50,245,158,11))
    $penA = New-Object System.Drawing.Pen($amber, [float](1.5*$f))

    # 상단 타원
    $g.FillEllipse($brush2, [float](7*$f), [float](7*$f), [float](18*$f), [float](6*$f))
    $g.DrawEllipse($penA, [float](7*$f), [float](7*$f), [float](18*$f), [float](6*$f))
    # 중간 타원
    $g.FillEllipse($brush2, [float](7*$f), [float](13*$f), [float](18*$f), [float](6*$f))
    $g.DrawEllipse($penA, [float](7*$f), [float](13*$f), [float](18*$f), [float](6*$f))
    # 하단 타원
    $g.FillEllipse($brush2, [float](7*$f), [float](19*$f), [float](18*$f), [float](6*$f))
    $g.DrawEllipse($penA, [float](7*$f), [float](19*$f), [float](18*$f), [float](6*$f))

    # 세로 선으로 실린더 연결
    $g.DrawLine($penA, [float](7*$f), [float](10*$f), [float](7*$f), [float](22*$f))
    $g.DrawLine($penA, [float](25*$f), [float](10*$f), [float](25*$f), [float](22*$f))

    # 상태 불빛 (초록 점)
    $brushG = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(20,184,80))
    $g.FillEllipse($brushG, [float](21*$f), [float](8.5*$f), [float](2.5*$f), [float](2.5*$f))
    $brushG.Dispose()

    $brush2.Dispose()
    $penA.Dispose()
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

$bw.Write([uint16]0)
$bw.Write([uint16]1)
$bw.Write([uint16]$pngs.Count)

$offset = 6 + $pngs.Count * 16
for ($i = 0; $i -lt $pngs.Count; $i++) {
    $szVal = $sizes[$i]
    if ($szVal -ge 256) { $szByte = [byte]0 } else { $szByte = [byte]$szVal }
    $bw.Write($szByte)
    $bw.Write($szByte)
    $bw.Write([byte]0)
    $bw.Write([byte]0)
    $bw.Write([uint16]1)
    $bw.Write([uint16]32)
    $bw.Write([uint32]$pngs[$i].Length)
    $bw.Write([uint32]$offset)
    $offset += $pngs[$i].Length
}

foreach ($png in $pngs) { $bw.Write($png) }
$bw.Close()
$fs.Close()

Write-Host "Mock.Desk 아이콘 생성 완료: $outPath"
