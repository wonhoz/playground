Add-Type -AssemblyName System.Drawing

$sizes = @(16, 32, 48, 256)
$dir   = Split-Path -Parent $MyInvocation.MyCommand.Path
$out   = Join-Path $dir "Resources\app.ico"
New-Item -ItemType Directory -Force -Path (Join-Path $dir "Resources") | Out-Null

$streams = @()
foreach ($sz in $sizes) {
    $bmp = New-Object System.Drawing.Bitmap($sz, $sz)
    $g   = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.Clear([System.Drawing.Color]::Transparent)

    # 배경 원
    $bgBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 8, 8, 16))
    $g.FillEllipse($bgBrush, 1, 1, $sz-2, $sz-2)

    if ($sz -ge 32) {
        # 건반 3개 (흰색)
        $keyW  = [int]($sz * 0.13)
        $keyH  = [int]($sz * 0.45)
        $keyY  = [int]($sz * 0.28)
        $gap   = [int]($sz * 0.06)
        $totalW = $keyW * 3 + $gap * 2
        $startX = ($sz - $totalW) / 2
        $wBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(220, 220, 255))
        for ($k = 0; $k -lt 3; $k++) {
            $kx = [int]($startX + $k * ($keyW + $gap))
            $g.FillRectangle($wBrush, $kx, $keyY, $keyW, $keyH)
        }
        # 검은 건반 2개
        $bkW = [int]($keyW * 0.75)
        $bkH = [int]($keyH * 0.6)
        $bkBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(40, 60, 200))
        $bkX1 = [int]($startX + $keyW - $bkW / 2)
        $bkX2 = [int]($startX + $keyW * 2 + $gap - $bkW / 2)
        $g.FillRectangle($bkBrush, $bkX1, $keyY, $bkW, $bkH)
        $g.FillRectangle($bkBrush, $bkX2 + $gap, $keyY, $bkW, $bkH)
        # 노란 노트 (떨어지는 느낌)
        $noteBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 224, 64))
        $noteH = [int]($sz * 0.10)
        $g.FillRectangle($noteBrush, [int]($startX), [int]($sz * 0.12), $keyW, $noteH)
        $g.FillRectangle($noteBrush, [int]($startX + $keyW + $gap), [int]($sz * 0.18), $keyW, $noteH)
    } else {
        # 작은 사이즈: 단순 음표
        $noteBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 224, 64))
        $r = [int]($sz * 0.22)
        $cx = [int]($sz * 0.38); $cy = [int]($sz * 0.62)
        $g.FillEllipse($noteBrush, $cx - $r, $cy - $r, $r*2, $r*2)
        $stemPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255, 224, 64), [int]([Math]::Max(1, $sz * 0.1)))
        $g.DrawLine($stemPen, $cx + $r - 1, $cy - $r, $cx + $r - 1, $cy - $r - [int]($sz * 0.4))
    }

    $g.Dispose()

    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $streams += @{ Size = $sz; Data = $ms.ToArray() }
    $ms.Dispose()
    $bmp.Dispose()
}

# ICO 헤더 조합
$ico = New-Object System.IO.MemoryStream
$bw  = New-Object System.IO.BinaryWriter($ico)

$bw.Write([uint16]0)                      # reserved
$bw.Write([uint16]1)                      # ICO type
$bw.Write([uint16]$streams.Count)

$headerSize = 6 + 16 * $streams.Count
$offset = $headerSize

foreach ($s in $streams) {
    $szVal = $s.Size
    $wb = if ($szVal -ge 256) { 0 } else { $szVal }
    $hb = if ($szVal -ge 256) { 0 } else { $szVal }
    $bw.Write([byte]$wb)
    $bw.Write([byte]$hb)
    $bw.Write([byte]0)                    # color count
    $bw.Write([byte]0)                    # reserved
    $bw.Write([uint16]1)                  # planes
    $bw.Write([uint16]32)                 # bit count
    $bw.Write([uint32]$s.Data.Length)
    $bw.Write([uint32]$offset)
    $offset += $s.Data.Length
}

foreach ($s in $streams) {
    $bw.Write($s.Data)
}

$bw.Flush()
[System.IO.File]::WriteAllBytes($out, $ico.ToArray())
$ico.Dispose()

Write-Host "ICO 생성 완료: $out"
