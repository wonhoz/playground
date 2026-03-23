# Table.Craft 아이콘 생성 스크립트
# 표(Table) + 렌즈/돋보기 형태의 CSV 분석 도구 아이콘

Add-Type -AssemblyName System.Drawing

$outDir = "$PSScriptRoot\Resources"
if (!(Test-Path $outDir)) { New-Item -ItemType Directory -Path $outDir | Out-Null }

function Make-Bitmap([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap($size, $size)
    $g   = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode   = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit

    # 배경 (다크 라운드 사각형)
    $bg = [System.Drawing.Color]::FromArgb(0xFF, 0x1E, 0x1E, 0x2E)
    $g.Clear($bg)

    $s = $size

    # 표 배경 패널
    $panelBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(0xFF, 0x31, 0x32, 0x44))
    $margin = [int]($s * 0.10)
    $panelRect = New-Object System.Drawing.Rectangle($margin, $margin, $s - $margin*2, $s - $margin*2)
    $g.FillRectangle($panelBrush, $panelRect)

    # 헤더 행 (초록 강조)
    $headerBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(0xFF, 0xA6, 0xE3, 0xA1))
    $hdrH = [int]($s * 0.22)
    $hdrRect = New-Object System.Drawing.Rectangle($margin, $margin, $s - $margin*2, $hdrH)
    $g.FillRectangle($headerBrush, $hdrRect)

    # 데이터 행들 (밝은 선)
    $rowBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(0x88, 0xCD, 0xD6, 0xF4))
    $rowH     = [int]($s * 0.12)
    $rowY     = $margin + $hdrH + [int]($s * 0.04)
    $rowW     = [int](($s - $margin*2) * 0.62)
    for ($r = 0; $r -lt 3; $r++) {
        $rRect = New-Object System.Drawing.Rectangle($margin + [int]($s*0.04), $rowY + $r * ($rowH + [int]($s*0.03)), $rowW, $rowH)
        $g.FillRectangle($rowBrush, $rRect)
    }

    # 돋보기 원 (오른쪽 하단)
    $cx = [int]($s * 0.72)
    $cy = [int]($s * 0.68)
    $r2 = [int]($s * 0.22)
    $lensOuterBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(0xFF, 0x1E, 0x1E, 0x2E))
    $lensPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(0xFF, 0xA6, 0xE3, 0xA1), [int]($s * 0.06))
    $g.FillEllipse($lensOuterBrush, $cx - $r2, $cy - $r2, $r2*2, $r2*2)
    $g.DrawEllipse($lensPen, $cx - $r2, $cy - $r2, $r2*2, $r2*2)

    # 돋보기 손잡이
    $handleW = [int]($s * 0.07)
    $handlePen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(0xFF, 0xA6, 0xE3, 0xA1), $handleW)
    $handlePen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $handlePen.EndCap   = [System.Drawing.Drawing2D.LineCap]::Round
    $hx1 = $cx + [int]($r2 * 0.72)
    $hy1 = $cy + [int]($r2 * 0.72)
    $hx2 = $cx + [int]($r2 * 1.4)
    $hy2 = $cy + [int]($r2 * 1.4)
    $g.DrawLine($handlePen, $hx1, $hy1, $hx2, $hy2)

    $g.Dispose()
    return $bmp
}

# ICO 파일 생성 (16, 32, 48, 256)
$sizes = @(16, 32, 48, 256)
$bitmaps = $sizes | ForEach-Object { Make-Bitmap $_ }

$icoPath = "$outDir\app.ico"
$stream  = [System.IO.File]::OpenWrite($icoPath)
$writer  = New-Object System.IO.BinaryWriter($stream)

# ICO 헤더
$writer.Write([uint16]0)       # Reserved
$writer.Write([uint16]1)       # Type: 1=ICO
$writer.Write([uint16]$sizes.Count)

# 각 이미지를 PNG로 인코딩
$pngStreams = @()
foreach ($bmp in $bitmaps) {
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $pngStreams += $ms
}

# 디렉토리 엔트리
$offset = 6 + $sizes.Count * 16
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $sz = $sizes[$i]
    $w  = if ($sz -ge 256) { 0 } else { $sz }
    $h  = if ($sz -ge 256) { 0 } else { $sz }
    $writer.Write([byte]$w)
    $writer.Write([byte]$h)
    $writer.Write([byte]0)       # ColorCount
    $writer.Write([byte]0)       # Reserved
    $writer.Write([uint16]1)     # Planes
    $writer.Write([uint16]32)    # BitCount
    $writer.Write([uint32]$pngStreams[$i].Length)
    $writer.Write([uint32]$offset)
    $offset += $pngStreams[$i].Length
}

# 이미지 데이터
foreach ($ms in $pngStreams) {
    $writer.Write($ms.ToArray())
    $ms.Dispose()
}

$writer.Close()
$stream.Close()
foreach ($bmp in $bitmaps) { $bmp.Dispose() }

Write-Host "아이콘 생성 완료: $icoPath" -ForegroundColor Green
