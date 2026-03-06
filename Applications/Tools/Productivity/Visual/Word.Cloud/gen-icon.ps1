Add-Type -AssemblyName System.Drawing

function New-IcoFile {
    param([string]$Path, [int[]]$Sizes)

    $pngData = @{}
    foreach ($sz in $Sizes) {
        $bmp = New-Object System.Drawing.Bitmap($sz, $sz)
        $g = [System.Drawing.Graphics]::FromImage($bmp)
        $g.SmoothingMode     = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit

        $g.Clear([System.Drawing.Color]::Transparent)

        $bgColor    = [System.Drawing.Color]::FromArgb(255, 13, 13, 22)
        $cyanColor  = [System.Drawing.Color]::FromArgb(255, 6, 182, 212)
        $whiteColor = [System.Drawing.Color]::White

        $bgBrush   = New-Object System.Drawing.SolidBrush($bgColor)
        $cyanBrush = New-Object System.Drawing.SolidBrush($cyanColor)
        $cyanPen   = New-Object System.Drawing.Pen($cyanColor, [float]([math]::Max(1.0, $sz * 0.03)))
        $whiteBrush = New-Object System.Drawing.SolidBrush($whiteColor)

        # 라운드 사각형 배경
        $gp = New-Object System.Drawing.Drawing2D.GraphicsPath
        $radius = [int]($sz * 0.18)
        $gp.AddArc(0, 0, $radius * 2, $radius * 2, 180, 90)
        $gp.AddArc($sz - $radius * 2, 0, $radius * 2, $radius * 2, 270, 90)
        $gp.AddArc($sz - $radius * 2, $sz - $radius * 2, $radius * 2, $radius * 2, 0, 90)
        $gp.AddArc(0, $sz - $radius * 2, $radius * 2, $radius * 2, 90, 90)
        $gp.CloseFigure()
        $g.FillPath($bgBrush, $gp)

        # 구름 형태 (3개 원 조합, Cyan)
        $cx = [float]($sz * 0.5)
        $cy = [float]($sz * 0.52)
        $r1 = [float]($sz * 0.18)  # 중앙 큰 원
        $r2 = [float]($sz * 0.13)  # 왼쪽 원
        $r3 = [float]($sz * 0.12)  # 오른쪽 원
        $r4 = [float]($sz * 0.10)  # 맨 왼쪽 작은 원

        # 구름 바닥 직사각형
        $cloudBottom = $cy + $r1
        $cloudLeft   = $cx - $r2 - $r4 - [float]($sz * 0.02)
        $cloudRight  = $cx + $r3 + [float]($sz * 0.04)
        $rectH = [float]($sz * 0.15)

        $cloudPath = New-Object System.Drawing.Drawing2D.GraphicsPath

        # 원들 추가 (중앙, 좌, 우, 맨좌)
        $cloudPath.AddEllipse([float]($cx - $r1), [float]($cy - $r1), $r1 * 2, $r1 * 2)
        $cloudPath.AddEllipse([float]($cx - $r2 * 1.4 - $r2), [float]($cy - $r2 * 0.3 - $r2), $r2 * 2, $r2 * 2)
        $cloudPath.AddEllipse([float]($cx + $r3 * 0.6 - $r3), [float]($cy - $r3 * 0.5 - $r3), $r3 * 2, $r3 * 2)
        $cloudPath.AddEllipse([float]($cx - $r2 * 1.4 - $r4 - $r2 * 0.2), [float]($cy + $r2 * 0.2 - $r4), $r4 * 2, $r4 * 2)
        $cloudPath.AddRectangle([System.Drawing.RectangleF]::new($cloudLeft, $cloudBottom - $rectH, $cloudRight - $cloudLeft, $rectH + 1))

        $g.FillPath($cyanBrush, $cloudPath)

        # 흰색 "W" 텍스트 (중앙)
        $fontSize = [int]($sz * 0.32)
        if ($fontSize -lt 5) { $fontSize = 5 }
        $font = New-Object System.Drawing.Font("Arial", $fontSize, [System.Drawing.FontStyle]::Bold)
        $sf = New-Object System.Drawing.StringFormat
        $sf.Alignment     = [System.Drawing.StringAlignment]::Center
        $sf.LineAlignment = [System.Drawing.StringAlignment]::Center
        $textRect = New-Object System.Drawing.RectangleF(0, [float]($sz * 0.08), [float]$sz, [float]$sz)
        $g.DrawString("W", $font, $whiteBrush, $textRect, $sf)

        $font.Dispose()
        $sf.Dispose()
        $cloudPath.Dispose()
        $gp.Dispose()
        $bgBrush.Dispose()
        $cyanBrush.Dispose()
        $cyanPen.Dispose()
        $whiteBrush.Dispose()
        $g.Dispose()

        $ms = New-Object System.IO.MemoryStream
        $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
        $bmp.Dispose()
        $pngData[$sz] = $ms.ToArray()
        $ms.Dispose()
    }

    # ICO 파일 작성
    $fs2 = [System.IO.File]::Create($Path)
    $bw  = New-Object System.IO.BinaryWriter($fs2)

    # ICONDIR header (6 bytes)
    $bw.Write([uint16]0)
    $bw.Write([uint16]1)
    $bw.Write([uint16]$Sizes.Count)

    # ICONDIRENTRY 블록 (16 bytes * count)
    $headerSize = 6 + $Sizes.Count * 16
    $offset = $headerSize

    foreach ($sz in $Sizes) {
        $data = $pngData[$sz]
        if ($sz -ge 256) { $w = 0 } else { $w = $sz }
        if ($sz -ge 256) { $h = 0 } else { $h = $sz }
        $bw.Write([byte]$w)
        $bw.Write([byte]$h)
        $bw.Write([byte]0)
        $bw.Write([byte]0)
        $bw.Write([uint16]1)
        $bw.Write([uint16]32)
        $bw.Write([uint32]$data.Length)
        $bw.Write([uint32]$offset)
        $offset += $data.Length
    }

    foreach ($sz in $Sizes) {
        $bw.Write($pngData[$sz])
    }

    $bw.Flush()
    $bw.Close()
    $fs2.Close()
    Write-Output "ICO 생성 완료: $Path"
}

New-IcoFile -Path "$PSScriptRoot\Resources\app.ico" -Sizes @(16, 32, 48, 256)
