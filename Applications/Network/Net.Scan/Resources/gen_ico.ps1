Add-Type -AssemblyName System.Drawing

function Make-Png([int]$sz) {
    $bmp = New-Object System.Drawing.Bitmap($sz, $sz)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.Clear([System.Drawing.Color]::Transparent)

    $bgC  = [System.Drawing.Color]::FromArgb(17, 17, 24)
    $accC = [System.Drawing.Color]::FromArgb(6, 182, 212)
    $dimC = [System.Drawing.Color]::FromArgb(80, 6, 182, 212)
    $swpC = [System.Drawing.Color]::FromArgb(45, 6, 182, 212)

    $bgB  = New-Object System.Drawing.SolidBrush($bgC)
    $accB = New-Object System.Drawing.SolidBrush($accC)
    $dimB = New-Object System.Drawing.SolidBrush($dimC)
    $swpB = New-Object System.Drawing.SolidBrush($swpC)
    $pw   = [float][Math]::Max(1.0, $sz / 24.0)
    $dPen = New-Object System.Drawing.Pen($dimC, $pw)

    $g.FillEllipse($bgB, 0, 0, $sz - 1, $sz - 1)

    $m  = [int]($sz * 0.06)
    $r1 = $sz - $m * 2
    $r2 = [int]($sz * 0.55)
    $r3 = [int]($sz * 0.28)
    $cx = $sz / 2.0
    $cy = $sz / 2.0

    $g.DrawEllipse($dPen, $m, $m, $r1, $r1)
    $g.DrawEllipse($dPen, [int]($cx - $r2/2), [int]($cy - $r2/2), $r2, $r2)
    $g.DrawEllipse($dPen, [int]($cx - $r3/2), [int]($cy - $r3/2), $r3, $r3)
    $g.DrawLine($dPen, $m, [int]$cy, $sz - $m, [int]$cy)
    $g.DrawLine($dPen, [int]$cx, $m, [int]$cx, $sz - $m)
    $g.FillPie($swpB, $m, $m, $r1, $r1, [float]-90.0, [float]70.0)

    $dr = [int]([Math]::Max(2.0, $sz * 0.045))
    $d1x = [int]($cx + $sz * 0.22); $d1y = [int]($cy - $sz * 0.15)
    $d2x = [int]($cx - $sz * 0.12); $d2y = [int]($cy + $sz * 0.18)
    $d3x = [int]($cx + $sz * 0.08); $d3y = [int]($cy + $sz * 0.28)
    $g.FillEllipse($accB, $d1x - $dr, $d1y - $dr, $dr*2, $dr*2)
    $g.FillEllipse($accB, $d2x - $dr, $d2y - $dr, $dr*2, $dr*2)
    $g.FillEllipse($dimB, $d3x - $dr, $d3y - $dr, $dr*2, $dr*2)
    $cd = [int]([Math]::Max(2.0, $sz * 0.06))
    $g.FillEllipse($accB, [int]$cx - $cd, [int]$cy - $cd, $cd*2, $cd*2)

    $g.Dispose(); $dPen.Dispose()
    $bgB.Dispose(); $accB.Dispose(); $dimB.Dispose(); $swpB.Dispose()

    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    return $ms.ToArray()
}

$dir     = Split-Path $MyInvocation.MyCommand.Path -Parent
$outPath = Join-Path $dir "app.ico"
$sizes   = 16, 32, 48, 256

$pngs = New-Object 'System.Collections.Generic.List[byte[]]'
foreach ($s in $sizes) { $pngs.Add((Make-Png $s)) }

$count  = $sizes.Count
$hdrSz  = 6 + 16 * $count
$off    = $hdrSz
$offs   = @()
foreach ($p in $pngs) { $offs += $off; $off += $p.Length }

$out = New-Object System.IO.MemoryStream
$bw  = New-Object System.IO.BinaryWriter($out)
$bw.Write([uint16]0); $bw.Write([uint16]1); $bw.Write([uint16]$count)

for ($i = 0; $i -lt $count; $i++) {
    $sz = $sizes[$i]
    if ($sz -ge 256) { $sv = [byte]0 } else { $sv = [byte]$sz }
    $bw.Write($sv); $bw.Write($sv)
    $bw.Write([byte]0); $bw.Write([byte]0)
    $bw.Write([uint16]1); $bw.Write([uint16]32)
    $bw.Write([uint32]($pngs[$i].Length))
    $bw.Write([uint32]$offs[$i])
}
foreach ($p in $pngs) { $bw.Write($p) }
$bw.Flush()

[System.IO.File]::WriteAllBytes($outPath, $out.ToArray())
$bw.Dispose(); $out.Dispose()
Write-Host "완료: $outPath"
