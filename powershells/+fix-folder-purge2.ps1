Add-Type -AssemblyName System.Drawing

$icoPath = "C:\Users\admin\source\repos\+Playground\Applications\Files\Manager\Folder.Purge\Resources\app.ico"

function Load-IcoBitmap([string]$path) {
    $bytes = [System.IO.File]::ReadAllBytes($path)
    $br    = New-Object System.IO.BinaryReader((New-Object System.IO.MemoryStream($bytes, $false)))
    $br.ReadUInt16() | Out-Null; $br.ReadUInt16() | Out-Null
    $count = [int]$br.ReadUInt16()
    $entries = @()
    for ($i = 0; $i -lt $count; $i++) {
        $ew = [int]$br.ReadByte(); if ($ew -eq 0) { $ew = 256 }
        $eh = [int]$br.ReadByte(); if ($eh -eq 0) { $eh = 256 }
        $br.ReadByte() | Out-Null; $br.ReadByte() | Out-Null
        $br.ReadUInt16() | Out-Null; $br.ReadUInt16() | Out-Null
        $sz  = [int]$br.ReadUInt32()
        $off = [int]$br.ReadUInt32()
        $entries += @{ W=$ew; H=$eh; Size=$sz; Offset=$off }
    }
    $br.Dispose()
    $best    = $entries | Sort-Object { $_.W } -Descending | Select-Object -First 1
    $imgData = $bytes[$best.Offset..($best.Offset + $best.Size - 1)]
    $ms      = New-Object System.IO.MemoryStream($imgData, $false)
    $bmp     = [System.Drawing.Bitmap]::FromStream($ms)
    $result  = $bmp.Clone([System.Drawing.Rectangle]::new(0, 0, $bmp.Width, $bmp.Height),
                           [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $bmp.Dispose(); $ms.Dispose()
    return $result
}

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
        $pngs += @{ Data = $ms.ToArray() }
        $ms.Dispose(); $bmp.Dispose()
    }
    $ms2 = New-Object System.IO.MemoryStream
    $bw  = New-Object System.IO.BinaryWriter($ms2)
    $bw.Write([uint16]0); $bw.Write([uint16]1); $bw.Write([uint16]$sizes.Count)
    $dataOffset = 6 + 16 * $sizes.Count
    for ($i = 0; $i -lt $sizes.Count; $i++) {
        $sz  = $sizes[$i]; $data = $pngs[$i].Data
        $szB = if ($sz -eq 256) { [byte]0 } else { [byte]$sz }
        $bw.Write($szB); $bw.Write($szB)
        $bw.Write([byte]0); $bw.Write([byte]0)
        $bw.Write([uint16]1); $bw.Write([uint16]32)
        $bw.Write([uint32]$data.Length)
        $bw.Write([uint32]$dataOffset)
        $dataOffset += $data.Length
    }
    foreach ($p in $pngs) { $bw.Write($p.Data) }
    $bw.Flush()
    [System.IO.File]::WriteAllBytes($outPath, $ms2.ToArray())
    $bw.Dispose(); $ms2.Dispose()
}

# ── 1. Load ──────────────────────────────────────────────────────────────────
$src = Load-IcoBitmap $icoPath
$w   = $src.Width; $h = $src.Height

Write-Host ("Loaded: {0}x{1}" -f $w, $h)
Write-Host ("(0,0)  = {0}" -f $src.GetPixel(0,0))
Write-Host ("(20,20)= {0}" -f $src.GetPixel(20,20))

# ── 2. Detect background color by scanning diagonally for first opaque px ────
$bgColor = $null
for ($d = 0; $d -lt 80; $d++) {
    $px = $src.GetPixel($d, $d)
    if ($px.A -gt 50) { $bgColor = $px; break }
}
if (-not $bgColor) { $bgColor = $src.GetPixel(20, 20) }
$bgR = $bgColor.R; $bgG = $bgColor.G; $bgB = $bgColor.B
Write-Host ("Background: R={0} G={1} B={2} A={3}" -f $bgR,$bgG,$bgB,$bgColor.A)

# ── 3. Flood-fill from all edges — background removal only, NO scaling ────────
$tolerance = 50
$visited   = New-Object bool[]($w * $h)
$queue     = New-Object System.Collections.Generic.Queue[System.Drawing.Point]

$hm1 = $h - 1; $wm1 = $w - 1

for ($x = 0; $x -lt $w; $x++) {
    foreach ($ey in @(0, $hm1)) {
        $px   = $src.GetPixel($x, $ey)
        $dr   = [int]$px.R - [int]$bgR
        $dg   = [int]$px.G - [int]$bgG
        $db   = [int]$px.B - [int]$bgB
        $dist = [Math]::Sqrt($dr*$dr + $dg*$dg + $db*$db)
        if ($px.A -lt 20 -or $dist -le $tolerance) {
            $idx = $ey * $w + $x
            if (-not $visited[$idx]) { $visited[$idx] = $true; $queue.Enqueue([System.Drawing.Point]::new($x, $ey)) }
        }
    }
}
for ($y = 1; $y -lt $hm1; $y++) {
    foreach ($ex in @(0, $wm1)) {
        $px   = $src.GetPixel($ex, $y)
        $dr   = [int]$px.R - [int]$bgR
        $dg   = [int]$px.G - [int]$bgG
        $db   = [int]$px.B - [int]$bgB
        $dist = [Math]::Sqrt($dr*$dr + $dg*$dg + $db*$db)
        if ($px.A -lt 20 -or $dist -le $tolerance) {
            $idx = $y * $w + $ex
            if (-not $visited[$idx]) { $visited[$idx] = $true; $queue.Enqueue([System.Drawing.Point]::new($ex, $y)) }
        }
    }
}

$dirs = @(
    [System.Drawing.Point]::new(1,0), [System.Drawing.Point]::new(-1,0),
    [System.Drawing.Point]::new(0,1), [System.Drawing.Point]::new(0,-1)
)

while ($queue.Count -gt 0) {
    $pt  = $queue.Dequeue()
    $px_x = $pt.X; $px_y = $pt.Y
    $col  = $src.GetPixel($px_x, $px_y)
    $dr   = [int]$col.R - [int]$bgR
    $dg   = [int]$col.G - [int]$bgG
    $db   = [int]$col.B - [int]$bgB
    $dist = [Math]::Sqrt($dr*$dr + $dg*$dg + $db*$db)
    if ($col.A -lt 20 -or $dist -le $tolerance) {
        $src.SetPixel($px_x, $px_y, [System.Drawing.Color]::Transparent)
        foreach ($d in $dirs) {
            $nx = $px_x + $d.X; $ny = $px_y + $d.Y
            if ($nx -ge 0 -and $nx -lt $w -and $ny -ge 0 -and $ny -lt $h) {
                $idx2 = $ny * $w + $nx
                if (-not $visited[$idx2]) {
                    $visited[$idx2] = $true
                    $queue.Enqueue([System.Drawing.Point]::new($nx, $ny))
                }
            }
        }
    }
}

# ── 4. Save preview + ICO (no scaling) ──────────────────────────────────────
$src.Save("C:\Users\admin\Desktop\folder_purge_v2.png")
Write-Host "Preview: Desktop\folder_purge_v2.png"

Save-Ico $src $icoPath
$src.Dispose()
Write-Host "Done."
