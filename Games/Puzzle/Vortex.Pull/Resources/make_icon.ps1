# Vortex.Pull 아이콘 생성 — 소용돌이 + 우주선 테마
# 16 / 32 / 48 / 256 px 멀티 사이즈 ICO

param([string]$Out = "$PSScriptRoot\app.ico")

function Make-Bmp {
    param([int]$sz)
    $pixels = New-Object byte[] ($sz * $sz * 4)
    $cx = $sz / 2.0
    $cy = $sz / 2.0
    $maxR = $sz / 2.0

    for ($py = 0; $py -lt $sz; $py++) {
        for ($px = 0; $px -lt $sz; $px++) {
            $dx = $px - $cx
            $dy = $py - $cy
            $r  = [Math]::Sqrt($dx*$dx + $dy*$dy)
            $a  = [Math]::Atan2($dy, $dx)
            $t  = ($r / $maxR)   # 0=center, 1=edge

            # 소용돌이 나선 패턴
            $spiral = ($a + $r * 0.18) % ([Math]::PI * 2)
            if ($spiral -lt 0) { $spiral += [Math]::PI * 2 }
            $band = [Math]::Abs([Math]::Sin($spiral * 2.5))

            # 배경: 우주 (깊은 파랑)
            $bgR = [int](10 + 20 * $t)
            $bgG = [int](5  + 15 * $t)
            $bgB = [int](30 + 50 * $t)

            # 소용돌이 색: 청록 → 보라
            $sw  = $band * (1.0 - $t * 0.5)
            $sR  = [int](60  * $sw + 120 * $t * $sw)
            $sG  = [int](180 * $sw * (1.0 - $t * 0.5))
            $sB  = [int](220 * $sw)

            $red   = [Math]::Min(255, $bgR + $sR)
            $green = [Math]::Min(255, $bgG + $sG)
            $blue  = [Math]::Min(255, $bgB + $sB)
            $alpha = 255

            # 중심 밝은 코어
            if ($r -lt $maxR * 0.18) {
                $core = 1.0 - ($r / ($maxR * 0.18))
                $red   = [Math]::Min(255, $red   + [int](200 * $core))
                $green = [Math]::Min(255, $green + [int](220 * $core))
                $blue  = [Math]::Min(255, $blue  + [int](255 * $core))
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

# ICO 헤더
$writer.Write([uint16]0)       # Reserved
$writer.Write([uint16]1)       # Type: ICO
$writer.Write([uint16]$sizes.Count)

# 디렉터리 항목 오프셋 계산
$dirSize   = 16 * $sizes.Count
$headerSz  = 6 + $dirSize
$offsets   = @{}
$offset    = $headerSz

foreach ($sz in $sizes) {
    $pixels = $bmpData[$sz]
    $bmpInfoSz  = 40
    $pixelBytes = $sz * $sz * 4
    $totalSz    = $bmpInfoSz + $pixelBytes
    $offsets[$sz] = @{ offset = $offset; size = $totalSz }
    $offset += $totalSz
}

foreach ($sz in $sizes) {
    $info = $offsets[$sz]
    if ($sz -ge 256) { $wb = [byte]0 } else { $wb = [byte]$sz }
    $writer.Write($wb)             # Width
    $writer.Write($wb)             # Height
    $writer.Write([byte]0)         # ColorCount
    $writer.Write([byte]0)         # Reserved
    $writer.Write([uint16]1)       # Planes
    $writer.Write([uint16]32)      # BitCount
    $writer.Write([uint32]$info.size)
    $writer.Write([uint32]$info.offset)
}

foreach ($sz in $sizes) {
    $pixels = $bmpData[$sz]
    # BITMAPINFOHEADER
    $writer.Write([uint32]40)           # biSize
    $writer.Write([int32]$sz)           # biWidth
    $writer.Write([int32]($sz * 2))     # biHeight (ICO: 2x)
    $writer.Write([uint16]1)            # biPlanes
    $writer.Write([uint16]32)           # biBitCount
    $writer.Write([uint32]0)            # biCompression
    $writer.Write([uint32]($sz*$sz*4))  # biSizeImage
    $writer.Write([int32]0)
    $writer.Write([int32]0)
    $writer.Write([uint32]0)
    $writer.Write([uint32]0)
    # 픽셀 (상하 반전)
    for ($row = ($sz - 1); $row -ge 0; $row--) {
        $start = $row * $sz * 4
        $writer.Write($pixels, $start, $sz * 4)
    }
}

$writer.Close()
$stream.Close()
Write-Host "ICO 생성 완료: $Out"
