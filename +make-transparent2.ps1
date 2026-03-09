Add-Type -AssemblyName System.Drawing

$root = "C:\Users\admin\source\repos\+Playground"

$targets = @(
    "Applications\Files\Manager\Folder.Purge\Resources\app.ico",
    "Applications\Media\Mosaic.Forge\Resources\app.ico",
    "Applications\Tools\Dev\Debug\Log.Lens\Resources\app.ico",
    "Applications\Tools\Dev\Debug\Signal.Flow\Resources\app.ico",
    "Applications\Tools\Dev\Network\Api.Probe\Resources\app.ico",
    "Applications\Tools\Dev\System\Sched.Cast\Resources\app.ico",
    "Applications\Tools\Productivity\Utility\Dict.Cast\Resources\app.ico",
    "Games\Arcade\Neon.Slice\Resources\app.ico"
)

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

    [System.IO.File]::WriteAllBytes($outPath, $ms2.ToArray())
    $bw.Dispose()
    $ms2.Dispose()
}

# Load largest PNG frame from ICO binary
function Load-IcoBitmap([string]$path) {
    $bytes = [System.IO.File]::ReadAllBytes($path)
    $br    = New-Object System.IO.BinaryReader((New-Object System.IO.MemoryStream($bytes, $false)))

    $br.ReadUInt16() | Out-Null  # reserved
    $br.ReadUInt16() | Out-Null  # type
    $count = [int]$br.ReadUInt16()

    $entries = @()
    for ($i = 0; $i -lt $count; $i++) {
        $w  = [int]$br.ReadByte(); if ($w -eq 0) { $w = 256 }
        $h  = [int]$br.ReadByte(); if ($h -eq 0) { $h = 256 }
        $br.ReadByte() | Out-Null; $br.ReadByte() | Out-Null
        $br.ReadUInt16() | Out-Null; $br.ReadUInt16() | Out-Null
        $dataSize   = [int]$br.ReadUInt32()
        $dataOffset = [int]$br.ReadUInt32()
        $entries   += @{ W=$w; H=$h; Size=$dataSize; Offset=$dataOffset }
    }
    $br.Dispose()

    # Pick largest entry
    $best = $entries | Sort-Object { $_.W } -Descending | Select-Object -First 1
    $imgData = $bytes[$best.Offset..($best.Offset + $best.Size - 1)]

    $ms  = New-Object System.IO.MemoryStream($imgData, $false)
    $bmp = [System.Drawing.Bitmap]::FromStream($ms)
    $result = $bmp.Clone(
        [System.Drawing.Rectangle]::new(0, 0, $bmp.Width, $bmp.Height),
        [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $bmp.Dispose(); $ms.Dispose()
    return $result
}

# Flood-fill transparency from all 4 corners with color tolerance
function Remove-Background([System.Drawing.Bitmap]$bmp, [int]$tolerance = 40) {
    $w = $bmp.Width; $h = $bmp.Height

    # Sample background color from corner (pick corner with highest alpha = most solid)
    $corners = @(
        $bmp.GetPixel(0,     0),
        $bmp.GetPixel($w-1,  0),
        $bmp.GetPixel(0,    $h-1),
        $bmp.GetPixel($w-1, $h-1)
    )
    $bgColor = $corners | Sort-Object { $_.A } -Descending | Select-Object -First 1

    # If all corners are already transparent (alpha < 20), background is already transparent
    $maxA = ($corners | Measure-Object -Property A -Maximum).Maximum
    if ($maxA -lt 20) {
        return $bmp  # already transparent, do nothing
    }

    $bgR = $bgColor.R; $bgG = $bgColor.G; $bgB = $bgColor.B

    # BFS flood fill from all 4 corners
    $visited = New-Object bool[]($w * $h)
    $queue   = New-Object System.Collections.Generic.Queue[System.Drawing.Point]

    $seeds = @(
        [System.Drawing.Point]::new(0,     0),
        [System.Drawing.Point]::new($w-1,  0),
        [System.Drawing.Point]::new(0,    $h-1),
        [System.Drawing.Point]::new($w-1, $h-1)
    )
    foreach ($s in $seeds) {
        $idx = $s.Y * $w + $s.X
        if (-not $visited[$idx]) {
            $visited[$idx] = $true
            $queue.Enqueue($s)
        }
    }

    $dirs = @(
        [System.Drawing.Point]::new(1,0), [System.Drawing.Point]::new(-1,0),
        [System.Drawing.Point]::new(0,1), [System.Drawing.Point]::new(0,-1)
    )

    while ($queue.Count -gt 0) {
        $pt  = $queue.Dequeue()
        $px  = $pt.X; $py = $pt.Y
        $col = $bmp.GetPixel($px, $py)

        # Color distance in RGB space
        $dr  = [int]$col.R - [int]$bgR
        $dg  = [int]$col.G - [int]$bgG
        $db  = [int]$col.B - [int]$bgB
        $dist = [Math]::Sqrt($dr*$dr + $dg*$dg + $db*$db)

        # Also consider alpha: nearly transparent pixels are treated as background
        if ($dist -le $tolerance -or $col.A -lt 20) {
            $bmp.SetPixel($px, $py, [System.Drawing.Color]::Transparent)

            foreach ($d in $dirs) {
                $nx = $px + $d.X; $ny = $py + $d.Y
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

    return $bmp
}

foreach ($rel in $targets) {
    $path = Join-Path $root $rel
    if (-not (Test-Path $path)) { Write-Host "SKIP: $rel"; continue }

    try {
        $bmp = Load-IcoBitmap $path
    } catch {
        $errMsg = $_.ToString(); Write-Host "ERROR loading ${rel}: $errMsg"; continue
    }

    # Check corner alpha to decide approach
    $corners = @(
        $bmp.GetPixel(0, 0),
        $bmp.GetPixel($bmp.Width-1, 0),
        $bmp.GetPixel(0, $bmp.Height-1),
        $bmp.GetPixel($bmp.Width-1, $bmp.Height-1)
    )
    $maxA = ($corners | Measure-Object -Property A -Maximum).Maximum

    if ($maxA -lt 20) {
        # Background already transparent — just re-save to normalize all sizes
        Write-Host "OK (already transparent): $rel"
        Save-Ico $bmp $path
    } else {
        # Solid background — flood fill removal
        $bgColor = $corners | Sort-Object { $_.A } -Descending | Select-Object -First 1
        $bmp = Remove-Background $bmp 40
        Save-Ico $bmp $path
        Write-Host "OK (bg removed): $rel  bg=$bgColor"
    }

    $bmp.Dispose()
}

Write-Host "Done."
