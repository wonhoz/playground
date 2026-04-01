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

        # 검정 라운드 사각형 배경
        $bgBrush     = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 13, 13, 20))
        $amberBrush  = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 245, 158, 11))
        $amberPen    = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255, 245, 158, 11))

        # 라운드 사각형 배경
        $gp = New-Object System.Drawing.Drawing2D.GraphicsPath
        $radius = [int]($sz * 0.2)
        $gp.AddArc(0, 0, $radius * 2, $radius * 2, 180, 90)
        $gp.AddArc($sz - $radius * 2, 0, $radius * 2, $radius * 2, 270, 90)
        $gp.AddArc($sz - $radius * 2, $sz - $radius * 2, $radius * 2, $radius * 2, 0, 90)
        $gp.AddArc(0, $sz - $radius * 2, $radius * 2, $radius * 2, 90, 90)
        $gp.CloseFigure()
        $g.FillPath($bgBrush, $gp)

        # </> 심볼 텍스트 렌더링
        $fontSize = [int]($sz * 0.38)
        if ($fontSize -lt 6) { $fontSize = 6 }
        $font = New-Object System.Drawing.Font("Consolas", $fontSize, [System.Drawing.FontStyle]::Bold)

        $text = "</>"
        $sf = New-Object System.Drawing.StringFormat
        $sf.Alignment     = [System.Drawing.StringAlignment]::Center
        $sf.LineAlignment = [System.Drawing.StringAlignment]::Center
        $rect = New-Object System.Drawing.RectangleF(0, 0, $sz, $sz)
        $g.DrawString($text, $font, $amberBrush, $rect, $sf)

        $font.Dispose()
        $sf.Dispose()
        $gp.Dispose()
        $bgBrush.Dispose()
        $amberBrush.Dispose()
        $amberPen.Dispose()
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
