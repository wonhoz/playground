Add-Type -AssemblyName System.Drawing

function New-KeyMapIcon {
    param([int]$sz)
    $bmp = New-Object System.Drawing.Bitmap($sz, $sz)
    $g   = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.Clear([System.Drawing.Color]::Transparent)

    # 배경
    $bgb = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255,19,19,31))
    $g.FillRectangle($bgb, 0, 0, $sz, $sz)

    # 키캡 3개 (격자)
    $keyColor = [System.Drawing.Color]::FromArgb(255, 30, 30, 46)
    $keyBdr   = [System.Drawing.Color]::FromArgb(255, 53, 53, 80)
    $accentC  = [System.Drawing.Color]::FromArgb(255, 6, 182, 212)

    function Draw-Key($x, $y, $w, $h, $accent) {
        $kb = New-Object System.Drawing.SolidBrush($keyColor)
        $kp = New-Object System.Drawing.Pen($keyBdr, 1)
        $r  = New-Object System.Drawing.Rectangle($x, $y, $w, $h)
        $g.FillRectangle($kb, $r)
        $g.DrawRectangle($kp, $r)
        if ($accent) {
            $ab = New-Object System.Drawing.SolidBrush($accentC)
            $ar = New-Object System.Drawing.Rectangle($x, $y, $w, $h)
            $g.FillRectangle($ab, $ar)
            $ab.Dispose()
        }
        $kb.Dispose(); $kp.Dispose()
    }

    $pad = [int]($sz * 0.08)
    $gap = [int]($sz * 0.06)
    $kw  = [int](($sz - $pad*2 - $gap*2) / 3)
    $kh  = [int]($kw * 0.85)
    $row1y = $pad
    $row2y = $pad + $kh + $gap
    $row3y = $pad + ($kh + $gap) * 2

    # 3x3 그리드, 일부 키 강조
    for ($c = 0; $c -lt 3; $c++) {
        $kx = $pad + $c * ($kw + $gap)
        Draw-Key $kx $row1y $kw $kh ($c -eq 1)
        Draw-Key $kx $row2y $kw $kh ($false)
        if ($c -lt 2) { Draw-Key $kx $row3y $kw $kh ($false) }
    }
    # 스페이스 바 (하단 넓은 키)
    $spaceW = $kw * 2 + $gap
    $spaceX = $pad + $kw + $gap
    Draw-Key $spaceX $row3y $spaceW $kh ($false)

    $g.Dispose(); $bgb.Dispose()
    return $bmp
}

$sizes = @(256, 48, 32, 16)
$bitmaps = @{}
foreach ($sz in $sizes) { $bitmaps[$sz] = New-KeyMapIcon -sz $sz }

$outPath = Join-Path $PSScriptRoot "app.ico"
$ms      = New-Object System.IO.MemoryStream
$writer  = New-Object System.IO.BinaryWriter($ms)
$writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$sizes.Count)

$imgStreams = @{}
foreach ($sz in $sizes) {
    $s = New-Object System.IO.MemoryStream
    $bitmaps[$sz].Save($s, [System.Drawing.Imaging.ImageFormat]::Png)
    $imgStreams[$sz] = $s
}

$offset = 6 + 16 * $sizes.Count
foreach ($sz in $sizes) {
    $imgData = $imgStreams[$sz].ToArray()
    $w = if ($sz -ge 256) { 0 } else { $sz }
    $h = if ($sz -ge 256) { 0 } else { $sz }
    $writer.Write([byte]$w); $writer.Write([byte]$h)
    $writer.Write([byte]0); $writer.Write([byte]0)
    $writer.Write([uint16]1); $writer.Write([uint16]32)
    $writer.Write([uint32]$imgData.Length); $writer.Write([uint32]$offset)
    $offset += $imgData.Length
}
foreach ($sz in $sizes) { $writer.Write($imgStreams[$sz].ToArray()) }

[System.IO.File]::WriteAllBytes($outPath, $ms.ToArray())
Write-Host "app.ico 생성 완료: $outPath"
foreach ($sz in $sizes) { $bitmaps[$sz].Dispose() }
