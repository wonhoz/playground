# Orbit.Craft 아이콘 생성 — 행성 궤도 테마
param([string]$Out = "$PSScriptRoot\app.ico")

function Make-Bmp {
    param([int]$sz)
    $pixels = New-Object byte[] ($sz * $sz * 4)
    $cx = $sz / 2.0; $cy = $sz / 2.0; $maxR = $sz / 2.0

    for ($py = 0; $py -lt $sz; $py++) {
        for ($px = 0; $px -lt $sz; $px++) {
            $dx = $px - $cx; $dy = $py - $cy
            $r  = [Math]::Sqrt($dx*$dx + $dy*$dy)
            $t  = $r / $maxR

            # 우주 배경
            $bgR = [int](8  + 12 * $t)
            $bgG = [int](4  + 10 * $t)
            $bgB = [int](20 + 40 * $t)

            $red = $bgR; $green = $bgG; $blue = $bgB; $alpha = 255

            # 궤도 링 (여러 타원 링)
            foreach ($orR in @(0.32, 0.52, 0.72)) {
                $ringDist = [Math]::Abs($t - $orR) * $sz
                if ($ringDist -lt 1.5) {
                    $strength = [Math]::Max(0, 1 - $ringDist / 1.5)
                    $red   = [Math]::Min(255, $red   + [int](90  * $strength))
                    $green = [Math]::Min(255, $green + [int](140 * $strength))
                    $blue  = [Math]::Min(255, $blue  + [int](200 * $strength))
                }
            }

            # 중심 별 (노란 코어)
            if ($r -lt $maxR * 0.14) {
                $c = 1.0 - $r / ($maxR * 0.14)
                $red   = [Math]::Min(255, $red   + [int](255 * $c))
                $green = [Math]::Min(255, $green + [int](210 * $c))
                $blue  = [Math]::Min(255, $blue  + [int](80  * $c))
            }

            # 행성 1 (작은 청색 점, 궤도 2에)
            $a2  = 0.52 * $maxR
            $px1 = $cx + $a2 * 0.71; $py1 = $cy - $a2 * 0.71
            $dp  = [Math]::Sqrt(($px - $px1)*($px - $px1) + ($py - $py1)*($py - $py1))
            if ($dp -lt $maxR * 0.085) {
                $c = [Math]::Max(0, 1 - $dp / ($maxR * 0.085))
                $red   = [Math]::Min(255, $red   + [int](50  * $c))
                $green = [Math]::Min(255, $green + [int](120 * $c))
                $blue  = [Math]::Min(255, $blue  + [int](220 * $c))
            }

            # 행성 2 (청록 점, 궤도 3에)
            $a3  = 0.72 * $maxR
            $px2 = $cx - $a3 * 0.5; $py2 = $cy + $a3 * 0.866
            $dp2 = [Math]::Sqrt(($px - $px2)*($px - $px2) + ($py - $py2)*($py - $py2))
            if ($dp2 -lt $maxR * 0.065) {
                $c = [Math]::Max(0, 1 - $dp2 / ($maxR * 0.065))
                $red   = [Math]::Min(255, $red   + [int](30  * $c))
                $green = [Math]::Min(255, $green + [int](220 * $c))
                $blue  = [Math]::Min(255, $blue  + [int](180 * $c))
            }

            # 원형 클리핑
            if ($r -gt $maxR) { $alpha = 0 }

            $idx = ($py * $sz + $px) * 4
            $pixels[$idx]   = [byte]$blue
            $pixels[$idx+1] = [byte]$green
            $pixels[$idx+2] = [byte]$red
            $pixels[$idx+3] = [byte]$alpha
        }
    }
    return $pixels
}

$sizes = @(16, 32, 48, 256)
$bmpData = @{}
foreach ($sz in $sizes) { $bmpData[$sz] = Make-Bmp -sz $sz }

$stream = [System.IO.File]::Create($Out)
$writer = New-Object System.IO.BinaryWriter($stream)
$writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$sizes.Count)

$headerSz = 6 + 16 * $sizes.Count
$offset   = $headerSz
$offsets  = @{}
foreach ($sz in $sizes) {
    $totalSz = 40 + $sz * $sz * 4
    $offsets[$sz] = @{ offset = $offset; size = $totalSz }
    $offset += $totalSz
}
foreach ($sz in $sizes) {
    $info = $offsets[$sz]
    if ($sz -ge 256) { $wb = [byte]0 } else { $wb = [byte]$sz }
    $writer.Write($wb); $writer.Write($wb); $writer.Write([byte]0); $writer.Write([byte]0)
    $writer.Write([uint16]1); $writer.Write([uint16]32)
    $writer.Write([uint32]$info.size); $writer.Write([uint32]$info.offset)
}
foreach ($sz in $sizes) {
    $pixels = $bmpData[$sz]
    $writer.Write([uint32]40); $writer.Write([int32]$sz); $writer.Write([int32]($sz * 2))
    $writer.Write([uint16]1); $writer.Write([uint16]32); $writer.Write([uint32]0)
    $writer.Write([uint32]($sz*$sz*4)); $writer.Write([int32]0); $writer.Write([int32]0)
    $writer.Write([uint32]0); $writer.Write([uint32]0)
    for ($row = ($sz - 1); $row -ge 0; $row--) {
        $writer.Write($pixels, $row * $sz * 4, $sz * 4)
    }
}
$writer.Close(); $stream.Close()
Write-Host "ICO 생성 완료: $Out"
