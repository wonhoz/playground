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
    "Applications\Tools\Productivity\Utility\Mouse.Flick\Resources\app.ico",
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
    $bw.Write([uint16]0)
    $bw.Write([uint16]1)
    $bw.Write([uint16]$sizes.Count)

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

# Load largest bitmap from ICO - handles PNG-in-ICO format
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

    $ms = New-Object System.IO.MemoryStream($imgData, $false)
    $bmp = [System.Drawing.Bitmap]::FromStream($ms)
    # Clone to detach from stream
    $result = $bmp.Clone([System.Drawing.Rectangle]::new(0, 0, $bmp.Width, $bmp.Height),
                          [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $bmp.Dispose()
    $ms.Dispose()
    return $result
}

foreach ($rel in $targets) {
    $path = Join-Path $root $rel
    if (-not (Test-Path $path)) {
        Write-Host "SKIP (not found): $rel"
        continue
    }

    try {
        $bmp = Load-IcoBitmap $path
    } catch {
        Write-Host "ERROR loading $rel : $_"
        continue
    }

    # Detect background colour from corner pixel
    $bgColor = $bmp.GetPixel(0, 0)

    # Make corner colour transparent
    $bmp.MakeTransparent($bgColor)

    Save-Ico $bmp $path
    $bmp.Dispose()

    Write-Host "OK: $rel  (bg=$bgColor)"
}

Write-Host "Done: $($targets.Count) icons processed"
