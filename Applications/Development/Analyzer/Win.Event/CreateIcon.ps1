# Win.Event 아이콘 생성 스크립트
# 디자인: 어두운 배경 + 앰버 경고 방패 + 이벤트 로그 라인 심볼

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$outPath   = Join-Path $scriptDir "Resources\app.ico"

Add-Type -AssemblyName System.Drawing

function New-WinEventBitmap {
    param([int]$sz)

    $bmp = New-Object System.Drawing.Bitmap($sz, $sz)
    $g   = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode    = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic

    $bg    = [System.Drawing.Color]::FromArgb(255, 18,  18,  28)
    $amber = [System.Drawing.Color]::FromArgb(255, 255, 165,  0)
    $amberD= [System.Drawing.Color]::FromArgb(255, 200, 120,  0)
    $white = [System.Drawing.Color]::FromArgb(255, 220, 220, 230)
    $gray  = [System.Drawing.Color]::FromArgb(180, 100, 100, 130)

    # 배경 원
    $brushBg = New-Object System.Drawing.SolidBrush($bg)
    $g.FillEllipse($brushBg, 1, 1, $sz-2, $sz-2)

    # 방패 모양 (앰버)
    $m   = [float]($sz * 0.5)
    $r   = [float]($sz * 0.34)
    $pts = @(
        [System.Drawing.PointF]::new($m,          [float]($sz * 0.12)),
        [System.Drawing.PointF]::new($m + $r,     [float]($sz * 0.28)),
        [System.Drawing.PointF]::new($m + $r,     [float]($sz * 0.55)),
        [System.Drawing.PointF]::new($m,          [float]($sz * 0.86)),
        [System.Drawing.PointF]::new($m - $r,     [float]($sz * 0.55)),
        [System.Drawing.PointF]::new($m - $r,     [float]($sz * 0.28))
    )

    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddPolygon($pts)
    $brushAmber = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(40, 255, 165, 0))
    $g.FillPath($brushAmber, $path)
    $penAmber = New-Object System.Drawing.Pen($amber, [float]($sz * 0.04))
    $g.DrawPolygon($penAmber, $pts)

    # 로그 라인 3개 (흰색 가로선)
    $penLine = New-Object System.Drawing.Pen($white, [float]($sz * 0.055))
    $penLine.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $penLine.EndCap   = [System.Drawing.Drawing2D.LineCap]::Round

    $lx1 = [float]($sz * 0.33)
    $lx2 = [float]($sz * 0.67)
    $ly1 = [float]($sz * 0.38)
    $ly2 = [float]($sz * 0.50)
    $ly3 = [float]($sz * 0.62)

    $g.DrawLine($penLine, $lx1, $ly1, $lx2, $ly1)
    $g.DrawLine($penLine, $lx1, $ly2, [float]($sz * 0.60), $ly2)
    $g.DrawLine($penLine, $lx1, $ly3, [float]($sz * 0.55), $ly3)

    $g.Dispose()
    return $bmp
}

# ICO 파일 직접 생성
function Write-IcoFile {
    param([System.Drawing.Bitmap[]]$bitmaps, [string]$path)

    $ms = New-Object System.IO.MemoryStream
    $bw = New-Object System.IO.BinaryWriter($ms)

    $count = $bitmaps.Count

    # ICO 헤더
    $bw.Write([uint16]0)      # reserved
    $bw.Write([uint16]1)      # type = ICO
    $bw.Write([uint16]$count)

    $pngStreams = @()
    foreach ($bmp in $bitmaps) {
        $ps = New-Object System.IO.MemoryStream
        $bmp.Save($ps, [System.Drawing.Imaging.ImageFormat]::Png)
        $pngStreams += $ps
    }

    $headerSize  = 6
    $dirEntrySize = 16
    $offset = $headerSize + $dirEntrySize * $count

    for ($i = 0; $i -lt $count; $i++) {
        $bmp = $bitmaps[$i]
        $szVal = if ($bmp.Width -ge 256) { 0 } else { $bmp.Width }
        $bw.Write([byte]$szVal)
        $bw.Write([byte]$szVal)
        $bw.Write([byte]0)    # color count
        $bw.Write([byte]0)    # reserved
        $bw.Write([uint16]1)  # planes
        $bw.Write([uint16]32) # bpp
        $bw.Write([uint32]$pngStreams[$i].Length)
        $bw.Write([uint32]$offset)
        $offset += $pngStreams[$i].Length
    }

    foreach ($ps in $pngStreams) {
        $bw.Write($ps.ToArray())
        $ps.Dispose()
    }

    $bw.Flush()
    $dir = Split-Path $path
    if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir | Out-Null }
    [System.IO.File]::WriteAllBytes($path, $ms.ToArray())
    $bw.Dispose()
    $ms.Dispose()
}

$sizes   = @(16, 32, 48, 256)
$bitmaps = $sizes | ForEach-Object { New-WinEventBitmap $_ }

Write-IcoFile -bitmaps $bitmaps -path $outPath
Write-Host "아이콘 생성 완료: $outPath"

foreach ($b in $bitmaps) { $b.Dispose() }
