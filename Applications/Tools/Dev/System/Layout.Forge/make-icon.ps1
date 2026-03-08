Add-Type -AssemblyName System.Drawing

$out = Join-Path $PSScriptRoot "Resources"
if (-not (Test-Path $out)) { New-Item -ItemType Directory -Path $out | Out-Null }

$sizes = @(16, 32, 48, 256)
$pngPaths = @()

foreach ($sz in $sizes) {
    $bmp = New-Object System.Drawing.Bitmap($sz, $sz)
    $g   = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode   = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit

    # Background
    $bg = [System.Drawing.Color]::FromArgb(255, 18, 18, 30)
    $g.Clear($bg)

    $p  = $sz / 256.0

    # Keyboard body
    $kx = [int](16 * $p); $ky = [int](70 * $p)
    $kw = [int](224 * $p); $kh = [int](130 * $p)
    $bodyBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 30, 30, 50))
    $bodyPen   = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255, 60, 60, 90), [float](2*$p))
    $g.FillRectangle($bodyBrush, $kx, $ky, $kw, $kh)
    $g.DrawRectangle($bodyPen, $kx, $ky, $kw, $kh)

    # Draw key rows (simplified dots/blocks)
    $keyColor   = [System.Drawing.Color]::FromArgb(255, 45, 45, 70)
    $accentColor = [System.Drawing.Color]::FromArgb(255, 37, 99, 235)
    $keyBrush   = New-Object System.Drawing.SolidBrush($keyColor)
    $accentBrush = New-Object System.Drawing.SolidBrush($accentColor)

    $rows = 3; $cols = 10
    $kbPad = [int](12 * $p)
    $keyW  = [int](($kw - $kbPad*2 - ($cols-1)*[int](3*$p)) / $cols)
    $keyH  = [int](($kh - $kbPad*2 - ($rows-1)*[int](4*$p)) / $rows)
    if ($keyW -lt 1) { $keyW = 1 }
    if ($keyH -lt 1) { $keyH = 1 }

    for ($r = 0; $r -lt $rows; $r++) {
        for ($c = 0; $c -lt $cols; $c++) {
            $x = $kx + $kbPad + $c * ($keyW + [int](3*$p))
            $y = $ky + $kbPad + $r * ($keyH + [int](4*$p))
            # Highlight Caps Lock position (row1, col0)
            if ($r -eq 1 -and $c -eq 0) {
                $g.FillRectangle($accentBrush, $x, $y, [int]($keyW * 1.5), $keyH)
            } else {
                $g.FillRectangle($keyBrush, $x, $y, $keyW, $keyH)
            }
        }
    }

    # Arrow indicator
    $arrPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255, 96, 165, 250), [float](2*$p))
    $ax = [int](128 * $p); $ay = [int](128 * $p)
    $al = [int](20 * $p)
    if ($al -gt 1) {
        $g.DrawLine($arrPen, $ax, $ay, $ax + $al, $ay)
        $g.DrawLine($arrPen, $ax + $al - [int](5*$p), $ay - [int](5*$p), $ax + $al, $ay)
        $g.DrawLine($arrPen, $ax + $al - [int](5*$p), $ay + [int](5*$p), $ax + $al, $ay)
    }

    $g.Dispose()
    $pngPath = Join-Path $out "icon_$sz.png"
    $bmp.Save($pngPath, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    $pngPaths += $pngPath

    $bodyBrush.Dispose(); $bodyPen.Dispose()
    $keyBrush.Dispose(); $accentBrush.Dispose(); $arrPen.Dispose()
}

# Build ICO
$icoPath = Join-Path $out "app.ico"
$fs = [System.IO.File]::Create($icoPath)
$bw = New-Object System.IO.BinaryWriter($fs)

$pngData = @()
foreach ($pp in $pngPaths) { $pngData += ,[System.IO.File]::ReadAllBytes($pp) }

$bw.Write([uint16]0); $bw.Write([uint16]1); $bw.Write([uint16]$pngData.Count)

$offset = 6 + 16 * $pngData.Count
for ($i = 0; $i -lt $pngData.Count; $i++) {
    $szVal = $sizes[$i]
    if ($szVal -ge 256) { $wByte = 0 } else { $wByte = $szVal }
    if ($szVal -ge 256) { $hByte = 0 } else { $hByte = $szVal }
    $bw.Write([byte]$wByte); $bw.Write([byte]$hByte)
    $bw.Write([byte]0); $bw.Write([byte]0)
    $bw.Write([uint16]1); $bw.Write([uint16]32)
    $bw.Write([uint32]$pngData[$i].Length)
    $bw.Write([uint32]$offset)
    $offset += $pngData[$i].Length
}
foreach ($d in $pngData) { $bw.Write($d) }
$bw.Close(); $fs.Close()

foreach ($pp in $pngPaths) { Remove-Item $pp }
Write-Host "app.ico 생성 완료: $icoPath"
