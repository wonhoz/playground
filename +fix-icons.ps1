Add-Type -AssemblyName System.Drawing

$root = "C:\Users\admin\source\repos\+Playground"

function Save-Ico([System.Drawing.Bitmap]$src, [string]$outPath) {
    $sizes = @(256, 48, 32, 16)
    $pngs  = @()
    foreach ($sz in $sizes) {
        $bmp = New-Object System.Drawing.Bitmap($sz, $sz, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $g2  = [System.Drawing.Graphics]::FromImage($bmp)
        $g2.InterpolationMode  = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $g2.SmoothingMode      = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $g2.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
        $g2.DrawImage($src, 0, 0, $sz, $sz)
        $g2.Dispose()
        $ms = New-Object System.IO.MemoryStream
        $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
        $pngs += @{ Data = $ms.ToArray(); Size = $sz }
        $ms.Dispose()
        $bmp.Dispose()
    }

    $ms2 = New-Object System.IO.MemoryStream
    $bw  = New-Object System.IO.BinaryWriter($ms2)
    $bw.Write([uint16]0); $bw.Write([uint16]1); $bw.Write([uint16]$sizes.Count)

    $dataOffset = 6 + 16 * $sizes.Count
    for ($i = 0; $i -lt $sizes.Count; $i++) {
        $sz   = $sizes[$i]
        $data = $pngs[$i].Data
        $szB  = if ($sz -eq 256) { [byte]0 } else { [byte]$sz }
        $bw.Write($szB); $bw.Write($szB)
        $bw.Write([byte]0); $bw.Write([byte]0)
        $bw.Write([uint16]1); $bw.Write([uint16]32)
        $bw.Write([uint32]$data.Length)
        $bw.Write([uint32]$dataOffset)
        $dataOffset += $data.Length
    }
    foreach ($p in $pngs) { $bw.Write($p.Data) }
    $bw.Flush()

    $dir = [System.IO.Path]::GetDirectoryName($outPath)
    if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
    [System.IO.File]::WriteAllBytes($outPath, $ms2.ToArray())
    $bw.Dispose()
    $ms2.Dispose()
}

function Make-Icon([string]$path, [scriptblock]$draw) {
    $bmp = New-Object System.Drawing.Bitmap(256, 256, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g   = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode      = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
    $g.TextRenderingHint  = [System.Drawing.Text.TextRenderingHint]::AntiAlias
    $g.Clear([System.Drawing.Color]::Transparent)

    & $draw $g

    $g.Dispose()
    $outPath = Join-Path $root $path
    Save-Ico $bmp $outPath
    $bmp.Dispose()
    Write-Host "Created: $path"
}

function HexC([string]$h, [int]$a = 255) {
    $r = [Convert]::ToInt32($h.Substring(1,2),16)
    $gv = [Convert]::ToInt32($h.Substring(3,2),16)
    $b  = [Convert]::ToInt32($h.Substring(5,2),16)
    return [System.Drawing.Color]::FromArgb($a, $r, $gv, $b)
}

# ─────────────────────────────────────────────────────────────────────────────
# AI.Clip — Brain icon (purple scheme)
# ─────────────────────────────────────────────────────────────────────────────
Make-Icon "Applications\AI\AI.Clip\Resources\app.ico" {
    param($g)

    $purple1 = HexC "#8B5CF6"
    $purple2 = HexC "#7C3AED"
    $purple3 = HexC "#A78BFA"
    $purple4 = HexC "#6D28D9"
    $cyan    = HexC "#06B6D4"
    $white   = HexC "#FFFFFF"
    $gray    = HexC "#C4B5FD"

    # Brain silhouette - two lobes
    # Left lobe — 10 points = 3*3+1 (3 Bezier segments)
    $leftLobePath = New-Object System.Drawing.Drawing2D.GraphicsPath
    $leftPts = [System.Drawing.PointF[]]@(
        [System.Drawing.PointF]::new(128, 208),  # P0: bottom center
        [System.Drawing.PointF]::new(90,  205),  # ctrl
        [System.Drawing.PointF]::new(40,  185),  # ctrl
        [System.Drawing.PointF]::new(32,  140),  # P1: left-mid
        [System.Drawing.PointF]::new(26,  105),  # ctrl
        [System.Drawing.PointF]::new(44,   66),  # ctrl
        [System.Drawing.PointF]::new(80,   58),  # P2: top-left
        [System.Drawing.PointF]::new(104,  54),  # ctrl
        [System.Drawing.PointF]::new(120,  66),  # ctrl
        [System.Drawing.PointF]::new(128,  80)   # P3: center top
    )
    $leftLobePath.AddBeziers($leftPts)
    $leftLobePath.CloseFigure()

    # Right lobe — 10 points = 3*3+1 (3 Bezier segments)
    $rightLobePath = New-Object System.Drawing.Drawing2D.GraphicsPath
    $rightPts = [System.Drawing.PointF[]]@(
        [System.Drawing.PointF]::new(128,  80),  # P0: center top
        [System.Drawing.PointF]::new(136,  66),  # ctrl
        [System.Drawing.PointF]::new(152,  54),  # ctrl
        [System.Drawing.PointF]::new(176,  58),  # P1: top-right
        [System.Drawing.PointF]::new(212,  66),  # ctrl
        [System.Drawing.PointF]::new(230, 105),  # ctrl
        [System.Drawing.PointF]::new(224, 140),  # P2: right-mid
        [System.Drawing.PointF]::new(216, 185),  # ctrl
        [System.Drawing.PointF]::new(166, 205),  # ctrl
        [System.Drawing.PointF]::new(128, 208)   # P3: bottom center
    )
    $rightLobePath.AddBeziers($rightPts)
    $rightLobePath.CloseFigure()

    # Fill lobes with gradient
    $rect = [System.Drawing.RectangleF]::new(28, 54, 200, 156)
    $grad = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        [System.Drawing.PointF]::new(128, 54),
        [System.Drawing.PointF]::new(128, 210),
        $purple2, $purple4)

    $fillBrush = New-Object System.Drawing.SolidBrush($purple2)
    $g.FillPath($fillBrush, $leftLobePath)
    $g.FillPath($fillBrush, $rightLobePath)
    $fillBrush.Dispose()
    $grad.Dispose()

    # Outline
    $pen = New-Object System.Drawing.Pen($purple3, 6)
    $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
    $g.DrawPath($pen, $leftLobePath)
    $g.DrawPath($pen, $rightLobePath)
    $pen.Dispose()

    # Center divider line (corpus callosum hint)
    $divPen = New-Object System.Drawing.Pen((HexC "#4C1D95"), 4)
    $g.DrawLine($divPen, 128, 80, 128, 205)
    $divPen.Dispose()

    # Sulci (fold lines) - left lobe
    $sulciPen = New-Object System.Drawing.Pen((HexC "#4C1D95"), 3)
    $sulciPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $sulciPen.EndCap   = [System.Drawing.Drawing2D.LineCap]::Round
    # Left sulci
    $g.DrawCurve($sulciPen, [System.Drawing.PointF[]]@(
        [System.Drawing.PointF]::new(55,  100),
        [System.Drawing.PointF]::new(72,  115),
        [System.Drawing.PointF]::new(65,  135)
    ))
    $g.DrawCurve($sulciPen, [System.Drawing.PointF[]]@(
        [System.Drawing.PointF]::new(42,  140),
        [System.Drawing.PointF]::new(68,  155),
        [System.Drawing.PointF]::new(85,  170)
    ))
    $g.DrawCurve($sulciPen, [System.Drawing.PointF[]]@(
        [System.Drawing.PointF]::new(80,   80),
        [System.Drawing.PointF]::new(95,   95),
        [System.Drawing.PointF]::new(88,  115)
    ))
    # Right sulci
    $g.DrawCurve($sulciPen, [System.Drawing.PointF[]]@(
        [System.Drawing.PointF]::new(201, 100),
        [System.Drawing.PointF]::new(184, 115),
        [System.Drawing.PointF]::new(191, 135)
    ))
    $g.DrawCurve($sulciPen, [System.Drawing.PointF[]]@(
        [System.Drawing.PointF]::new(214, 140),
        [System.Drawing.PointF]::new(188, 155),
        [System.Drawing.PointF]::new(171, 170)
    ))
    $g.DrawCurve($sulciPen, [System.Drawing.PointF[]]@(
        [System.Drawing.PointF]::new(176,  80),
        [System.Drawing.PointF]::new(161,  95),
        [System.Drawing.PointF]::new(168, 115)
    ))
    $sulciPen.Dispose()

    # Brain stem
    $stemBrush = New-Object System.Drawing.SolidBrush($purple4)
    $stemPen   = New-Object System.Drawing.Pen($purple3, 4)
    $stemRect  = [System.Drawing.RectangleF]::new(108, 208, 40, 28)
    $stemPath  = New-Object System.Drawing.Drawing2D.GraphicsPath
    $stemPath.AddArc($stemRect.X, $stemRect.Y, 20, 20, 180, 90)
    $stemPath.AddArc($stemRect.X + $stemRect.Width - 20, $stemRect.Y, 20, 20, 270, 90)
    $stemPath.AddArc($stemRect.X + $stemRect.Width - 20, $stemRect.Y + $stemRect.Height - 20, 20, 20, 0, 90)
    $stemPath.AddArc($stemRect.X, $stemRect.Y + $stemRect.Height - 20, 20, 20, 90, 90)
    $stemPath.CloseFigure()
    $g.FillPath($stemBrush, $stemPath)
    $g.DrawPath($stemPen, $stemPath)
    $stemBrush.Dispose()
    $stemPen.Dispose()
    $stemPath.Dispose()

    # AI circuit nodes - small glowing dots
    $nodeBrush = New-Object System.Drawing.SolidBrush($cyan)
    $nodePositions = @(
        @(52, 118), @(75, 160), @(100, 88),
        @(204, 118), @(181, 160), @(156, 88)
    )
    foreach ($pos in $nodePositions) {
        $nx = $pos[0] - 5
        $ny = $pos[1] - 5
        $g.FillEllipse($nodeBrush, $nx, $ny, 10, 10)
    }
    $nodeBrush.Dispose()

    # Node outlines
    $nodePen = New-Object System.Drawing.Pen($white, 1.5)
    foreach ($pos in $nodePositions) {
        $nx = $pos[0] - 5
        $ny = $pos[1] - 5
        $g.DrawEllipse($nodePen, $nx, $ny, 10, 10)
    }
    $nodePen.Dispose()

    $leftLobePath.Dispose()
    $rightLobePath.Dispose()
}

# ─────────────────────────────────────────────────────────────────────────────
# PDF.Forge — centered document + flames
# ─────────────────────────────────────────────────────────────────────────────
Make-Icon "Applications\Files\Inspector\PDF.Forge\Resources\app.ico" {
    param($g)

    # Document: centered on 256px canvas
    # width=148, so x=(256-148)/2=54, height=180, y=(256-180)/2-10=28
    $docX = 54; $docY = 28; $docW = 148; $docH = 180
    $cornerR = 10

    # Document body
    $docPath = New-Object System.Drawing.Drawing2D.GraphicsPath
    $docPath.AddArc($docX, $docY, $cornerR*2, $cornerR*2, 180, 90)
    $docPath.AddArc($docX + $docW - $cornerR*2, $docY, $cornerR*2, $cornerR*2, 270, 90)
    $docPath.AddLine($docX + $docW, $docY + $cornerR, $docX + $docW, $docY + $docH - $cornerR)
    $docPath.AddArc($docX + $docW - $cornerR*2, $docY + $docH - $cornerR*2, $cornerR*2, $cornerR*2, 0, 90)
    $docPath.AddArc($docX, $docY + $docH - $cornerR*2, $cornerR*2, $cornerR*2, 90, 90)
    $docPath.CloseFigure()

    $grad = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        [System.Drawing.PointF]::new(128, $docY),
        [System.Drawing.PointF]::new(128, $docY + $docH),
        (HexC "#1E1E2E"), (HexC "#0D0D1A"))
    $g.FillPath($grad, $docPath)
    $grad.Dispose()

    $docPen = New-Object System.Drawing.Pen((HexC "#EF4444"), 5)
    $g.DrawPath($docPen, $docPath)
    $docPen.Dispose()
    $docPath.Dispose()

    # "PDF" text centered at (128, 118)
    $font = New-Object System.Drawing.Font("Arial", 44, [System.Drawing.FontStyle]::Bold)
    $brush = New-Object System.Drawing.SolidBrush((HexC "#EF4444"))
    $sf = New-Object System.Drawing.StringFormat
    $sf.Alignment     = [System.Drawing.StringAlignment]::Center
    $sf.LineAlignment = [System.Drawing.StringAlignment]::Center
    $g.DrawString("PDF", $font, $brush, 128, 118, $sf)
    $font.Dispose()
    $brush.Dispose()
    $sf.Dispose()

    # Horizontal lines (page lines) centered
    $linePen = New-Object System.Drawing.Pen((HexC "#4B5563"), 3)
    $lineXS = $docX + 20; $lineXE = $docX + $docW - 20
    $g.DrawLine($linePen, $lineXS, 155, $lineXE, 155)
    $g.DrawLine($linePen, $lineXS, 172, $lineXE, 172)
    $linePen.Dispose()

    # Flames at bottom center: base at (128, docY+docH) = (128, 208)
    # Flame 1 - main (center)
    $fl1 = New-Object System.Drawing.Drawing2D.GraphicsPath
    $fl1Pts = [System.Drawing.PointF[]]@(
        [System.Drawing.PointF]::new(128, 208),
        [System.Drawing.PointF]::new(108, 195),
        [System.Drawing.PointF]::new(112, 170),
        [System.Drawing.PointF]::new(128, 185),
        [System.Drawing.PointF]::new(144, 170),
        [System.Drawing.PointF]::new(148, 195),
        [System.Drawing.PointF]::new(128, 208)
    )
    $fl1.AddBeziers($fl1Pts)
    $fl1Brush = New-Object System.Drawing.SolidBrush((HexC "#F97316"))
    $g.FillPath($fl1Brush, $fl1)
    $fl1Brush.Dispose()
    $fl1.Dispose()

    # Flame 2 - left smaller
    $fl2 = New-Object System.Drawing.Drawing2D.GraphicsPath
    $fl2Pts = [System.Drawing.PointF[]]@(
        [System.Drawing.PointF]::new(110, 208),
        [System.Drawing.PointF]::new(94,  200),
        [System.Drawing.PointF]::new(96,  180),
        [System.Drawing.PointF]::new(108, 190),
        [System.Drawing.PointF]::new(118, 180),
        [System.Drawing.PointF]::new(120, 198),
        [System.Drawing.PointF]::new(110, 208)
    )
    $fl2.AddBeziers($fl2Pts)
    $fl2Brush = New-Object System.Drawing.SolidBrush((HexC "#EF4444"))
    $g.FillPath($fl2Brush, $fl2)
    $fl2Brush.Dispose()
    $fl2.Dispose()

    # Flame 3 - right smaller
    $fl3 = New-Object System.Drawing.Drawing2D.GraphicsPath
    $fl3Pts = [System.Drawing.PointF[]]@(
        [System.Drawing.PointF]::new(146, 208),
        [System.Drawing.PointF]::new(136, 198),
        [System.Drawing.PointF]::new(138, 180),
        [System.Drawing.PointF]::new(148, 190),
        [System.Drawing.PointF]::new(160, 180),
        [System.Drawing.PointF]::new(162, 200),
        [System.Drawing.PointF]::new(146, 208)
    )
    $fl3.AddBeziers($fl3Pts)
    $fl3Brush = New-Object System.Drawing.SolidBrush((HexC "#EF4444"))
    $g.FillPath($fl3Brush, $fl3)
    $fl3Brush.Dispose()
    $fl3.Dispose()

    # Inner flame glow
    $fl4 = New-Object System.Drawing.Drawing2D.GraphicsPath
    $fl4Pts = [System.Drawing.PointF[]]@(
        [System.Drawing.PointF]::new(128, 206),
        [System.Drawing.PointF]::new(118, 198),
        [System.Drawing.PointF]::new(120, 182),
        [System.Drawing.PointF]::new(128, 192),
        [System.Drawing.PointF]::new(136, 182),
        [System.Drawing.PointF]::new(138, 198),
        [System.Drawing.PointF]::new(128, 206)
    )
    $fl4.AddBeziers($fl4Pts)
    $fl4Brush = New-Object System.Drawing.SolidBrush((HexC "#FCD34D"))
    $g.FillPath($fl4Brush, $fl4)
    $fl4Brush.Dispose()
    $fl4.Dispose()
}

Write-Host "All icons generated."
