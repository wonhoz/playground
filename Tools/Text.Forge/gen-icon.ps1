Add-Type -AssemblyName System.Drawing

function New-IcoFile {
    param([string]$Path, [int[]]$Sizes)

    $pngData = @{}
    foreach ($sz in $Sizes) {
        $bmp = New-Object System.Drawing.Bitmap($sz, $sz)
        $g = [System.Drawing.Graphics]::FromImage($bmp)
        $g.SmoothingMode     = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit

        # 배경: #14143A
        $g.Clear([System.Drawing.Color]::FromArgb(255, 20, 20, 58))

        # 오렌지 브러시
        $brush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 255, 160, 0))

        # 글꼴 크기 계산
        $fs = [int]($sz * 0.38)
        if ($fs -lt 6) { $fs = 6 }
        $font = New-Object System.Drawing.Font("Consolas", $fs, [System.Drawing.FontStyle]::Bold)
        $sf = New-Object System.Drawing.StringFormat
        $sf.Alignment     = [System.Drawing.StringAlignment]::Center
        $sf.LineAlignment = [System.Drawing.StringAlignment]::Center
        $g.DrawString("{  }", $font, $brush, [System.Drawing.RectangleF]::new(0, 0, $sz, $sz), $sf)

        $font.Dispose()
        $brush.Dispose()
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
        $w = if ($sz -ge 256) { 0 } else { $sz }
        $h = if ($sz -ge 256) { 0 } else { $sz }
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
