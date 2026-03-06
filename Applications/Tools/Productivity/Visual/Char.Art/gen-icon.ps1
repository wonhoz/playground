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

        $bgColor     = [System.Drawing.Color]::FromArgb(255, 13, 13, 16)
        $amberColor  = [System.Drawing.Color]::FromArgb(255, 245, 158, 11)
        $dimColor    = [System.Drawing.Color]::FromArgb(255, 100, 65, 5)
        $whiteColor  = [System.Drawing.Color]::FromArgb(255, 232, 232, 224)

        $bgBrush    = New-Object System.Drawing.SolidBrush($bgColor)
        $amberBrush = New-Object System.Drawing.SolidBrush($amberColor)
        $dimBrush   = New-Object System.Drawing.SolidBrush($dimColor)
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

        # 픽셀 그리드 (밝기가 다른 작은 사각형들로 텍스트 아트 느낌)
        $gridCols  = 8
        $gridRows  = 6
        $margin    = [int]($sz * 0.08)
        $cellW     = ([float]($sz - $margin * 2)) / $gridCols
        $cellH     = ([float]($sz - $margin * 2)) / $gridRows

        # 미리 정의된 밝기 패턴 (알파벳 "A" 형태)
        $pattern = @(
            @(0,0,0,1,1,0,0,0),
            @(0,0,1,1,1,1,0,0),
            @(0,1,1,0,0,1,1,0),
            @(0,1,1,1,1,1,1,0),
            @(0,1,1,0,0,1,1,0),
            @(0,1,1,0,0,1,1,0)
        )

        for ($row = 0; $row -lt $gridRows; $row++) {
            for ($col = 0; $col -lt $gridCols; $col++) {
                $x = [int]($margin + $col * $cellW)
                $y = [int]($margin + $row * $cellH)
                $w = [int]($cellW - 1)
                $h = [int]($cellH - 1)
                if ($w -lt 1) { $w = 1 }
                if ($h -lt 1) { $h = 1 }

                if ($pattern[$row][$col] -eq 1) {
                    $g.FillRectangle($amberBrush, $x, $y, $w, $h)
                } else {
                    $g.FillRectangle($dimBrush, $x, $y, $w, $h)
                }
            }
        }

        $gp.Dispose()
        $bgBrush.Dispose()
        $amberBrush.Dispose()
        $dimBrush.Dispose()
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
