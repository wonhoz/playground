Add-Type -AssemblyName System.Drawing

function Make-DominoBitmap([int]$sz) {
    $bmp = New-Object System.Drawing.Bitmap($sz, $sz)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.Clear([System.Drawing.Color]::FromArgb(26, 26, 26))

    $bg   = [System.Drawing.Color]::FromArgb(26, 26, 26)
    $bone = [System.Drawing.Color]::FromArgb(220, 220, 220)
    $dot  = [System.Drawing.Color]::FromArgb(26, 26, 46)
    $acc  = [System.Drawing.Color]::FromArgb(0, 210, 160)

    $pad = [int]($sz * 0.08)
    $dw  = [int]($sz * 0.20)   # 한 장 너비
    $dh  = [int]($sz * 0.62)   # 한 장 높이
    $gap = [int]($sz * 0.06)   # 장 간격
    $r   = [int]($sz * 0.04)   # 둥근 모서리

    # 도미노 3장: 세워진 것 2개, 쓰러진 것 1개
    $positions = @(
        @{ X = $pad;                     Y = [int]($sz * 0.19); W = $dw; H = $dh; Angle = 0 },
        @{ X = $pad + $dw + $gap;        Y = [int]($sz * 0.19); W = $dw; H = $dh; Angle = 0 },
        @{ X = $pad + ($dw + $gap) * 2;  Y = [int]($sz * 0.19 + $dh - $dw); W = $dh; H = $dw; Angle = 0 }
    )

    $colors = @($bone, $bone, $acc)

    for ($i = 0; $i -lt 3; $i++) {
        $p = $positions[$i]
        $brush = New-Object System.Drawing.SolidBrush($colors[$i])
        $pen   = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(60, 60, 60), 1)
        $g.FillRectangle($brush, $p.X, $p.Y, $p.W, $p.H)
        $g.DrawRectangle($pen, $p.X, $p.Y, $p.W, $p.H)
        $brush.Dispose()
        $pen.Dispose()
    }

    # 첫 두 도미노에 점 그리기
    for ($i = 0; $i -lt 2; $i++) {
        $p  = $positions[$i]
        $ds = [int]([Math]::Max(2, $sz * 0.05))
        $cx = $p.X + $p.W / 2 - $ds / 2
        $b  = New-Object System.Drawing.SolidBrush($dot)
        # 상단 점
        $g.FillEllipse($b, $cx, $p.Y + [int]($p.H * 0.15), $ds, $ds)
        # 하단 점
        $g.FillEllipse($b, $cx, $p.Y + [int]($p.H * 0.65), $ds, $ds)
        $b.Dispose()
    }

    $g.Dispose()
    return $bmp
}

$sizes = @(16, 32, 48, 256)
$bitmaps = @()
foreach ($s in $sizes) {
    $bitmaps += Make-DominoBitmap $s
}

# ICO 파일 직접 작성
$outPath = Join-Path $PSScriptRoot "app.ico"
$stream  = [System.IO.File]::Create($outPath)
$writer  = New-Object System.IO.BinaryWriter($stream)

$count = $bitmaps.Count
# ICONDIR
$writer.Write([uint16]0)      # reserved
$writer.Write([uint16]1)      # type=ICO
$writer.Write([uint16]$count)

# PNG 바이트 배열 수집
$pngArrays = @()
foreach ($bmp in $bitmaps) {
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $pngArrays += ,$ms.ToArray()
    $ms.Dispose()
}

# ICONDIRENTRY 헤더 (16바이트 * count)
$headerBase = 6 + 16 * $count
$offset = $headerBase
for ($i = 0; $i -lt $count; $i++) {
    $sz  = $sizes[$i]
    $byt = $pngArrays[$i]
    $w   = if ($sz -ge 256) { 0 } else { $sz }
    $h   = if ($sz -ge 256) { 0 } else { $sz }
    $writer.Write([byte]$w)
    $writer.Write([byte]$h)
    $writer.Write([byte]0)    # color count
    $writer.Write([byte]0)    # reserved
    $writer.Write([uint16]1)  # planes
    $writer.Write([uint16]32) # bit count
    $writer.Write([uint32]$byt.Length)
    $writer.Write([uint32]$offset)
    $offset += $byt.Length
}

# 이미지 데이터
foreach ($byt in $pngArrays) {
    $writer.Write($byt)
}

$writer.Close()
$stream.Close()
foreach ($bmp in $bitmaps) { $bmp.Dispose() }

Write-Host "app.ico 생성 완료: $outPath"
