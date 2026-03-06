# Serve.Cast 아이콘 생성 스크립트
# 디자인: 다크 배경 + 폴더 모양 + 번개(⚡) 파란 심볼

Add-Type -AssemblyName System.Drawing

$outDir = Join-Path $PSScriptRoot "Resources"
if (-not (Test-Path $outDir)) { New-Item -ItemType Directory -Path $outDir | Out-Null }
$outIco = Join-Path $outDir "app.ico"

function Make-Bitmap([int]$sz) {
    $bmp = New-Object System.Drawing.Bitmap($sz, $sz)
    $g   = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode   = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit

    # 배경 (다크 네이비)
    $bgColor = [System.Drawing.Color]::FromArgb(255, 0x0E, 0x15, 0x20)
    $g.Clear($bgColor)

    $pad  = [int]($sz * 0.08)
    $w    = $sz - $pad * 2
    $h    = $sz - $pad * 2

    # ── 폴더 모양 (하단 사각형) ─────────────────────────────────────
    $folderBg = [System.Drawing.Color]::FromArgb(255, 0x1E, 0x2A, 0x40)
    $accentBg = [System.Drawing.Color]::FromArgb(255, 0x89, 0xB4, 0xFA)

    # 폴더 탭 (상단 왼쪽 작은 사각형)
    $tabW = [int]($w * 0.45)
    $tabH = [int]($h * 0.14)
    $tabX = $pad
    $tabY = [int]($pad + $h * 0.22)
    $tabBrush = New-Object System.Drawing.SolidBrush($folderBg)
    $tabPath  = New-Object System.Drawing.Drawing2D.GraphicsPath
    $r3 = [int]($sz * 0.06)
    $tabPath.AddArc($tabX, $tabY, $r3 * 2, $r3 * 2, 180, 90)
    $tabPath.AddArc($tabX + $tabW - $r3 * 2, $tabY, $r3 * 2, $r3 * 2, 270, 90)
    $tabPath.AddLine($tabX + $tabW, $tabY + $tabH, $tabX, $tabY + $tabH)
    $tabPath.CloseFigure()
    $g.FillPath($tabBrush, $tabPath)

    # 폴더 본체 (큰 사각형)
    $bodyX = $pad
    $bodyY = [int]($pad + $h * 0.32)
    $bodyW = $w
    $bodyH = [int]($h * 0.58)
    $r4    = [int]($sz * 0.08)
    $bodyPath = New-Object System.Drawing.Drawing2D.GraphicsPath
    $bodyPath.AddArc($bodyX, $bodyY, $r4 * 2, $r4 * 2, 180, 90)
    $bodyPath.AddArc($bodyX + $bodyW - $r4 * 2, $bodyY, $r4 * 2, $r4 * 2, 270, 90)
    $bodyPath.AddArc($bodyX + $bodyW - $r4 * 2, $bodyY + $bodyH - $r4 * 2, $r4 * 2, $r4 * 2, 0, 90)
    $bodyPath.AddArc($bodyX, $bodyY + $bodyH - $r4 * 2, $r4 * 2, $r4 * 2, 90, 90)
    $bodyPath.CloseFigure()
    $g.FillPath($tabBrush, $bodyPath)

    # 폴더 테두리 (블루)
    $borderPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(120, 0x89, 0xB4, 0xFA), [float]([int]($sz * 0.04)))
    $g.DrawPath($borderPen, $bodyPath)

    # ── 번개 심볼 (⚡) ─────────────────────────────────────────────
    $fontSize = [float]([int]($sz * 0.34))
    $font     = New-Object System.Drawing.Font("Segoe UI Emoji", $fontSize, [System.Drawing.FontStyle]::Bold)
    $brush    = New-Object System.Drawing.SolidBrush($accentBg)
    $sf       = New-Object System.Drawing.StringFormat
    $sf.Alignment     = [System.Drawing.StringAlignment]::Center
    $sf.LineAlignment = [System.Drawing.StringAlignment]::Center

    $centerRect = New-Object System.Drawing.RectangleF(
        [float]$bodyX, [float]$bodyY, [float]$bodyW, [float]$bodyH)
    $g.DrawString([char]0x26A1, $font, $brush, $centerRect, $sf)

    $g.Dispose()
    return $bmp
}

# ICO 파일 생성
$sizes  = @(256, 48, 32, 16)
$bitmaps = $sizes | ForEach-Object { Make-Bitmap $_ }

$ms = New-Object System.IO.MemoryStream
$bw = New-Object System.IO.BinaryWriter($ms)

# ICONDIR
$bw.Write([uint16]0)             # Reserved
$bw.Write([uint16]1)             # Type = ICO
$bw.Write([uint16]$sizes.Count)  # Count

$headerSize = 6 + $sizes.Count * 16
$imageData  = @()
$offset     = $headerSize

foreach ($bmp in $bitmaps) {
    $img_ms = New-Object System.IO.MemoryStream
    $bmp.Save($img_ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $bytes = $img_ms.ToArray()
    $imageData += , $bytes
    $img_ms.Dispose()

    $dim = if ($bmp.Width -ge 256) { 0 } else { [byte]$bmp.Width }
    $bw.Write([byte]$dim)       # Width
    $bw.Write([byte]$dim)       # Height
    $bw.Write([byte]0)          # ColorCount
    $bw.Write([byte]0)          # Reserved
    $bw.Write([uint16]1)        # Planes
    $bw.Write([uint16]32)       # BitCount
    $bw.Write([uint32]$bytes.Length)
    $bw.Write([uint32]$offset)
    $offset += $bytes.Length
}

foreach ($bytes in $imageData) { $bw.Write($bytes) }

[System.IO.File]::WriteAllBytes($outIco, $ms.ToArray())
$bw.Dispose()
$ms.Dispose()
foreach ($bmp in $bitmaps) { $bmp.Dispose() }

Write-Host "아이콘 생성 완료: $outIco" -ForegroundColor Cyan
