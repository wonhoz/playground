Add-Type -AssemblyName System.Drawing

function Make-Bitmap([int]$sz) {
    $bmp = New-Object System.Drawing.Bitmap($sz, $sz)
    $g   = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias

    # 배경
    $g.Clear([System.Drawing.Color]::FromArgb(26, 26, 42))

    $cols = 5
    $pad  = [int]($sz * 0.08)
    $gap  = [int]($sz * 0.02)
    $cell = [int](($sz - 2 * $pad - ($cols - 1) * $gap) / $cols)

    # 모자이크 타일 색상 패턴 (5x5 격자, 웜~쿨 컬러 그라데이션)
    $palette = @(
        @(255,80,20),  @(255,130,20), @(255,180,40), @(255,210,80), @(220,200,60),
        @(230,60,20),  @(255,110,30), @(255,160,50), @(240,195,70), @(180,200,50),
        @(200,50,50),  @(230,90,40),  @(255,140,60), @(220,180,80), @(140,195,80),
        @(160,40,80),  @(200,70,60),  @(230,120,70), @(200,165,90), @(90,180,120),
        @(120,30,100), @(170,50,80),  @(200,100,80), @(180,150,100),@(60,160,160)
    )

    $r = [int]([Math]::Max(2, $sz * 0.04))

    for ($row = 0; $row -lt $cols; $row++) {
        for ($col = 0; $col -lt $cols; $col++) {
            $idx = $row * $cols + $col
            $c   = $palette[$idx]
            $x   = $pad + $col * ($cell + $gap)
            $y   = $pad + $row * ($cell + $gap)

            # 타일 그림자 효과
            $shadow = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(40,0,0,0))
            $g.FillRectangle($shadow, $x+2, $y+2, $cell, $cell)
            $shadow.Dispose()

            # 타일
            $brush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb($c[0],$c[1],$c[2]))
            $rect  = New-Object System.Drawing.Rectangle($x, $y, $cell, $cell)
            $g.FillRectangle($brush, $rect)
            $brush.Dispose()

            # 밝은 테두리
            $pen = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(60,255,255,255), 1)
            $g.DrawRectangle($pen, $x, $y, $cell-1, $cell-1)
            $pen.Dispose()
        }
    }

    $g.Dispose()
    return $bmp
}

# ICO 파일 생성 (16/32/48/256px)
$outPath = Join-Path $PSScriptRoot "app.ico"
$sizes   = @(256, 48, 32, 16)
$bmps    = $sizes | ForEach-Object { Make-Bitmap $_ }

$ms = New-Object System.IO.MemoryStream
$bw = New-Object System.IO.BinaryWriter($ms)

# ICO 헤더
$bw.Write([uint16]0)          # 예약
$bw.Write([uint16]1)          # 타입: ICO
$bw.Write([uint16]$sizes.Count)

# 디렉터리 엔트리 (위치 계산용)
$headerSize  = 6 + 16 * $sizes.Count
$imageOffset = $headerSize

$pngStreams = @()
foreach ($bmp in $bmps) {
    $ps = New-Object System.IO.MemoryStream
    $bmp.Save($ps, [System.Drawing.Imaging.ImageFormat]::Png)
    $pngStreams += $ps
}

for ($i = 0; $i -lt $sizes.Count; $i++) {
    $sz   = $sizes[$i]
    $data = $pngStreams[$i].ToArray()
    $bw.Write([byte]$(if ($sz -ge 256) { 0 } else { $sz }))
    $bw.Write([byte]$(if ($sz -ge 256) { 0 } else { $sz }))
    $bw.Write([byte]0)          # 색상 팔레트
    $bw.Write([byte]0)          # 예약
    $bw.Write([uint16]1)        # 색상 평면
    $bw.Write([uint16]32)       # bpp
    $bw.Write([uint32]$data.Length)
    $bw.Write([uint32]$imageOffset)
    $imageOffset += $data.Length
}

foreach ($ps in $pngStreams) {
    $bw.Write($ps.ToArray())
    $ps.Dispose()
}

$bw.Flush()
[System.IO.File]::WriteAllBytes($outPath, $ms.ToArray())
$bw.Dispose()
$ms.Dispose()
$bmps | ForEach-Object { $_.Dispose() }

Write-Host "아이콘 생성 완료: $outPath"
