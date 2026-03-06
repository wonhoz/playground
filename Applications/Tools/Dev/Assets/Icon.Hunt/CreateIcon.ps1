Add-Type -AssemblyName System.Drawing

$sizes = @(256, 48, 32, 16)
$bitmaps = @{}

foreach ($size in $sizes) {
    $bmp = New-Object System.Drawing.Bitmap($size, $size)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.Clear([System.Drawing.Color]::Transparent)

    $pad = [int]($size * 0.05)
    $cx = $size / 2.0
    $cy = $size / 2.0

    # 배경 원 (진한 남색)
    $bgBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        [System.Drawing.PointF]::new(0, 0),
        [System.Drawing.PointF]::new($size, $size),
        [System.Drawing.Color]::FromArgb(255, 15, 30, 60),
        [System.Drawing.Color]::FromArgb(255, 30, 60, 100)
    )
    $g.FillEllipse($bgBrush, $pad, $pad, $size - $pad * 2, $size - $pad * 2)
    $bgBrush.Dispose()

    # 돋보기 렌즈 원 (청록색 그라디언트)
    $lensR = [int]($size * 0.28)
    $lensX = [int]($cx - $size * 0.12 - $lensR)
    $lensY = [int]($cy - $size * 0.12 - $lensR)
    $lensBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        [System.Drawing.PointF]::new($lensX, $lensY),
        [System.Drawing.PointF]::new($lensX + $lensR * 2, $lensY + $lensR * 2),
        [System.Drawing.Color]::FromArgb(255, 80, 200, 220),
        [System.Drawing.Color]::FromArgb(255, 40, 120, 200)
    )
    $lensPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255, 100, 220, 240), [float]([Math]::Max(2, $size * 0.04)))
    $g.FillEllipse($lensBrush, $lensX, $lensY, $lensR * 2, $lensR * 2)
    $g.DrawEllipse($lensPen, $lensX, $lensY, $lensR * 2, $lensR * 2)
    $lensBrush.Dispose()
    $lensPen.Dispose()

    # 돋보기 손잡이
    $handlePen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255, 100, 220, 240), [float]([Math]::Max(3, $size * 0.055)))
    $handlePen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $handlePen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $hx1 = [float]($lensX + $lensR * 2 * 0.72)
    $hy1 = [float]($lensY + $lensR * 2 * 0.72)
    $hx2 = [float]($cx + $size * 0.26)
    $hy2 = [float]($cy + $size * 0.26)
    $g.DrawLine($handlePen, $hx1, $hy1, $hx2, $hy2)
    $handlePen.Dispose()

    # 렌즈 안에 작은 컬러 사각형 3개 (아이콘을 상징)
    if ($size -ge 32) {
        $innerPad = [int]($lensR * 0.3)
        $dotSize = [int]($lensR * 0.28)
        $colors = @(
            [System.Drawing.Color]::FromArgb(200, 255, 100, 100),
            [System.Drawing.Color]::FromArgb(200, 100, 255, 150),
            [System.Drawing.Color]::FromArgb(200, 100, 150, 255)
        )
        $positions = @(
            @{ x = $lensX + $innerPad; y = $lensY + $innerPad },
            @{ x = $lensX + $lensR - [int]($dotSize / 2); y = $lensY + $innerPad },
            @{ x = $lensX + $innerPad; y = $lensY + $lensR - [int]($dotSize * 0.3) }
        )
        for ($i = 0; $i -lt 3; $i++) {
            $db = New-Object System.Drawing.SolidBrush($colors[$i])
            $g.FillRectangle($db, $positions[$i].x, $positions[$i].y, $dotSize, $dotSize)
            $db.Dispose()
        }
    }

    $g.Dispose()
    $bitmaps[$size] = $bmp
}

# ICO 파일 생성
$ms = New-Object System.IO.MemoryStream
$writer = New-Object System.IO.BinaryWriter($ms)
$writer.Write([uint16]0)
$writer.Write([uint16]1)
$writer.Write([uint16]$sizes.Count)

$imageData = @()
foreach ($size in $sizes) {
    $imgMs = New-Object System.IO.MemoryStream
    $bitmaps[$size].Save($imgMs, [System.Drawing.Imaging.ImageFormat]::Png)
    $bytes = $imgMs.ToArray()
    $imageData += ,$bytes
    $imgMs.Dispose()
}

$dataOffset = 6 + ($sizes.Count * 16)
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $size = $sizes[$i]; $data = $imageData[$i]
    $wb = if ($size -eq 256) { 0 } else { $size }
    $writer.Write([byte]$wb); $writer.Write([byte]$wb)
    $writer.Write([byte]0); $writer.Write([byte]0)
    $writer.Write([uint16]1); $writer.Write([uint16]32)
    $writer.Write([uint32]$data.Length)
    $writer.Write([uint32]$dataOffset)
    $dataOffset += $data.Length
}
foreach ($data in $imageData) { $writer.Write($data) }

$writer.Flush()
[System.IO.File]::WriteAllBytes("Resources\app.ico", $ms.ToArray())
$ms.Dispose(); $writer.Dispose()
foreach ($bmp in $bitmaps.Values) { $bmp.Dispose() }
Write-Host "아이콘 생성 완료"
