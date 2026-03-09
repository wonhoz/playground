Add-Type -AssemblyName System.Drawing

$icoPath = "C:\Users\admin\source\repos\+Playground\Applications\Files\Manager\Folder.Purge\Resources\app.ico"

function Load-IcoBitmap([string]$path) {
    $bytes = [System.IO.File]::ReadAllBytes($path)
    $br    = New-Object System.IO.BinaryReader((New-Object System.IO.MemoryStream($bytes, $false)))
    $br.ReadUInt16() | Out-Null; $br.ReadUInt16() | Out-Null
    $count = [int]$br.ReadUInt16()
    $entries = @()
    for ($i = 0; $i -lt $count; $i++) {
        $w = [int]$br.ReadByte(); if ($w -eq 0) { $w = 256 }
        $h = [int]$br.ReadByte(); if ($h -eq 0) { $h = 256 }
        $br.ReadByte() | Out-Null; $br.ReadByte() | Out-Null
        $br.ReadUInt16() | Out-Null; $br.ReadUInt16() | Out-Null
        $sz = [int]$br.ReadUInt32(); $off = [int]$br.ReadUInt32()
        $entries += @{ W=$w; H=$h; Size=$sz; Offset=$off }
    }
    $br.Dispose()
    $best = $entries | Sort-Object { $_.W } -Descending | Select-Object -First 1
    $imgData = $bytes[$best.Offset..($best.Offset + $best.Size - 1)]
    $ms  = New-Object System.IO.MemoryStream($imgData, $false)
    $bmp = [System.Drawing.Bitmap]::FromStream($ms)
    $result = $bmp.Clone([System.Drawing.Rectangle]::new(0, 0, $bmp.Width, $bmp.Height),
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

# ── 1. Load original bitmap ─────────────────────────────────────────────────
$src = Load-IcoBitmap $icoPath
$w = $src.Width; $h = $src.Height   # 256x256

# ── 2. Find background color: scan inward from edges for first opaque pixel ─
$bgColor = $null
for ($d = 0; $d -lt 60; $d++) {
    $px = $src.GetPixel($d, $d)
    if ($px.A -gt 50) { $bgColor = $px; break }
}
if (-not $bgColor) { $bgColor = $src.GetPixel(20, 20) }
Write-Host ("Background color detected: R={0} G={1} B={2} A={3}" -f $bgColor.R,$bgColor.G,$bgColor.B,$bgColor.A)

$bgR = $bgColor.R; $bgG = $bgColor.G; $bgB = $bgColor.B

# ── 3. Flood-fill background removal from edge seed points ──────────────────
$visited = New-Object bool[]($w * $h)
$queue   = New-Object System.Collections.Generic.Queue[System.Drawing.Point]
$tolerance = 45

# Seed from every pixel along all 4 edges that matches the background color
$hm1 = $h - 1; $wm1 = $w - 1
for ($x = 0; $x -lt $w; $x++) {
    foreach ($ey in @(0, $hm1)) {
        $px  = $src.GetPixel($x, $ey)
        $dr  = [int]$px.R - [int]$bgR
        $dg  = [int]$px.G - [int]$bgG
        $db  = [int]$px.B - [int]$bgB
        $dist = [Math]::Sqrt($dr*$dr + $dg*$dg + $db*$db)
        if ($px.A -lt 20 -or $dist -le $tolerance) {
            $idx = $ey * $w + $x
            if (-not $visited[$idx]) { $visited[$idx] = $true; $queue.Enqueue([System.Drawing.Point]::new($x, $ey)) }
        }
    }
}
for ($y = 1; $y -lt $hm1; $y++) {
    foreach ($ex in @(0, $wm1)) {
        $px  = $src.GetPixel($ex, $y)
        $dr  = [int]$px.R - [int]$bgR
        $dg  = [int]$px.G - [int]$bgG
        $db  = [int]$px.B - [int]$bgB
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
    $col = $src.GetPixel($px_x, $px_y)
    $dr  = [int]$col.R - [int]$bgR
    $dg  = [int]$col.G - [int]$bgG
    $db  = [int]$col.B - [int]$bgB
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

# ── 4. Find bounding box of remaining visible content ───────────────────────
$minX = $w; $maxX = 0; $minY = $h; $maxY = 0
for ($y = 0; $y -lt $h; $y++) {
    for ($x = 0; $x -lt $w; $x++) {
        if ($src.GetPixel($x, $y).A -gt 10) {
            if ($x -lt $minX) { $minX = $x }
            if ($x -gt $maxX) { $maxX = $x }
            if ($y -lt $minY) { $minY = $y }
            if ($y -gt $maxY) { $maxY = $y }
        }
    }
}
Write-Host ("Folder icon bounds: ({0},{1}) to ({2},{3})" -f $minX,$minY,$maxX,$maxY)
$contentW = $maxX - $minX + 1
$contentH = $maxY - $minY + 1
Write-Host ("Content size: {0}x{1}" -f $contentW,$contentH)

# ── 5. Scale up: crop content and expand to fill more of the canvas ──────────
# Target: leave ~10px padding on each side (236x236 usable area)
$targetSize = 236
$padding    = ($w - $targetSize) / 2   # 10px each side

$dst = New-Object System.Drawing.Bitmap($w, $h, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$g   = [System.Drawing.Graphics]::FromImage($dst)
$g.Clear([System.Drawing.Color]::Transparent)
$g.InterpolationMode  = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$g.SmoothingMode      = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality

$srcRect = [System.Drawing.Rectangle]::new($minX, $minY, $contentW, $contentH)
$dstRect = [System.Drawing.Rectangle]::new([int]$padding, [int]$padding, $targetSize, $targetSize)
$g.DrawImage($src, $dstRect, $srcRect, [System.Drawing.GraphicsUnit]::Pixel)
$g.Dispose()
$src.Dispose()

# Save preview
$dst.Save("C:\Users\admin\Desktop\folder_purge_fixed.png")
Write-Host "Preview saved to Desktop: folder_purge_fixed.png"

# Save ICO
Save-Ico $dst $icoPath
$dst.Dispose()
Write-Host "ICO saved."
