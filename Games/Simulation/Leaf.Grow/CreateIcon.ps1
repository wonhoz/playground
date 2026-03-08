Add-Type -AssemblyName System.Drawing

$sizes = @(16, 32, 48, 256)
$dir   = Split-Path -Parent $MyInvocation.MyCommand.Path
$out   = Join-Path $dir "Resources\app.ico"
New-Item -ItemType Directory -Force -Path (Join-Path $dir "Resources") | Out-Null

$streams = @()
foreach ($sz in $sizes) {
    $bmp = New-Object System.Drawing.Bitmap($sz, $sz)
    $g   = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.Clear([System.Drawing.Color]::Transparent)

    # 배경 원 (다크 그린)
    $bgBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 10, 20, 10))
    $g.FillEllipse($bgBrush, 1, 1, $sz-2, $sz-2)

    if ($sz -ge 32) {
        # 줄기
        $stemPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255, 80, 120, 40), [int]([Math]::Max(2, $sz * 0.07)))
        $stemPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
        $stemPen.EndCap   = [System.Drawing.Drawing2D.LineCap]::Round
        $cx = $sz / 2
        $g.DrawLine($stemPen, $cx, [int]($sz * 0.85), $cx, [int]($sz * 0.4))

        # 잎 (타원 2개)
        $leafBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 64, 200, 90))
        $leafW = [int]($sz * 0.35); $leafH = [int]($sz * 0.22)

        # 좌측 잎
        $m1 = New-Object System.Drawing.Drawing2D.Matrix
        $m1.RotateAt(-40, [System.Drawing.PointF]::new($cx, [int]($sz * 0.55)))
        $g.Transform = $m1
        $g.FillEllipse($leafBrush, $cx - $leafW, [int]($sz * 0.55) - $leafH/2, $leafW, $leafH)
        $g.ResetTransform()

        # 우측 잎
        $m2 = New-Object System.Drawing.Drawing2D.Matrix
        $m2.RotateAt(40, [System.Drawing.PointF]::new($cx, [int]($sz * 0.45)))
        $g.Transform = $m2
        $g.FillEllipse($leafBrush, $cx, [int]($sz * 0.45) - $leafH/2, $leafW, $leafH)
        $g.ResetTransform()

        # 상단 잎
        $topLeafBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 80, 220, 100))
        $g.FillEllipse($topLeafBrush, [int]($cx - $sz*0.18), [int]($sz*0.2), [int]($sz*0.36), [int]($sz*0.24))
    } else {
        # 작은 사이즈: 단순 잎 점
        $leafBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 64, 200, 90))
        $r = [int]($sz * 0.28)
        $g.FillEllipse($leafBrush, $sz/2 - $r, $sz/2 - $r, $r*2, $r*2)
    }

    $g.Dispose()

    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $streams += @{ Size = $sz; Data = $ms.ToArray() }
    $ms.Dispose()
    $bmp.Dispose()
}

$ico = New-Object System.IO.MemoryStream
$bw  = New-Object System.IO.BinaryWriter($ico)

$bw.Write([uint16]0)
$bw.Write([uint16]1)
$bw.Write([uint16]$streams.Count)

$headerSize = 6 + 16 * $streams.Count
$offset = $headerSize

foreach ($s in $streams) {
    $szVal = $s.Size
    $wb = if ($szVal -ge 256) { 0 } else { $szVal }
    $hb = if ($szVal -ge 256) { 0 } else { $szVal }
    $bw.Write([byte]$wb)
    $bw.Write([byte]$hb)
    $bw.Write([byte]0)
    $bw.Write([byte]0)
    $bw.Write([uint16]1)
    $bw.Write([uint16]32)
    $bw.Write([uint32]$s.Data.Length)
    $bw.Write([uint32]$offset)
    $offset += $s.Data.Length
}

foreach ($s in $streams) { $bw.Write($s.Data) }
$bw.Flush()
[System.IO.File]::WriteAllBytes($out, $ico.ToArray())
$ico.Dispose()

Write-Host "ICO 생성 완료: $out"
