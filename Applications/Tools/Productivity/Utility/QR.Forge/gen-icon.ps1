Add-Type -AssemblyName System.Drawing

function New-IcoFile {
    param([string]$Path, [int[]]$Sizes)

    $pngData = @{}
    foreach ($sz in $Sizes) {
        $bmp = New-Object System.Drawing.Bitmap($sz, $sz)
        $g = [System.Drawing.Graphics]::FromImage($bmp)
        $g.SmoothingMode     = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit

        # 배경: 투명 (나중에 라운드 사각형으로 채움)
        $g.Clear([System.Drawing.Color]::Transparent)

        # 보라색 라운드 사각형 배경
        $bgBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 20, 15, 30))
        $accentBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 124, 95, 232))
        $whiteBrush  = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 240, 235, 255))

        # 배경 채우기
        $gp = New-Object System.Drawing.Drawing2D.GraphicsPath
        $radius = [int]($sz * 0.2)
        $gp.AddArc(0, 0, $radius * 2, $radius * 2, 180, 90)
        $gp.AddArc($sz - $radius * 2, 0, $radius * 2, $radius * 2, 270, 90)
        $gp.AddArc($sz - $radius * 2, $sz - $radius * 2, $radius * 2, $radius * 2, 0, 90)
        $gp.AddArc(0, $sz - $radius * 2, $radius * 2, $radius * 2, 90, 90)
        $gp.CloseFigure()
        $g.FillPath($bgBrush, $gp)

        # QR 격자 패턴 (3×3 모듈 - 가운데)
        # 중앙 영역에 QR 패턴 그리기
        $pad  = [int]($sz * 0.18)
        $area = $sz - $pad * 2
        $cell = [int]($area / 7)

        # QR 패턴: 흰 셀들
        $pattern = @(
            @(1,1,1,0,1,1,1),
            @(1,0,1,0,1,0,1),
            @(1,1,1,0,1,1,1),
            @(0,0,0,0,0,0,0),
            @(1,1,1,0,1,0,0),
            @(1,0,0,0,0,1,0),
            @(1,1,1,0,0,0,1)
        )

        for ($row = 0; $row -lt 7; $row++) {
            for ($col = 0; $col -lt 7; $col++) {
                if ($pattern[$row][$col] -eq 1) {
                    $x = $pad + $col * $cell
                    $y = $pad + $row * $cell
                    if ($cell -ge 3) {
                        $g.FillRectangle($whiteBrush, $x + 1, $y + 1, $cell - 1, $cell - 1)
                    } else {
                        $g.FillRectangle($whiteBrush, $x, $y, $cell, $cell)
                    }
                } else {
                    # 어두운 셀 — 보라색
                    $x = $pad + $col * $cell
                    $y = $pad + $row * $cell
                    if ($cell -ge 3) {
                        $g.FillRectangle($accentBrush, $x + 1, $y + 1, $cell - 1, $cell - 1)
                    }
                }
            }
        }

        $gp.Dispose()
        $bgBrush.Dispose()
        $accentBrush.Dispose()
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
