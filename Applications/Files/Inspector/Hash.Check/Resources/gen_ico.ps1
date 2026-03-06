Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms

function Make-Bitmap {
    param([int]$sz)

    $bmp = New-Object System.Drawing.Bitmap($sz, $sz)
    $g   = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode   = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic

    # 배경 #111118
    $bg = [System.Drawing.Color]::FromArgb(0xFF, 0x11, 0x11, 0x18)
    $g.Clear($bg)

    $pad    = [int]($sz * 0.08)
    $shield = $sz - $pad * 2

    # 방패 그리기 (초록 #22C55E 외곽, 내부 어두운 초록 반투명)
    $shieldPts = @(
        [System.Drawing.PointF]::new($pad + $shield * 0.5, $pad),
        [System.Drawing.PointF]::new($pad + $shield,        $pad + $shield * 0.3),
        [System.Drawing.PointF]::new($pad + $shield,        $pad + $shield * 0.6),
        [System.Drawing.PointF]::new($pad + $shield * 0.5,  $pad + $shield),
        [System.Drawing.PointF]::new($pad,                  $pad + $shield * 0.6),
        [System.Drawing.PointF]::new($pad,                  $pad + $shield * 0.3)
    )

    $brushShield = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(60, 0x22, 0xC5, 0x5E))
    $penShield   = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(0xFF, 0x22, 0xC5, 0x5E), [float]($sz * 0.07))
    $penShield.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round

    $g.FillPolygon($brushShield, $shieldPts)
    $g.DrawPolygon($penShield, $shieldPts)

    # 체크마크 #22C55E
    $cx = $pad + $shield * 0.5
    $cy = $pad + $shield * 0.55
    $ck = $shield * 0.22

    $checkPts = @(
        [System.Drawing.PointF]::new($cx - $ck,       $cy),
        [System.Drawing.PointF]::new($cx - $ck * 0.2, $cy + $ck * 0.85),
        [System.Drawing.PointF]::new($cx + $ck,        $cy - $ck * 0.6)
    )
    $penCheck = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(0xFF, 0x22, 0xC5, 0x5E), [float]($sz * 0.1))
    $penCheck.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $penCheck.EndCap   = [System.Drawing.Drawing2D.LineCap]::Round
    $penCheck.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round

    $g.DrawLines($penCheck, $checkPts)

    $g.Dispose()
    return $bmp
}

$sizes = @(16, 32, 48, 256)

$outPath = Join-Path $PSScriptRoot "app.ico"

# ICO 파일 직접 구성
$ms = New-Object System.IO.MemoryStream

$bitmaps = @{}
$pngs    = @{}
foreach ($sz in $sizes) {
    $bmp = Make-Bitmap $sz
    $pngMs = New-Object System.IO.MemoryStream
    $bmp.Save($pngMs, [System.Drawing.Imaging.ImageFormat]::Png)
    $bitmaps[$sz] = $bmp
    $pngs[$sz]    = $pngMs.ToArray()
    $pngMs.Dispose()
    $bmp.Dispose()
}

$writer = New-Object System.IO.BinaryWriter($ms)

# ICO Header
$writer.Write([uint16]0)         # reserved
$writer.Write([uint16]1)         # type = icon
$writer.Write([uint16]$sizes.Count)

$offset = 6 + 16 * $sizes.Count  # header + directory entries

foreach ($sz in $sizes) {
    $data = $pngs[$sz]
    $w    = if ($sz -ge 256) { 0 } else { $sz }
    $h    = if ($sz -ge 256) { 0 } else { $sz }
    $writer.Write([byte]$w)
    $writer.Write([byte]$h)
    $writer.Write([byte]0)   # color count
    $writer.Write([byte]0)   # reserved
    $writer.Write([uint16]1) # planes
    $writer.Write([uint16]32)# bit count
    $writer.Write([uint32]$data.Length)
    $writer.Write([uint32]$offset)
    $offset += $data.Length
}

foreach ($sz in $sizes) {
    $writer.Write($pngs[$sz])
}

$writer.Flush()
$bytes = $ms.ToArray()
$ms.Dispose()

[System.IO.File]::WriteAllBytes($outPath, $bytes)
Write-Host "app.ico 생성 완료: $outPath"
