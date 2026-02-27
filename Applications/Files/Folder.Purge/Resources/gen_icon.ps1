# Folder.Purge 아이콘 생성 스크립트
# 폴더 + 삭제(X) 조합 모티프, 주황-레드 계열
Add-Type -AssemblyName System.Drawing

function Make-Bitmap {
    param([int]$sz)

    $bmp = New-Object System.Drawing.Bitmap($sz, $sz)
    $g   = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode   = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAlias
    $g.Clear([System.Drawing.Color]::Transparent)

    # ── 배경 (둥근 사각형) ──
    $bgColor = [System.Drawing.Color]::FromArgb(255, 30, 30, 30)
    $bgBrush = New-Object System.Drawing.SolidBrush($bgColor)
    $pad = [int]($sz * 0.05)
    $g.FillRectangle($bgBrush, $pad, $pad, $sz - $pad*2, $sz - $pad*2)

    # ── 폴더 몸통 ──
    $fW   = [int]($sz * 0.72)
    $fH   = [int]($sz * 0.52)
    $fX   = [int]($sz * 0.14)
    $fY   = [int]($sz * 0.30)

    # 폴더 탭 (작은 상단 돌출)
    $tabW  = [int]($fW * 0.38)
    $tabH  = [int]($fH * 0.15)
    $tabBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 232, 100, 60))
    $g.FillRectangle($tabBrush, $fX, $fY - $tabH, $tabW, $tabH + 2)

    # 폴더 본체
    $folderBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 200, 80, 40))
    $g.FillRectangle($folderBrush, $fX, $fY, $fW, $fH)

    # 폴더 하이라이트 (상단 밝은 선)
    $hlBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(60, 255, 255, 255))
    $g.FillRectangle($hlBrush, $fX, $fY, $fW, [int]($fH * 0.12))

    # ── X 표시 (삭제 의미) ──
    $xPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255, 255, 255, 255), [float]([Math]::Max(2, $sz * 0.07)))
    $xPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $xPen.EndCap   = [System.Drawing.Drawing2D.LineCap]::Round

    $cx   = $fX + $fW * 0.5
    $cy   = $fY + $fH * 0.5
    $xR   = $fW * 0.22

    $g.DrawLine($xPen, [float]($cx - $xR), [float]($cy - $xR), [float]($cx + $xR), [float]($cy + $xR))
    $g.DrawLine($xPen, [float]($cx + $xR), [float]($cy - $xR), [float]($cx - $xR), [float]($cy + $xR))

    $g.Dispose()
    $bgBrush.Dispose()
    $tabBrush.Dispose()
    $folderBrush.Dispose()
    $hlBrush.Dispose()
    $xPen.Dispose()
    return $bmp
}

$sizes  = @(16, 32, 48, 256)
$bitmaps = $sizes | ForEach-Object { Make-Bitmap $_ }

# ICO 파일 직접 쓰기
$outPath = Join-Path $PSScriptRoot "app.ico"
$stream  = [System.IO.File]::OpenWrite($outPath)
$writer  = New-Object System.IO.BinaryWriter($stream)

$count = $bitmaps.Count

# ICO 헤더 (6 bytes)
$writer.Write([uint16]0)        # Reserved
$writer.Write([uint16]1)        # Type: ICO
$writer.Write([uint16]$count)   # Image count

# PNG 데이터 수집
$pngStreams = foreach ($bmp in $bitmaps) {
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $ms
}

# 디렉터리 엔트리 (16 bytes × count)
$offset = 6 + 16 * $count
for ($i = 0; $i -lt $count; $i++) {
    $sz   = $sizes[$i]
    $data = $pngStreams[$i].ToArray()
    $szVal = $sz

    if ($szVal -ge 256) {
        $writer.Write([byte]0)
    } else {
        $writer.Write([byte]$szVal)
    }
    if ($szVal -ge 256) {
        $writer.Write([byte]0)
    } else {
        $writer.Write([byte]$szVal)
    }   # Height (same for square)
    $writer.Write([byte]0)        # Color count (0 = >256)
    $writer.Write([byte]0)        # Reserved
    $writer.Write([uint16]1)      # Planes
    $writer.Write([uint16]32)     # Bit count
    $writer.Write([uint32]$data.Length)
    $writer.Write([uint32]$offset)
    $offset += $data.Length
}

# PNG 데이터 본문
foreach ($ms in $pngStreams) {
    $writer.Write($ms.ToArray())
    $ms.Dispose()
}

$writer.Close()
$stream.Close()
foreach ($b in $bitmaps) { $b.Dispose() }

Write-Host "app.ico 생성 완료: $outPath"
