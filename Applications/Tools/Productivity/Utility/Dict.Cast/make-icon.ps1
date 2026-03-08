# Dict.Cast 아이콘 생성 스크립트
# 책 + 돋보기 테마 / 배경 #13131F / 파란 계열 강조
param([string]$Out = "$PSScriptRoot\Resources\app.ico")

Add-Type -AssemblyName System.Drawing

function New-DictBitmap([int]$sz) {
    $bmp = New-Object System.Drawing.Bitmap($sz, $sz)
    $g   = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode   = 'AntiAlias'
    $g.TextRenderingHint = 'AntiAliasGridFit'

    # 배경
    $bgBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255,19,19,31))
    $g.FillRectangle($bgBrush, 0, 0, $sz, $sz)

    $s = $sz / 256.0  # 스케일 팩터

    # 책 본체 (둥근 사각형 근사)
    $bookX = [int](40 * $s); $bookY = [int](36 * $s)
    $bookW = [int](140 * $s); $bookH = [int](168 * $s)
    $bookBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255,30,58,138))
    $g.FillRectangle($bookBrush, $bookX, $bookY, $bookW, $bookH)

    # 책 밝은 페이지 영역
    $pageBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(255,239,246,255))
    $g.FillRectangle($pageBrush, [int](($bookX+12)*$s/($s)), [int](($bookY+12)*1), [int](($bookW-24)*1), [int](($bookH-24)*1))

    # 척추 선
    $spinePen = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(255,96,165,250), [float](4*$s))
    $spineX = [int](($bookX + $bookW * 0.22))
    $g.DrawLine($spinePen, $spineX, $bookY, $spineX, $bookY + $bookH)

    # 페이지 라인들
    $linePen = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(180,148,163,200), [float](1.5*$s))
    $lineLeft  = $spineX + [int](8*$s)
    $lineRight = $bookX + $bookW - [int](14*$s)
    for ($row = 0; $row -lt 5; $row++) {
        $ly = $bookY + [int](36*$s) + $row * [int](24*$s)
        $g.DrawLine($linePen, $lineLeft, $ly, $lineRight, $ly)
    }

    # 돋보기 원
    $mgX = [int](128 * $s); $mgY = [int](128 * $s)
    $mgR = [int](68  * $s)
    $mgBg = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(230,19,19,31))
    $g.FillEllipse($mgBg, $mgX - $mgR, $mgY - $mgR, $mgR*2, $mgR*2)
    $mgPen = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(255,96,165,250), [float](10*$s))
    $g.DrawEllipse($mgPen, $mgX - $mgR, $mgY - $mgR, $mgR*2, $mgR*2)

    # 돋보기 손잡이
    $hPen = [System.Drawing.Pen]::new([System.Drawing.Color]::FromArgb(255,96,165,250), [float](10*$s))
    $hPen.StartCap = 'Round'; $hPen.EndCap = 'Round'
    $hx1 = [int](($mgX + $mgR * 0.70)); $hy1 = [int](($mgY + $mgR * 0.70))
    $hx2 = [int](228 * $s);             $hy2 = [int](228 * $s)
    $g.DrawLine($hPen, $hx1, $hy1, $hx2, $hy2)

    $g.Dispose()
    return $bmp
}

New-Item -ItemType Directory -Force -Path (Split-Path $Out) | Out-Null

$sizes = @(256, 48, 32, 16)
$bitmaps = $sizes | ForEach-Object { New-DictBitmap $_ }

# ICO 파일 직접 작성
$stream = [System.IO.File]::OpenWrite($Out)
$writer = New-Object System.IO.BinaryWriter($stream)

$count = $sizes.Count

# ICONDIR
$writer.Write([uint16]0)     # reserved
$writer.Write([uint16]1)     # type: ICO
$writer.Write([uint16]$count)

$pngDataList = @()
foreach ($bmp in $bitmaps) {
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $pngDataList += ,$ms.ToArray()
    $ms.Dispose()
}

$offset = 6 + $count * 16
for ($i = 0; $i -lt $count; $i++) {
    $sz   = $sizes[$i]
    $data = $pngDataList[$i]
    $w = if ($sz -ge 256) { 0 } else { $sz }
    $h = if ($sz -ge 256) { 0 } else { $sz }
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

foreach ($data in $pngDataList) {
    $writer.Write($data)
}

$writer.Close()
$stream.Close()

foreach ($bmp in $bitmaps) { $bmp.Dispose() }

Write-Host "아이콘 생성 완료: $Out"
