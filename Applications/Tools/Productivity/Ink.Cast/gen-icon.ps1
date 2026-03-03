Add-Type -AssemblyName System.Drawing

function New-IcoFile {
    param([string]$Path, [int[]]$Sizes)

    $pngData = @{}
    foreach ($sz in $Sizes) {
        $bmp = New-Object System.Drawing.Bitmap($sz, $sz)
        $g   = [System.Drawing.Graphics]::FromImage($bmp)
        $g.SmoothingMode     = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit

        # 배경: 딥 네이비 (#12122A)
        $g.Clear([System.Drawing.Color]::FromArgb(255, 18, 18, 42))

        # 보라색 계열 그라데이션 브러시
        $cx = $sz / 2.0; $cy = $sz / 2.0
        $r  = $sz * 0.42

        # 잉크방울 느낌: 타원 + 아래 뾰족
        $pts = New-Object System.Drawing.Drawing2D.GraphicsPath
        $pts.AddEllipse($cx - $r * 0.58, $cy - $r * 0.72, $r * 1.16, $r * 1.0)

        # 외부 글로우
        $glowBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(60, 124, 106, 244))
        $g.FillEllipse($glowBrush, $cx - $r * 0.7, $cy - $r * 0.85, $r * 1.4, $r * 1.2)
        $glowBrush.Dispose()

        # 메인 원형 (잉크 도트)
        $mainBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 44, 44, 90))
        $g.FillEllipse($mainBrush, $cx - $r * 0.5, $cy - $r * 0.65, $r * 1.0, $r * 0.95)
        $mainBrush.Dispose()

        # 테두리 링
        $borderPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(220, 124, 106, 244), [float]($sz * 0.06))
        $g.DrawEllipse($borderPen, $cx - $r * 0.5, $cy - $r * 0.65, $r * 1.0, $r * 0.95)
        $borderPen.Dispose()

        # 중앙 ✦ 문자
        $starBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 203, 166, 247))
        $fs = [int]($sz * 0.28); if ($fs -lt 5) { $fs = 5 }
        $font = New-Object System.Drawing.Font("Segoe UI Symbol", $fs, [System.Drawing.FontStyle]::Regular)
        $sf   = New-Object System.Drawing.StringFormat
        $sf.Alignment     = [System.Drawing.StringAlignment]::Center
        $sf.LineAlignment = [System.Drawing.StringAlignment]::Center
        $rect = [System.Drawing.RectangleF]::new($cx - $r * 0.5, $cy - $r * 0.65, $r * 1.0, $r * 0.95)
        $g.DrawString([char]0x2726, $font, $starBrush, $rect, $sf)  # ✦
        $font.Dispose(); $starBrush.Dispose(); $sf.Dispose()

        # 하단 줄무늬 (마크다운 느낌)
        $linePen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(100, 124, 106, 244), [float]($sz * 0.04))
        if ($sz -ge 32) {
            $lx1 = $cx - $r * 0.62; $lx2 = $cx + $r * 0.62
            $ly1 = $cy + $r * 0.46; $ly2 = $ly1 + $sz * 0.05
            $ly3 = $ly2 + $sz * 0.09
            $g.DrawLine($linePen, $lx1, $ly1, $lx2 * 0.85, $ly1)
            $g.DrawLine($linePen, $lx1, $ly3, $lx2 * 0.65, $ly3)
        }
        $linePen.Dispose()

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
    $bw.Write([uint16]0)
    $bw.Write([uint16]1)
    $bw.Write([uint16]$Sizes.Count)

    $headerSize = 6 + $Sizes.Count * 16
    $offset = $headerSize

    foreach ($sz in $Sizes) {
        $data = $pngData[$sz]
        $w = if ($sz -ge 256) { 0 } else { $sz }
        $h = if ($sz -ge 256) { 0 } else { $sz }
        $bw.Write([byte]$w);  $bw.Write([byte]$h)
        $bw.Write([byte]0);   $bw.Write([byte]0)
        $bw.Write([uint16]1); $bw.Write([uint16]32)
        $bw.Write([uint32]$data.Length)
        $bw.Write([uint32]$offset)
        $offset += $data.Length
    }
    foreach ($sz in $Sizes) { $bw.Write($pngData[$sz]) }
    $bw.Flush(); $bw.Close(); $fs2.Close()
    Write-Output "ICO 생성 완료: $Path"
}

New-IcoFile -Path "$PSScriptRoot\Resources\app.ico" -Sizes @(16, 32, 48, 256)
