Add-Type -AssemblyName System.Drawing

function Make-Icon {
    param([string]$OutPath)

    $sizes = @(256, 48, 32, 16)
    $bitmaps = @()

    foreach ($sz in $sizes) {
        $bmp = New-Object System.Drawing.Bitmap($sz, $sz)
        $g   = [System.Drawing.Graphics]::FromImage($bmp)
        $g.SmoothingMode   = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAlias

        # 배경
        $g.FillRectangle([System.Drawing.Brushes]::Transparent, 0, 0, $sz, $sz)
        $bg = [System.Drawing.Color]::FromArgb(255, 18, 18, 30)
        $g.FillEllipse([System.Drawing.SolidBrush]::new($bg), 2, 2, $sz-4, $sz-4)

        # 음표 이모지 (큰 사이즈)
        if ($sz -ge 32) {
            $fontSize = [int]($sz * 0.52)
            $font  = New-Object System.Drawing.Font("Segoe UI Emoji", $fontSize)
            $brush = [System.Drawing.Brushes]::White
            $sf    = New-Object System.Drawing.StringFormat
            $sf.Alignment         = [System.Drawing.StringAlignment]::Center
            $sf.LineAlignment     = [System.Drawing.StringAlignment]::Center
            $rect = New-Object System.Drawing.RectangleF(0, 0, $sz, $sz)
            $g.DrawString([char]::ConvertFromUtf32(0x1F3B5), $font, $brush, $rect, $sf)
            $font.Dispose()
        }

        # 태그 라벨 (하단 우측)
        if ($sz -ge 48) {
            $tagSz = [int]($sz * 0.38)
            $tagX  = $sz - $tagSz - 2
            $tagY  = $sz - $tagSz - 2
            $accent = [System.Drawing.Color]::FromArgb(255, 96, 165, 250)
            $g.FillEllipse([System.Drawing.SolidBrush]::new($accent), $tagX, $tagY, $tagSz, $tagSz)
            $fontSm = New-Object System.Drawing.Font("Segoe UI", ([int]($tagSz * 0.5)), [System.Drawing.FontStyle]::Bold)
            $sfC    = New-Object System.Drawing.StringFormat
            $sfC.Alignment     = [System.Drawing.StringAlignment]::Center
            $sfC.LineAlignment = [System.Drawing.StringAlignment]::Center
            $rectSm = New-Object System.Drawing.RectangleF($tagX, $tagY, $tagSz, $tagSz)
            $g.DrawString("T", $fontSm, [System.Drawing.Brushes]::White, $rectSm, $sfC)
            $fontSm.Dispose()
        }

        $g.Dispose()
        $bitmaps += $bmp
    }

    # ICO 파일 조합
    $ms = New-Object System.IO.MemoryStream
    $bw = New-Object System.IO.BinaryWriter($ms)

    # ICONDIR
    $bw.Write([int16]0)        # reserved
    $bw.Write([int16]1)        # type = 1 (ICO)
    $bw.Write([int16]$bitmaps.Count)

    $dataStreams = @()
    foreach ($b in $bitmaps) {
        $ds = New-Object System.IO.MemoryStream
        $b.Save($ds, [System.Drawing.Imaging.ImageFormat]::Png)
        $dataStreams += $ds
    }

    $offset = 6 + 16 * $bitmaps.Count
    for ($i = 0; $i -lt $bitmaps.Count; $i++) {
        $b  = $bitmaps[$i]
        $ds = $dataStreams[$i]
        $w  = if ($b.Width  -ge 256) { 0 } else { $b.Width  }
        $h  = if ($b.Height -ge 256) { 0 } else { $b.Height }
        $bw.Write([byte]$w)
        $bw.Write([byte]$h)
        $bw.Write([byte]0)   # color count
        $bw.Write([byte]0)   # reserved
        $bw.Write([int16]1)  # planes
        $bw.Write([int16]32) # bpp
        $bw.Write([int32]$ds.Length)
        $bw.Write([int32]$offset)
        $offset += $ds.Length
    }
    foreach ($ds in $dataStreams) {
        $bw.Write($ds.ToArray())
        $ds.Dispose()
    }
    foreach ($b in $bitmaps) { $b.Dispose() }

    [System.IO.File]::WriteAllBytes($OutPath, $ms.ToArray())
    $ms.Dispose()
    Write-Host "아이콘 생성 완료: $OutPath"
}

$out = Join-Path $PSScriptRoot "Resources\app.ico"
Make-Icon -OutPath $out
