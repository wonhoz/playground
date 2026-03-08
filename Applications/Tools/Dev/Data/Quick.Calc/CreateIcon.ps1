# Quick.Calc 아이콘 생성 (16/32/48/256px)
# 어두운 배경 + 0/1 비트 패턴 + 계산기 테마

Add-Type -AssemblyName System.Drawing

function Create-Bitmap {
    param([int]$size)

    $bmp = New-Object System.Drawing.Bitmap($size, $size)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAlias

    # 배경
    $bgColor = [System.Drawing.Color]::FromArgb(255, 18, 18, 32)
    $accentColor = [System.Drawing.Color]::FromArgb(255, 50, 140, 255)
    $accentDim  = [System.Drawing.Color]::FromArgb(180, 30, 100, 200)
    $greenColor = [System.Drawing.Color]::FromArgb(255, 80, 220, 120)

    # 둥근 배경 채우기
    $radius = [int]($size * 0.18)
    $bgBrush = New-Object System.Drawing.SolidBrush($bgColor)
    $borderPen = New-Object System.Drawing.Pen($accentColor, [float]([Math]::Max(1, $size * 0.03)))

    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddArc(0, 0, $radius * 2, $radius * 2, 180, 90)
    $path.AddArc($size - $radius * 2, 0, $radius * 2, $radius * 2, 270, 90)
    $path.AddArc($size - $radius * 2, $size - $radius * 2, $radius * 2, $radius * 2, 0, 90)
    $path.AddArc(0, $size - $radius * 2, $radius * 2, $radius * 2, 90, 90)
    $path.CloseFigure()

    $g.FillPath($bgBrush, $path)
    $g.DrawPath($borderPen, $path)

    if ($size -ge 32) {
        # 비트 도트 패턴 (상단)
        $dotSize = [Math]::Max(2, [int]($size * 0.08))
        $dotGap = [int]($size * 0.13)
        $startX = [int]($size * 0.14)
        $startY = [int]($size * 0.14)
        # 패턴: 1010 / 0110 (각각 2줄, 4열)
        $pattern = @(1,0,1,0, 0,1,1,0)
        for ($i = 0; $i -lt 8; $i++) {
            $col = $i % 4
            $row = [int]($i / 4)
            $x = $startX + $col * $dotGap
            $y = $startY + $row * $dotGap
            if ($pattern[$i] -eq 1) {
                $dotBrush = New-Object System.Drawing.SolidBrush($accentColor)
            } else {
                $dotBrush = New-Object System.Drawing.SolidBrush($accentDim)
            }
            $g.FillEllipse($dotBrush, $x, $y, $dotSize, $dotSize)
            $dotBrush.Dispose()
        }

        # 메인 텍스트 "0x"
        $fontSize = [float]($size * 0.28)
        $font = New-Object System.Drawing.Font("Consolas", $fontSize, [System.Drawing.FontStyle]::Bold)
        $textBrush = New-Object System.Drawing.SolidBrush($greenColor)
        $sf = New-Object System.Drawing.StringFormat
        $sf.Alignment = [System.Drawing.StringAlignment]::Center
        $sf.LineAlignment = [System.Drawing.StringAlignment]::Center
        $rect = New-Object System.Drawing.RectangleF(0, [float]($size * 0.42), [float]$size, [float]($size * 0.32))
        $g.DrawString("0x", $font, $textBrush, $rect, $sf)
        $font.Dispose()
        $textBrush.Dispose()

        # 하단 소문자 "calc" 표시
        $fontSize2 = [float]($size * 0.14)
        $font2 = New-Object System.Drawing.Font("Consolas", $fontSize2, [System.Drawing.FontStyle]::Regular)
        $textBrush2 = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(180, 160, 180, 220))
        $rect2 = New-Object System.Drawing.RectangleF(0, [float]($size * 0.74), [float]$size, [float]($size * 0.2))
        $g.DrawString("calc", $font2, $textBrush2, $rect2, $sf)
        $font2.Dispose()
        $textBrush2.Dispose()
        $sf.Dispose()
    } else {
        # 16px: 간단한 "0x" 텍스트
        $fontSize = [float]($size * 0.42)
        $font = New-Object System.Drawing.Font("Consolas", $fontSize, [System.Drawing.FontStyle]::Bold)
        $textBrush = New-Object System.Drawing.SolidBrush($greenColor)
        $sf = New-Object System.Drawing.StringFormat
        $sf.Alignment = [System.Drawing.StringAlignment]::Center
        $sf.LineAlignment = [System.Drawing.StringAlignment]::Center
        $rect = New-Object System.Drawing.RectangleF(0, 0, [float]$size, [float]$size)
        $g.DrawString("0x", $font, $textBrush, $rect, $sf)
        $font.Dispose()
        $textBrush.Dispose()
        $sf.Dispose()
    }

    $g.Dispose()
    $bgBrush.Dispose()
    $borderPen.Dispose()
    return $bmp
}

function Write-Ico {
    param([string]$outPath, [int[]]$sizes)

    $bitmaps = @{}
    foreach ($sz in $sizes) {
        $bitmaps[$sz] = Create-Bitmap -size $sz
    }

    $ms = New-Object System.IO.MemoryStream

    # ICO 헤더
    $writer = New-Object System.IO.BinaryWriter($ms)
    $writer.Write([uint16]0)         # Reserved
    $writer.Write([uint16]1)         # Type: ICO
    $writer.Write([uint16]$sizes.Count) # Count

    # 각 이미지의 PNG 바이트 수집
    $pngList = @()
    foreach ($sz in $sizes) {
        $pngMs = New-Object System.IO.MemoryStream
        $bitmaps[$sz].Save($pngMs, [System.Drawing.Imaging.ImageFormat]::Png)
        $pngList += ,$pngMs.ToArray()
        $pngMs.Dispose()
    }

    # 디렉터리 엔트리 계산
    $offset = 6 + 16 * $sizes.Count
    for ($i = 0; $i -lt $sizes.Count; $i++) {
        $sz = $sizes[$i]
        $szVal = if ($sz -ge 256) { 0 } else { $sz }
        $writer.Write([byte]$szVal)      # Width
        $writer.Write([byte]$szVal)      # Height
        $writer.Write([byte]0)           # ColorCount
        $writer.Write([byte]0)           # Reserved
        $writer.Write([uint16]1)         # Planes
        $writer.Write([uint16]32)        # BitCount
        $writer.Write([uint32]$pngList[$i].Length) # BytesInRes
        $writer.Write([uint32]$offset)   # ImageOffset
        $offset += $pngList[$i].Length
    }

    foreach ($png in $pngList) {
        $writer.Write($png)
    }

    $writer.Flush()
    [System.IO.File]::WriteAllBytes($outPath, $ms.ToArray())
    $ms.Dispose()

    foreach ($bmp in $bitmaps.Values) { $bmp.Dispose() }
    Write-Host "ICO 생성 완료: $outPath"
}

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$outFile = Join-Path $scriptDir "Resources\app.ico"
Write-Ico -outPath $outFile -sizes @(16, 32, 48, 256)
