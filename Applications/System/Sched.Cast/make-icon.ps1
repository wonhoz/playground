Add-Type -AssemblyName System.Drawing

$out = Join-Path $PSScriptRoot "Resources"
if (-not (Test-Path $out)) { New-Item -ItemType Directory -Path $out | Out-Null }

$sizes = @(16, 32, 48, 256)
$pngPaths = @()

foreach ($sz in $sizes) {
    $bmp = New-Object System.Drawing.Bitmap($sz, $sz)
    $g   = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias

    $bg = [System.Drawing.Color]::FromArgb(255, 18, 18, 30)
    $g.Clear($bg)

    $p = $sz / 256.0
    $cx = [int](128 * $p); $cy = [int](128 * $p)
    $r  = [int](96  * $p)

    # Clock face background
    $faceBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 26, 40, 60))
    $g.FillEllipse($faceBrush, $cx - $r, $cy - $r, $r*2, $r*2)

    # Clock rim
    $rimPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255, 20, 160, 180), [float](3*$p))
    $g.DrawEllipse($rimPen, $cx - $r, $cy - $r, $r*2, $r*2)

    # Hour marks (12 ticks)
    $tickPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(180, 60, 200, 220), [float](2*$p))
    for ($h = 0; $h -lt 12; $h++) {
        $angle = $h * 30 * [Math]::PI / 180
        $x1 = $cx + [int](($r - [int](8*$p)) * [Math]::Sin($angle))
        $y1 = $cy - [int](($r - [int](8*$p)) * [Math]::Cos($angle))
        $x2 = $cx + [int](($r - [int](2*$p)) * [Math]::Sin($angle))
        $y2 = $cy - [int](($r - [int](2*$p)) * [Math]::Cos($angle))
        if ($x1 -ne $x2 -or $y1 -ne $y2) {
            $g.DrawLine($tickPen, $x1, $y1, $x2, $y2)
        }
    }

    # Hour hand (pointing ~10 o'clock)
    $hourPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255, 200, 220, 240), [float](3*$p))
    $hAngle  = -60 * [Math]::PI / 180
    $hLen    = [int](52 * $p)
    $g.DrawLine($hourPen, $cx, $cy,
        $cx + [int]($hLen * [Math]::Sin($hAngle)),
        $cy - [int]($hLen * [Math]::Cos($hAngle)))

    # Minute hand (pointing ~2 o'clock)
    $minPen  = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255, 96, 210, 230), [float](2*$p))
    $mAngle  = 60 * [Math]::PI / 180
    $mLen    = [int](72 * $p)
    $g.DrawLine($minPen, $cx, $cy,
        $cx + [int]($mLen * [Math]::Sin($mAngle)),
        $cy - [int]($mLen * [Math]::Cos($mAngle)))

    # Center dot
    $dotBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 20, 200, 220))
    $dotR = [int](5 * $p)
    if ($dotR -lt 1) { $dotR = 1 }
    $g.FillEllipse($dotBrush, $cx - $dotR, $cy - $dotR, $dotR*2, $dotR*2)

    $g.Dispose()
    $pngPath = Join-Path $out "icon_$sz.png"
    $bmp.Save($pngPath, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    $pngPaths += $pngPath

    $faceBrush.Dispose(); $rimPen.Dispose(); $tickPen.Dispose()
    $hourPen.Dispose(); $minPen.Dispose(); $dotBrush.Dispose()
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
