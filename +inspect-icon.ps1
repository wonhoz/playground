Add-Type -AssemblyName System.Drawing

$path = "C:\Users\admin\source\repos\+Playground\Applications\Files\Manager\Folder.Purge\Resources\app.ico"
$bytes = [System.IO.File]::ReadAllBytes($path)
$br    = New-Object System.IO.BinaryReader((New-Object System.IO.MemoryStream($bytes, $false)))

$br.ReadUInt16() | Out-Null
$br.ReadUInt16() | Out-Null
$count = [int]$br.ReadUInt16()
Write-Host "Frame count: $count"

$entries = @()
for ($i = 0; $i -lt $count; $i++) {
    $w  = [int]$br.ReadByte(); if ($w -eq 0) { $w = 256 }
    $h  = [int]$br.ReadByte(); if ($h -eq 0) { $h = 256 }
    $br.ReadByte() | Out-Null; $br.ReadByte() | Out-Null
    $br.ReadUInt16() | Out-Null; $br.ReadUInt16() | Out-Null
    $dataSize   = [int]$br.ReadUInt32()
    $dataOffset = [int]$br.ReadUInt32()
    Write-Host ("  Frame {0}: {1}x{2}, size={3}, offset={4}" -f $i,$w,$h,$dataSize,$dataOffset)
    $entries += @{ W=$w; H=$h; Size=$dataSize; Offset=$dataOffset }
}
$br.Dispose()

# Load largest frame
$best = $entries | Sort-Object { $_.W } -Descending | Select-Object -First 1
$imgData = $bytes[$best.Offset..($best.Offset + $best.Size - 1)]
$ms  = New-Object System.IO.MemoryStream($imgData, $false)
$bmp = [System.Drawing.Bitmap]::FromStream($ms)
$bmp32 = $bmp.Clone([System.Drawing.Rectangle]::new(0, 0, $bmp.Width, $bmp.Height),
                    [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$bmp.Dispose(); $ms.Dispose()

Write-Host "Largest frame size: $($bmp32.Width)x$($bmp32.Height)"

# Sample key pixels
Write-Host "Corner pixels:"
Write-Host "  (0,0)   = $($bmp32.GetPixel(0,0))"
Write-Host "  (255,0) = $($bmp32.GetPixel(255,0))"
Write-Host "  (0,255) = $($bmp32.GetPixel(0,255))"
Write-Host "  (255,255)=$($bmp32.GetPixel(255,255))"

Write-Host "Center area pixels:"
Write-Host "  (128,128)=$($bmp32.GetPixel(128,128))"
Write-Host "  (128,100)=$($bmp32.GetPixel(128,100))"
Write-Host "  (128,150)=$($bmp32.GetPixel(128,150))"
Write-Host "  (64,128) =$($bmp32.GetPixel(64,128))"
Write-Host "  (192,128)=$($bmp32.GetPixel(192,128))"

# Find bounding box of non-transparent pixels
$minX = 256; $maxX = 0; $minY = 256; $maxY = 0
for ($y = 0; $y -lt 256; $y++) {
    for ($x = 0; $x -lt 256; $x++) {
        $px = $bmp32.GetPixel($x, $y)
        if ($px.A -gt 10) {
            if ($x -lt $minX) { $minX = $x }
            if ($x -gt $maxX) { $maxX = $x }
            if ($y -lt $minY) { $minY = $y }
            if ($y -gt $maxY) { $maxY = $y }
        }
    }
}
Write-Host "Bounding box of visible pixels: ($minX,$minY) to ($maxX,$maxY)"

# Save as PNG for inspection
$bmp32.Save("C:\Users\admin\Desktop\folder_purge_inspect.png")
Write-Host "Saved to Desktop: folder_purge_inspect.png"
$bmp32.Dispose()
