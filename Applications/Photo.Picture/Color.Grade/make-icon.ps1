Add-Type -AssemblyName System.Drawing

function Make-Icon {
    param([string]$OutPath)
    $sizes = @(256, 48, 32, 16)
    $bitmaps = @()
    foreach ($sz in $sizes) {
        $bmp = New-Object System.Drawing.Bitmap($sz, $sz)
        $g   = [System.Drawing.Graphics]::FromImage($bmp)
        $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias

        $pad = 2
        $inner = $sz - $pad * 2
        $bgColor = [System.Drawing.Color]::FromArgb(255, 80, 30, 160)
        $g.FillEllipse([System.Drawing.SolidBrush]::new($bgColor), $pad, $pad, $inner, $inner)

        if ($sz -ge 32) {
            $dotSz = [int]([Math]::Round($sz * 0.18))
            $cx = [double]($sz / 2); $cy = [double]($sz / 2)
            $r2 = [double]($sz * 0.26)
            $colors = @(
                [System.Drawing.Color]::FromArgb(255, 255, 100, 100),
                [System.Drawing.Color]::FromArgb(255, 80,  200, 100),
                [System.Drawing.Color]::FromArgb(255, 100, 160, 255),
                [System.Drawing.Color]::FromArgb(255, 255, 220, 60)
            )
            for ($i = 0; $i -lt 4; $i++) {
                $angle = [double]($i * 90 - 45)
                $rad   = $angle * [Math]::PI / 180.0
                $x = [int]($cx + $r2 * [Math]::Cos($rad) - $dotSz / 2)
                $y = [int]($cy + $r2 * [Math]::Sin($rad) - $dotSz / 2)
                $g.FillEllipse([System.Drawing.SolidBrush]::new($colors[$i]), $x, $y, $dotSz, $dotSz)
            }
            # 중앙 흰 원
            $wSz = [int]([Math]::Round($sz * 0.22))
            $wx = [int]($cx - $wSz / 2)
            $wy = [int]($cy - $wSz / 2)
            $g.FillEllipse([System.Drawing.Brushes]::White, $wx, $wy, $wSz, $wSz)
        }
        $g.Dispose()
        $bitmaps += $bmp
    }

    $ms = New-Object System.IO.MemoryStream
    $bw = New-Object System.IO.BinaryWriter($ms)
    $bw.Write([int16]0); $bw.Write([int16]1); $bw.Write([int16]$bitmaps.Count)
    $dataStreams = @()
    foreach ($b in $bitmaps) {
        $ds = New-Object System.IO.MemoryStream
        $b.Save($ds, [System.Drawing.Imaging.ImageFormat]::Png)
        $dataStreams += $ds
    }
    $offset = 6 + 16 * $bitmaps.Count
    for ($i = 0; $i -lt $bitmaps.Count; $i++) {
        $b = $bitmaps[$i]; $ds = $dataStreams[$i]
        $w = if ($b.Width  -ge 256) { 0 } else { $b.Width  }
        $h = if ($b.Height -ge 256) { 0 } else { $b.Height }
        $bw.Write([byte]$w); $bw.Write([byte]$h)
        $bw.Write([byte]0);  $bw.Write([byte]0)
        $bw.Write([int16]1); $bw.Write([int16]32)
        $bw.Write([int32]$ds.Length); $bw.Write([int32]$offset)
        $offset += $ds.Length
    }
    foreach ($ds in $dataStreams) { $bw.Write($ds.ToArray()); $ds.Dispose() }
    foreach ($b  in $bitmaps)    { $b.Dispose() }
    [System.IO.File]::WriteAllBytes($OutPath, $ms.ToArray())
    $ms.Dispose()
    Write-Host "아이콘 생성 완료: $OutPath"
}

Make-Icon -OutPath (Join-Path $PSScriptRoot "Resources\app.ico")
