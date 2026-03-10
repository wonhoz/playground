<#
  +generate-icons.ps1
  Playground 솔루션 전체 프로젝트 아이콘 생성기
  - 투명 배경, 심플·귀여운 스타일
  - 멀티사이즈 ICO (256/48/32/16px)
#>
param([string]$Root = "C:\Users\admin\source\repos\+Playground")

Add-Type -AssemblyName System.Drawing

# ────────────────────────────────────────────────────────────────────────────
#  색상 / 브러시 / 펜 헬퍼
# ────────────────────────────────────────────────────────────────────────────
function C([string]$hex, [int]$a = 255) {
    [System.Drawing.Color]::FromArgb($a,
        [Convert]::ToInt32($hex.Substring(1,2),16),
        [Convert]::ToInt32($hex.Substring(3,2),16),
        [Convert]::ToInt32($hex.Substring(5,2),16))
}
function P([string]$hex, [float]$w, [int]$a = 255) {
    $pen = [System.Drawing.Pen]::new((C $hex $a), $w)
    $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $pen.EndCap   = [System.Drawing.Drawing2D.LineCap]::Round
    $pen
}

# ────────────────────────────────────────────────────────────────────────────
#  캔버스 생성
# ────────────────────────────────────────────────────────────────────────────
function New-Canvas {
    $bmp = [System.Drawing.Bitmap]::new(256, 256, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode     = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
    $g.PixelOffsetMode   = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.Clear([System.Drawing.Color]::Transparent)
    @{ Bmp = $bmp; G = $g }
}

# ────────────────────────────────────────────────────────────────────────────
#  멀티사이즈 ICO 저장
# ────────────────────────────────────────────────────────────────────────────
function Save-Ico([System.Drawing.Bitmap]$src, [string]$outPath) {
    $dir = [IO.Path]::GetDirectoryName($outPath)
    if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }

    $sizes = @(256, 48, 32, 16)
    $pngs  = @()
    foreach ($sz in $sizes) {
        $resized = [System.Drawing.Bitmap]::new($sz, $sz, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $rg = [System.Drawing.Graphics]::FromImage($resized)
        $rg.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $rg.SmoothingMode     = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $rg.PixelOffsetMode   = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $rg.Clear([System.Drawing.Color]::Transparent)
        $rg.DrawImage($src, 0, 0, $sz, $sz)
        $rg.Dispose()
        $ms = [IO.MemoryStream]::new()
        $resized.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
        $pngs += , $ms.ToArray()
        $ms.Dispose(); $resized.Dispose()
    }

    $ms = [IO.MemoryStream]::new()
    $bw = [IO.BinaryWriter]::new($ms)
    $bw.Write([uint16]0); $bw.Write([uint16]1); $bw.Write([uint16]$sizes.Count)
    $offset = 6 + 16 * $sizes.Count
    for ($i = 0; $i -lt $sizes.Count; $i++) {
        $sz = $sizes[$i]
        $szB = if ($sz -eq 256) { [byte]0 } else { [byte]$sz }
        $bw.Write($szB); $bw.Write($szB)
        $bw.Write([byte]0); $bw.Write([byte]0)
        $bw.Write([uint16]1); $bw.Write([uint16]32)
        $bw.Write([uint32]$pngs[$i].Length); $bw.Write([uint32]$offset)
        $offset += $pngs[$i].Length
    }
    foreach ($b in $pngs) { $bw.Write($b) }
    $bw.Flush()
    [IO.File]::WriteAllBytes($outPath, $ms.ToArray())
    $bw.Dispose(); $ms.Dispose()
}

# ────────────────────────────────────────────────────────────────────────────
#  도형 헬퍼
# ────────────────────────────────────────────────────────────────────────────
function New-RrPath([float]$x,[float]$y,[float]$w,[float]$h,[float]$r) {
    $p = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $p.AddArc($x,         $y,         $r*2,$r*2, 180,90)
    $p.AddArc($x+$w-$r*2, $y,         $r*2,$r*2, 270,90)
    $p.AddArc($x+$w-$r*2, $y+$h-$r*2, $r*2,$r*2,   0,90)
    $p.AddArc($x,         $y+$h-$r*2, $r*2,$r*2,  90,90)
    $p.CloseFigure(); $p
}
function FillC($g,[string]$h,[float]$cx,[float]$cy,[float]$r,[int]$a=255) {
    $b=[System.Drawing.SolidBrush]::new((C $h $a)); $g.FillEllipse($b,$cx-$r,$cy-$r,$r*2,$r*2); $b.Dispose()
}
function StrkC($g,[string]$h,[float]$cx,[float]$cy,[float]$r,[float]$sw=5,[int]$a=255) {
    $p=[System.Drawing.Pen]::new((C $h $a),$sw); $g.DrawEllipse($p,$cx-$r,$cy-$r,$r*2,$r*2); $p.Dispose()
}
function Fr($g,[string]$h,[float]$x,[float]$y,[float]$w,[float]$ht,[int]$a=255) {
    $b=[System.Drawing.SolidBrush]::new((C $h $a)); $g.FillRectangle($b,$x,$y,$w,$ht); $b.Dispose()
}
function Frr($g,[string]$h,[float]$x,[float]$y,[float]$w,[float]$ht,[float]$r,[int]$a=255) {
    $path=New-RrPath $x $y $w $ht $r
    $b=[System.Drawing.SolidBrush]::new((C $h $a)); $g.FillPath($b,$path); $b.Dispose(); $path.Dispose()
}
function Srr($g,[string]$h,[float]$x,[float]$y,[float]$w,[float]$ht,[float]$r,[float]$sw=5,[int]$a=255) {
    $path=New-RrPath $x $y $w $ht $r
    $p=[System.Drawing.Pen]::new((C $h $a),$sw); $g.DrawPath($p,$path); $p.Dispose(); $path.Dispose()
}
function Ln($g,[string]$h,[float]$x1,[float]$y1,[float]$x2,[float]$y2,[float]$sw=6,[int]$a=255) {
    $p=P $h $sw $a; $g.DrawLine($p,$x1,$y1,$x2,$y2); $p.Dispose()
}
function Ar($g,[string]$h,[float]$cx,[float]$cy,[float]$r,[float]$st,[float]$sw2,[float]$pen=5,[int]$a=255) {
    $p=[System.Drawing.Pen]::new((C $h $a),$pen); $g.DrawArc($p,$cx-$r,$cy-$r,$r*2,$r*2,$st,$sw2); $p.Dispose()
}
function Tx($g,[string]$h,[string]$t,[float]$sz,[float]$cx,[float]$cy,[bool]$bold=$true,[int]$a=255) {
    $style = if ($bold) { [System.Drawing.FontStyle]::Bold } else { [System.Drawing.FontStyle]::Regular }
    $font  = [System.Drawing.Font]::new("Segoe UI", $sz, $style, [System.Drawing.GraphicsUnit]::Pixel)
    $b     = [System.Drawing.SolidBrush]::new((C $h $a))
    $sf    = [System.Drawing.StringFormat]::new()
    $sf.Alignment = $sf.LineAlignment = [System.Drawing.StringAlignment]::Center
    $g.DrawString($t, $font, $b, [System.Drawing.RectangleF]::new($cx-128,$cy-128,256,256), $sf)
    $font.Dispose(); $b.Dispose(); $sf.Dispose()
}
function Sparkle($g,[string]$h,[float]$cx,[float]$cy,[float]$sz,[int]$a=255) {
    $s1 = $sz * 0.2
    $pts = [System.Drawing.PointF[]]@(
        [System.Drawing.PointF]::new($cx,       $cy - $sz),
        [System.Drawing.PointF]::new($cx + $s1, $cy - $s1),
        [System.Drawing.PointF]::new($cx + $sz, $cy),
        [System.Drawing.PointF]::new($cx + $s1, $cy + $s1),
        [System.Drawing.PointF]::new($cx,       $cy + $sz),
        [System.Drawing.PointF]::new($cx - $s1, $cy + $s1),
        [System.Drawing.PointF]::new($cx - $sz, $cy),
        [System.Drawing.PointF]::new($cx - $s1, $cy - $s1)
    )
    $b = [System.Drawing.SolidBrush]::new((C $h $a)); $g.FillPolygon($b,$pts); $b.Dispose()
}
function Check($g,[string]$h,[float]$cx,[float]$cy,[float]$sz,[float]$sw=7) {
    $s5 = $sz * 0.5
    $s1 = $sz * 0.1
    $s4 = $sz * 0.45
    $s6 = $sz * 0.6
    $p = P $h $sw; $p.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
    $g.DrawLine($p, $cx-$s5, $cy, $cx-$s1, $cy+$s4)
    $g.DrawLine($p, $cx-$s1, $cy+$s4, $cx+$s6, $cy-$s4)
    $p.Dispose()
}
function Poly($g,[string]$h,[float[][]]$coords,[int]$a=255) {
    $pts = [System.Drawing.PointF[]]($coords | ForEach-Object { [System.Drawing.PointF]::new($_[0],$_[1]) })
    $b=[System.Drawing.SolidBrush]::new((C $h $a)); $g.FillPolygon($b,$pts); $b.Dispose()
}

# ────────────────────────────────────────────────────────────────────────────
#  아이콘 생성 래퍼
# ────────────────────────────────────────────────────────────────────────────
$_ok=0; $_fail=0
function Make-Icon([string]$path,[scriptblock]$draw) {
    try {
        $c = New-Canvas
        & $draw $c.G
        $c.G.Dispose()
        Save-Ico $c.Bmp $path
        $c.Bmp.Dispose()
        $script:_ok++
        $name = [IO.Path]::GetFileName([IO.Path]::GetDirectoryName($path))
        Write-Host "  v $name" -ForegroundColor Green
    } catch {
        $script:_fail++
        Write-Host "  x $path : $_" -ForegroundColor Red
    }
}

Write-Host ""
Write-Host "=== Playground 아이콘 생성 시작 ===" -ForegroundColor Cyan

# ════════════════════════════════════════════════════════════════════════════
#  AI.Clip — 클립보드 + AI 스파클 (보라)
# ════════════════════════════════════════════════════════════════════════════
Make-Icon "$Root\Applications\AI\AI.Clip\Resources\app.ico" {
    param($g)
    Frr $g "#8B5CF6" 56 76 144 148 14
    Frr $g "#1E1B4B" 70 92 116 120 8
    Frr $g "#8B5CF6" 96 52 64 40 10
    Fr  $g "#8B5CF6" 108 64 40 28
    Ln $g "#A78BFA" 84 124 172 124 7
    Ln $g "#A78BFA" 84 148 152 148 7
    Ln $g "#A78BFA" 84 172 164 172 7
    Sparkle $g "#E9D5FF" 188 72 30
    Sparkle $g "#C4B5FD" 210 108 16
    Sparkle $g "#DDD6FE" 172 48 14
}

# Music.Player — 헤드폰 (초록)
Make-Icon "$Root\Applications\Audio\Music.Player\Resources\app.ico" {
    param($g)
    StrkC  $g "#22C55E" 128 116 80 10
    Ar  $g "#22C55E" 128 116 80 180 180 10
    Frr $g "#22C55E" 48  140 36 56 10
    Frr $g "#22C55E" 172 140 36 56 10
    FillC  $g "#22C55E" 128 116 28
    FillC  $g "#052E16" 128 116 16
    Sparkle $g "#86EFAC" 196 68 20
}

# Stay.Awake — 커피잔 (노랑)
Make-Icon "$Root\Applications\Automation\Stay.Awake\Resources\app.ico" {
    param($g)
    Poly $g "#EAB308" @(@(72,100),@(80,188),@(176,188),@(184,100))
    Frr  $g "#1A1000" 80 110 96 70 6
    Ar $g "#EAB308" 198 148 28 270 180 10
    Frr $g "#EAB308" 56 188 144 14 7
    Ar $g "#FDE68A" 100 68 20 200 140 7
    Ar $g "#FDE68A" 128 56 18 200 140 7
    Ar $g "#FDE68A" 156 68 20 200 140 7
}

# Batch.Rename — 파일 A→B (주황)
Make-Icon "$Root\Applications\Files\Manager\Batch.Rename\Resources\app.ico" {
    param($g)
    Frr $g "#F97316" 24 64 96 120 10
    Frr $g "#1A0A00" 32 80 80 96 6
    Frr $g "#F97316" 136 72 96 120 10
    Frr $g "#1A0A00" 144 88 80 96 6
    Tx  $g "#FED7AA" "A" 68 72 108 $true
    Tx  $g "#FED7AA" "B" 68 184 108 $true
    Ln $g "#FB923C" 96 144 136 128 8
    Poly $g "#FB923C" @(@(130,116),@(150,128),@(130,140))
}

# File.Duplicates — 겹친 파일 (주황)
Make-Icon "$Root\Applications\Files\Manager\File.Duplicates\Resources\app.ico" {
    param($g)
    Frr $g "#EA580C" 24 60 110 140 10
    Frr $g "#1A0A00" 36 76 86 108 6
    Frr $g "#F97316" 64 88 110 140 10
    Frr $g "#1A0A00" 76 104 86 108 6
    Frr $g "#FB923C" 108 40 60 26 6
    Ln $g "#FED7AA" 84 124 140 124 6
    Ln $g "#FED7AA" 84 148 130 148 6
}

# File.Unlocker — 열린 자물쇠 (파랑)
Make-Icon "$Root\Applications\Files\Manager\File.Unlocker\Resources\app.ico" {
    param($g)
    Frr $g "#3B82F6" 56 136 144 96 14
    Frr $g "#1E3A5F" 72 152 112 64 8
    Ar $g "#3B82F6" 160 108 52 210 200 11
    FillC $g "#60A5FA" 128 148 20
    Ln $g "#93C5FD" 128 148 128 188 8
}

# Folder.Purge — 폴더 + 불꽃 (빨강)
Make-Icon "$Root\Applications\Files\Manager\Folder.Purge\Resources\app.ico" {
    param($g)
    Frr $g "#DC2626" 28 80 72 28 8
    Frr $g "#EF4444" 28 100 200 116 10
    Frr $g "#3F0000" 40 114 176 88 6
    Poly $g "#FB923C" @(@(104,148),@(128,84),@(152,148),@(140,136),@(128,108),@(116,136))
    Poly $g "#FEF08A" @(@(114,148),@(128,108),@(142,148),@(136,138),@(128,120),@(120,138))
    FillC $g "#FEF08A" 128 148 10
}

# Disk.Lens — 디스크 + 돋보기 (청록)
Make-Icon "$Root\Applications\Files\Inspector\Disk.Lens\Resources\app.ico" {
    param($g)
    Frr $g "#0E7490" 24 48 160 164 16
    Frr $g "#083344" 44 68 120 124 10
    FillC  $g "#06B6D4" 104 130 28
    FillC  $g "#083344" 104 130 14
    StrkC $g "#06B6D4" 168 160 42 10
    Ln $g "#06B6D4" 200 192 228 222 12
}

# Hash.Check — # + 체크 (청록)
Make-Icon "$Root\Applications\Files\Inspector\Hash.Check\Resources\app.ico" {
    param($g)
    Tx $g "#14B8A6" "#" 180 104 120 $true
    Check $g "#FFFFFF" 148 164 60 10
}

# PDF.Forge — PDF 문서 + 불꽃 (빨강)
Make-Icon "$Root\Applications\Files\Inspector\PDF.Forge\Resources\app.ico" {
    param($g)
    Frr $g "#991B1B" 36 28 148 200 12
    Frr $g "#1F0000" 50 48 120 160 8
    Tx  $g "#EF4444" "PDF" 90 88 128 $true
    Poly $g "#FB923C" @(@(164,28),@(184,68),@(196,28),@(188,52),@(180,28))
    Poly $g "#FEF08A" @(@(172,32),@(184,60),@(192,32),@(186,50),@(180,32))
}

# Zip.Peek — 압축 + 눈 (노랑)
Make-Icon "$Root\Applications\Files\Inspector\Zip.Peek\Resources\app.ico" {
    param($g)
    Frr $g "#CA8A04" 36 44 184 168 14
    Frr $g "#1A1000" 52 60 152 136 8
    Ln $g "#FDE047" 128 60 128 180 9
    for ($y = 72; $y -le 168; $y += 20) { Frr $g "#FDE047" 114 $y 16 12 4 }
    Ar $g "#FDE047" 128 148 40 200 140 8
    Ar $g "#FDE047" 128 148 40 20  140 8
    FillC $g "#FDE047" 128 136 14
    FillC $g "#1A1000" 128 136 7
}

# Mosaic.Forge — 타일 그리드 (보라)
Make-Icon "$Root\Applications\Media\Mosaic.Forge\Resources\app.ico" {
    param($g)
    $clrs = @("#8B5CF6","#A78BFA","#6D28D9","#7C3AED","#8B5CF6","#A78BFA","#7C3AED","#8B5CF6","#6D28D9")
    $i = 0
    for ($row = 0; $row -lt 3; $row++) {
        for ($col = 0; $col -lt 3; $col++) {
            Frr $g $clrs[$i] (28+$col*76) (28+$row*76) 68 68 10
            $i++
        }
    }
}

# Photo.Video.Organizer — 카메라 + 폴더 (핑크)
Make-Icon "$Root\Applications\Media\Photo.Video.Organizer\Resources\app.ico" {
    param($g)
    Frr $g "#EC4899" 32 80 192 136 16
    Frr $g "#4A0020" 48 96 160 104 10
    Frr $g "#EC4899" 92 60 72 36 10
    StrkC $g "#F9A8D4" 128 148 44 10
    FillC $g "#831843" 128 148 28
    FillC $g "#F9A8D4" 128 148 12
    Frr $g "#FBBF24" 156 84 28 20 6
}

# Api.Probe — { } + 신호 (청록)
Make-Icon "$Root\Applications\Tools\Dev\Network\Api.Probe\Resources\app.ico" {
    param($g)
    Tx $g "#06B6D4" "{}" 136 96 128 $true
    Ar $g "#67E8F9" 128 180 28 200 140 6
    Ar $g "#67E8F9" 128 180 44 200 140 5
    Ar $g "#67E8F9" 128 180 60 200 140 4
}

# Mock.Server — 서버 + 화살표 (파랑)
Make-Icon "$Root\Applications\Tools\Dev\Network\Mock.Server\Resources\app.ico" {
    param($g)
    Frr $g "#1E40AF" 40 36 176 52 10
    Srr $g "#93C5FD" 40 36 176 52 10 4
    FillC  $g "#60A5FA" 188 62 10
    Frr $g "#1E40AF" 40 104 176 52 10
    Srr $g "#93C5FD" 40 104 176 52 10 4
    FillC  $g "#60A5FA" 188 130 10
    Frr $g "#1E40AF" 40 172 176 52 10
    Srr $g "#93C5FD" 40 172 176 52 10 4
    FillC  $g "#60A5FA" 188 198 10
    Ln $g "#60A5FA" 64 148 120 148 7
    Poly $g "#60A5FA" @(@(56,136),@(44,148),@(56,160))
    Poly $g "#60A5FA" @(@(128,136),@(140,148),@(128,160))
}

# Serve.Cast — 안테나 방송 (청록)
Make-Icon "$Root\Applications\Tools\Dev\Network\Serve.Cast\Resources\app.ico" {
    param($g)
    Ln $g "#0D9488" 128 196 128 96 9
    Ar $g "#14B8A6" 128 120 56 220 100 7
    Ar $g "#14B8A6" 128 120 56 220 -100 7
    Ar $g "#2DD4BF" 128 108 88 220 100 6
    Ar $g "#2DD4BF" 128 108 88 220 -100 6
    Ar $g "#99F6E4" 128 96 116 220 100 5
    Ar $g "#99F6E4" 128 96 116 220 -100 5
    Frr $g "#0D9488" 112 192 32 40 6
    Frr $g "#0D9488" 88  228 80 16 8
    FillC  $g "#0D9488" 128 100 12
}

# Hex.Peek — 0x + 돋보기 (주황)
Make-Icon "$Root\Applications\Tools\Dev\Debug\Hex.Peek\Resources\app.ico" {
    param($g)
    Tx $g "#F97316" "0x" 120 88 108 $true
    StrkC $g "#FB923C" 152 164 40 9
    Ln $g "#FB923C" 183 195 216 228 12
}

# Log.Lens — 로그 라인 + 돋보기 (파랑)
Make-Icon "$Root\Applications\Tools\Dev\Debug\Log.Lens\Resources\app.ico" {
    param($g)
    Frr $g "#1E3A5F" 20 28 148 200 12
    Ln $g "#60A5FA" 36 72 152 72 7
    Ln $g "#60A5FA" 36 104 148 104 7
    Ln $g "#60A5FA" 36 136 128 136 7
    Ln $g "#60A5FA" 36 168 140 168 7
    StrkC $g "#3B82F6" 184 164 44 10
    Ln $g "#3B82F6" 218 198 240 220 12
}

# Signal.Flow — 사인파 (청록)
Make-Icon "$Root\Applications\Tools\Dev\Debug\Signal.Flow\Resources\app.ico" {
    param($g)
    Ln $g "#0E7490" 20 128 236 128 2
    $pts = [System.Drawing.PointF[]]( 0..48 | ForEach-Object {
        $x = [float](20 + $_ * 4.5)
        $y = [float](128 - [Math]::Sin($_ * [Math]::PI / 8) * 72)
        [System.Drawing.PointF]::new($x, $y)
    })
    $pen = [System.Drawing.Pen]::new((C "#06B6D4"), 9)
    $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
    $g.DrawCurve($pen, $pts)
    $pen.Dispose()
    FillC $g "#67E8F9" 20 128 11
    FillC $g "#67E8F9" 236 128 11
}

# Glyph.Map — 문자 그리드 (보라)
Make-Icon "$Root\Applications\Tools\Dev\Assets\Glyph.Map\Resources\app.ico" {
    param($g)
    $chars = @("A","B","C","D","E","F","G","H","I")
    $clrs  = @("#8B5CF6","#A78BFA","#7C3AED","#8B5CF6","#C4B5FD","#7C3AED","#A78BFA","#8B5CF6","#6D28D9")
    $i=0
    for ($row=0;$row-lt3;$row++) {
        for ($col=0;$col-lt3;$col++) {
            Frr $g "#2E1065" (16+$col*76) (16+$row*76) 68 68 8
            Tx $g $clrs[$i] $chars[$i] 44 (50+$col*76) (50+$row*76) $true
            $i++
        }
    }
}

# Icon.Hunt — 별 + 조준선 (핑크)
Make-Icon "$Root\Applications\Tools\Dev\Assets\Icon.Hunt\Resources\app.ico" {
    param($g)
    StrkC $g "#EC4899" 128 128 96 8
    StrkC $g "#EC4899" 128 128 60 5
    Ln $g "#EC4899" 24 128 68 128 6
    Ln $g "#EC4899" 188 128 232 128 6
    Ln $g "#EC4899" 128 24 128 68 6
    Ln $g "#EC4899" 128 188 128 232 6
    Sparkle $g "#F9A8D4" 128 128 44
}

# Key.Map — 키보드 키 (주황)
Make-Icon "$Root\Applications\Tools\Dev\Assets\Key.Map\Resources\app.ico" {
    param($g)
    Frr $g "#92400E" 28 52 200 152 16
    Frr $g "#F97316" 36 44 200 152 16
    Frr $g "#431407" 52 68 164 116 10
    for ($r=0;$r-lt3;$r++) {
        for ($c=0;$c-lt4;$c++) {
            Frr $g "#9A3412" (60+$c*44) (84+$r*36) 36 28 6
        }
    }
    Frr $g "#EA580C" 108 188 88 18 6
}

# Locale.Forge — 지구 + 망치 (초록)
Make-Icon "$Root\Applications\Tools\Dev\Assets\Locale.Forge\Resources\app.ico" {
    param($g)
    StrkC  $g "#10B981" 108 108 76 9
    Ar  $g "#10B981" 108 108 36 0 360 6
    Ln  $g "#10B981" 32  108 184 108 6
    Ln  $g "#10B981" 108 32  108 184 6
    Ar  $g "#6EE7B7" 108 108 76 270 180 5
    Frr $g "#34D399" 168 168 60 28 8
    Ln  $g "#10B981" 180 196 218 234 11
}

# Boot.Map — 전원 + 맵 노드 (파랑)
Make-Icon "$Root\Applications\Tools\Dev\Data\Boot.Map\Resources\app.ico" {
    param($g)
    StrkC $g "#3B82F6" 128 100 72 9
    Ar $g "#3B82F6" 128 100 72 230 260 12
    Ln $g "#3B82F6" 128 28 128 100 11
    FillC $g "#60A5FA" 68  168 18
    FillC $g "#60A5FA" 128 200 18
    FillC $g "#60A5FA" 188 168 18
    Ln $g "#93C5FD" 68 168 128 200 5
    Ln $g "#93C5FD" 128 200 188 168 5
    Ln $g "#93C5FD" 68 168 188 168 5
}

# Quick.Calc — 계산기 (초록)
Make-Icon "$Root\Applications\Tools\Dev\Data\Quick.Calc\Resources\app.ico" {
    param($g)
    Frr $g "#15803D" 44 28 168 200 16
    Frr $g "#052E16" 60 44 136 60 8
    Tx $g "#BBF7D0" "=" 72 128 160 $true
    $bclrs = @("#22C55E","#22C55E","#22C55E","#16A34A","#22C55E","#22C55E","#22C55E","#15803D","#22C55E","#22C55E","#22C55E","#4ADE80")
    $i=0
    for ($r=0;$r-lt3;$r++) {
        for ($c=0;$c-lt4;$c++) {
            Frr $g $bclrs[$i] (60+$c*34) (122+$r*34) 28 28 6
            $i++
        }
    }
}

# Table.Craft — 테이블 그리드 (주황)
Make-Icon "$Root\Applications\Tools\Dev\Data\Table.Craft\Resources\app.ico" {
    param($g)
    Frr $g "#9A3412" 24 28 208 208 12
    Frr $g "#EA580C" 24 28 208 48 12
    for ($c=1;$c-lt3;$c++) { Ln $g "#C2410C" (24+$c*69) 28 (24+$c*69) 236 5 }
    for ($r=1;$r-lt3;$r++) { Ln $g "#C2410C" 24 (76+$r*60) 232 (76+$r*60) 5 }
}

# Layout.Forge — 레이아웃 + 망치 (보라)
Make-Icon "$Root\Applications\Tools\Dev\System\Layout.Forge\Resources\app.ico" {
    param($g)
    Srr $g "#7C3AED" 20 20 216 180 12 7
    Ln  $g "#7C3AED" 20 76 236 76 7
    Ln  $g "#7C3AED" 116 76 116 200 7
    Frr $g "#A78BFA" 140 196 80 32 8
    Ln  $g "#7C3AED" 152 226 188 256 12
}

# Sched.Cast — 시계 + 신호 (파랑)
Make-Icon "$Root\Applications\Tools\Dev\System\Sched.Cast\Resources\app.ico" {
    param($g)
    StrkC $g "#2563EB" 100 100 72 9
    Ln $g "#3B82F6" 100 100 100 52 8
    Ln $g "#3B82F6" 100 100 136 116 7
    FillC $g "#3B82F6" 100 100 10
    Ar $g "#60A5FA" 182 168 28 220 100 6
    Ar $g "#60A5FA" 182 168 28 220 -100 6
    Ar $g "#93C5FD" 182 160 48 220 100 5
    Ar $g "#93C5FD" 182 160 48 220 -100 5
    FillC $g "#2563EB" 182 168 10
}

# DNS.Flip — 지구 + 회전화살표 (청록)
Make-Icon "$Root\Applications\Tools\Network\DNS.Flip\Resources\app.ico" {
    param($g)
    StrkC  $g "#0D9488" 128 128 88 9
    Ar  $g "#0D9488" 128 128 44 0 360 6
    Ln  $g "#0D9488" 36  128 220 128 6
    Ln  $g "#0D9488" 128 36  128 220 6
    Ar $g "#2DD4BF" 128 68 36 30 300 9
    Poly $g "#2DD4BF" @(@(164,48),@(174,68),@(154,68))
    Ar $g "#2DD4BF" 128 188 36 210 300 9
    Poly $g "#2DD4BF" @(@(92,208),@(82,188),@(102,188))
}

# Net.Scan — 레이더 스윕 (청록)
Make-Icon "$Root\Applications\Tools\Network\Net.Scan\Resources\app.ico" {
    param($g)
    StrkC $g "#0E7490" 128 128 96 4
    StrkC $g "#0E7490" 128 128 64 4
    StrkC $g "#0E7490" 128 128 32 4
    Ln $g "#0E7490" 128 128 224 128 3
    Ln $g "#0E7490" 128 128 128 32 3
    $sweep=[System.Drawing.Drawing2D.GraphicsPath]::new()
    $sweep.AddPie(32, 32, 192, 192, -90, 80)
    $gb=[System.Drawing.Drawing2D.LinearGradientBrush]::new(
        [System.Drawing.Point]::new(128,128),
        [System.Drawing.Point]::new(200,50),
        (C "#06B6D4" 180),(C "#06B6D4" 0))
    $g.FillPath($gb, $sweep)
    $gb.Dispose(); $sweep.Dispose()
    FillC $g "#06B6D4" 128 128 10
    FillC $g "#67E8F9" 168 92 8
    FillC $g "#67E8F9" 192 148 7
}

# Port.Watch — 포트 + 눈 (초록)
Make-Icon "$Root\Applications\Tools\Network\Port.Watch\Resources\app.ico" {
    param($g)
    Frr $g "#166534" 40 44 176 108 14
    for ($p=0;$p-lt4;$p++) { Frr $g "#22C55E" (60+$p*40) 24 20 40 6 }
    Ar $g "#4ADE80" 128 192 52 200 140 8
    Ar $g "#4ADE80" 128 192 52 20  140 8
    FillC $g "#4ADE80" 128 176 18
    FillC $g "#166534" 128 176 9
}

# Code.Snap — 카메라 + 코드 (주황)
Make-Icon "$Root\Applications\Tools\Productivity\Capture\Code.Snap\Resources\app.ico" {
    param($g)
    Frr $g "#C2410C" 20 68 216 148 16
    Frr $g "#431407" 36 88 184 108 10
    Frr $g "#C2410C" 80 48 96 36 10
    StrkC  $g "#FB923C" 128 142 40 9
    FillC  $g "#431407" 128 142 26
    Tx  $g "#FB923C" "</>" 68 100 142 $true
}

# Screen.Recorder — 화면 + 녹화 버튼 (빨강)
Make-Icon "$Root\Applications\Tools\Productivity\Capture\Screen.Recorder\Resources\app.ico" {
    param($g)
    Frr $g "#7F1D1D" 20 36 216 160 12
    Frr $g "#0D0D0D" 36 52 184 128 8
    Frr $g "#7F1D1D" 96 196 64 24 8
    Frr $g "#7F1D1D" 72 216 112 14 7
    FillC  $g "#EF4444" 128 116 36
    FillC  $g "#FCA5A5" 128 116 18
}

# Echo.Text — T + 에코 파동 (파랑)
Make-Icon "$Root\Applications\Tools\Productivity\Text\Echo.Text\Resources\app.ico" {
    param($g)
    Tx $g "#3B82F6" "T" 128 96 128 $true
    Ar $g "#60A5FA" 128 200 32 200 140 6
    Ar $g "#93C5FD" 128 200 52 200 140 5
    Ar $g "#BFDBFE" 128 200 72 200 140 4
}

# Mark.View — # + 북마크 (보라)
Make-Icon "$Root\Applications\Tools\Productivity\Text\Mark.View\Resources\app.ico" {
    param($g)
    Tx $g "#8B5CF6" "#" 140 92 128 $true
    Poly $g "#A78BFA" @(@(168,32),@(208,32),@(208,132),@(188,112),@(168,132))
}

# Text.Forge — T + 망치 (청록)
Make-Icon "$Root\Applications\Tools\Productivity\Text\Text.Forge\Resources\app.ico" {
    param($g)
    Tx $g "#0D9488" "T" 140 92 128 $true
    Frr $g "#14B8A6" 148 188 76 30 8
    Ln  $g "#0D9488" 160 216 200 252 12
}

# Char.Art — 픽셀 A (핑크)
Make-Icon "$Root\Applications\Tools\Productivity\Visual\Char.Art\Resources\app.ico" {
    param($g)
    $pixels = @(
        @(0,0,0,1,1,0,0,0),
        @(0,0,1,1,1,1,0,0),
        @(0,1,1,0,0,1,1,0),
        @(0,1,1,0,0,1,1,0),
        @(0,1,1,1,1,1,1,0),
        @(0,1,1,0,0,1,1,0),
        @(0,1,1,0,0,1,1,0),
        @(0,0,0,0,0,0,0,0)
    )
    for ($r=0;$r-lt8;$r++) {
        for ($c=0;$c-lt8;$c++) {
            if ($pixels[$r][$c]) { Frr $g "#EC4899" (24+$c*28) (20+$r*28) 24 24 4 }
        }
    }
}

# Timeline.Craft — 타임라인 (주황)
Make-Icon "$Root\Applications\Tools\Productivity\Visual\Timeline.Craft\Resources\app.ico" {
    param($g)
    Ln $g "#C2410C" 24 128 232 128 7
    $pts  = @(48, 96, 144, 200)
    $clrs = @("#FB923C","#F97316","#EA580C","#FB923C")
    for ($i=0;$i-lt4;$i++) {
        FillC $g $clrs[$i] $pts[$i] 128 22
        FillC $g "#431407" $pts[$i] 128 11
    }
    Ln $g "#FED7AA" 48 80 48 148 5
    Ln $g "#FED7AA" 96 92 96 148 5
    Ln $g "#FED7AA" 144 72 144 148 5
    Ln $g "#FED7AA" 200 100 200 148 5
}

# Word.Cloud — 구름 (청록)
Make-Icon "$Root\Applications\Tools\Productivity\Visual\Word.Cloud\Resources\app.ico" {
    param($g)
    FillC  $g "#0E7490" 84  148 48
    FillC  $g "#0E7490" 128 128 60
    FillC  $g "#0E7490" 172 148 48
    FillC  $g "#0E7490" 108 164 50
    FillC  $g "#0E7490" 152 164 50
    Fr  $g "#0E7490" 84  148 88 36
    Tx $g "#CFFAFE" "Aa" 56 84 128 $false
    Tx $g "#67E8F9" "Bb" 40 136 152 $false
    Tx $g "#A5F3FC" "Cc" 50 108 176 $false
}

# Clipboard.Stacker — 스택 클립보드 (파랑)
Make-Icon "$Root\Applications\Tools\Productivity\Utility\Clipboard.Stacker\Resources\app.ico" {
    param($g)
    Frr $g "#1E40AF" 52 56 144 156 12 160
    Frr $g "#1E40AF" 44 48 144 156 12 200
    Frr $g "#1D4ED8" 36 40 144 156 12
    Frr $g "#1E3A5F" 48 56 120 128 8
    Frr $g "#1E40AF" 84 24 64 36 8
    Fr  $g "#1D4ED8" 96 36 40 28
    Ln $g "#60A5FA" 52 88 144 88 7
    Ln $g "#60A5FA" 52 112 132 112 7
    Ln $g "#60A5FA" 52 136 140 136 7
}

# Dict.Cast — 책 + 방송 (초록)
Make-Icon "$Root\Applications\Tools\Productivity\Utility\Dict.Cast\Resources\app.ico" {
    param($g)
    Frr $g "#166534" 20 32 128 192 12
    Frr $g "#052E16" 36 48 96 160 8
    Ln  $g "#4ADE80" 36 96 152 96 5
    Ln  $g "#4ADE80" 36 128 152 128 5
    Ln  $g "#4ADE80" 36 160 128 160 5
    Ln  $g "#22C55E" 148 32 148 224 8
    Ar $g "#4ADE80" 196 168 28 220 100 6
    Ar $g "#4ADE80" 196 168 28 220 -100 6
    Ar $g "#86EFAC" 196 160 48 220 100 5
    Ar $g "#86EFAC" 196 160 48 220 -100 5
    FillC $g "#166534" 196 168 10
}

# Mouse.Flick — 마우스 + 화살표 (보라)
Make-Icon "$Root\Applications\Tools\Productivity\Utility\Mouse.Flick\Resources\app.ico" {
    param($g)
    Frr $g "#7C3AED" 80 28 96 152 48
    Ln  $g "#A78BFA" 128 28 128 104 6
    Ln  $g "#A78BFA" 80 104 176 104 6
    Frr $g "#5B21B6" 112 56 32 52 6
    Ln  $g "#C4B5FD" 196 88 240 52 8
    Poly $g "#C4B5FD" @(@(240,52),@(220,60),@(232,76))
    Ln  $g "#C4B5FD" 196 168 240 204 8
    Poly $g "#C4B5FD" @(@(240,204),@(220,196),@(232,180))
}

# Prompt.Forge — > 커서 + 망치 (주황)
Make-Icon "$Root\Applications\Tools\Productivity\Utility\Prompt.Forge\Resources\app.ico" {
    param($g)
    Frr $g "#1C0A00" 20 20 216 168 12
    Tx  $g "#F97316" "> _" 116 88 128 $true
    Frr $g "#FB923C" 148 196 80 30 8
    Ln  $g "#F97316" 160 224 200 256 12
}

# QR.Forge — QR 코너패턴 (청록)
Make-Icon "$Root\Applications\Tools\Productivity\Utility\QR.Forge\Resources\app.ico" {
    param($g)
    foreach ($pos in @(@(20,20),@(148,20),@(20,148))) {
        Frr $g "#0D9488" $pos[0] $pos[1] 88 88 8
        Frr $g "#0A0A14" ($pos[0]+12) ($pos[1]+12) 64 64 4
        Frr $g "#0D9488" ($pos[0]+20) ($pos[1]+20) 48 48 4
    }
    Frr $g "#2DD4BF" 152 152 22 22 3
    Frr $g "#2DD4BF" 182 182 22 22 3
    Frr $g "#2DD4BF" 152 212 22 22 3
    Frr $g "#2DD4BF" 212 152 22 22 3
    Frr $g "#14B8A6" 190 30 50 22 7
    Ln  $g "#0D9488" 200 50 228 86 10
}

# Tag.Forge — 태그 + 망치 (핑크)
Make-Icon "$Root\Applications\Tools\Productivity\Media\Tag.Forge\Resources\app.ico" {
    param($g)
    $tagPath = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $tagPath.AddLines([System.Drawing.PointF[]]@(
        [System.Drawing.PointF]::new(28,  52),
        [System.Drawing.PointF]::new(28,  156),
        [System.Drawing.PointF]::new(148, 156),
        [System.Drawing.PointF]::new(212, 104),
        [System.Drawing.PointF]::new(148, 52)
    ))
    $tagPath.CloseFigure()
    $b=[System.Drawing.SolidBrush]::new((C "#BE185D")); $g.FillPath($b,$tagPath); $b.Dispose()
    $tagPath.Dispose()
    FillC $g "#FBCFE8" 68 104 18
    FillC $g "#BE185D" 68 104 9
    Ln $g "#FCE7F3" 96 96 160 96 6
    Ln $g "#FCE7F3" 96 112 148 112 6
    Frr $g "#EC4899" 168 168 68 28 8
    Ln  $g "#BE185D" 180 194 220 234 12
}

# Color.Grade — 색상 휠 (다색)
Make-Icon "$Root\Applications\Tools\Productivity\Creative\Color.Grade\Resources\app.ico" {
    param($g)
    $secClrs = @("#EF4444","#F97316","#EAB308","#22C55E","#06B6D4","#3B82F6","#8B5CF6","#EC4899")
    for ($i=0;$i-lt8;$i++) {
        $path=[System.Drawing.Drawing2D.GraphicsPath]::new()
        $path.AddPie(28,28,200,200, ($i*45-11), 48)
        $b=[System.Drawing.SolidBrush]::new((C $secClrs[$i])); $g.FillPath($b,$path)
        $b.Dispose(); $path.Dispose()
    }
    FillC $g "#0F0F1A" 128 128 44
    FillC $g "#FFFFFF" 128 128 22 180
}

# App.Temp — 온도계 (파랑)
Make-Icon "$Root\Applications\Tools\System\App.Temp\Resources\app.ico" {
    param($g)
    Frr $g "#1E40AF" 104 28 48 148 24
    Frr $g "#0F172A" 112 36 32 124 20
    FillC  $g "#3B82F6" 128 196 44
    Frr $g "#60A5FA" 116 120 24 100 12
    FillC  $g "#93C5FD" 128 196 30
    Ln $g "#BFDBFE" 140 80  156 80  5
    Ln $g "#BFDBFE" 140 108 156 108 5
    Ln $g "#BFDBFE" 140 136 152 136 5
    Ln $g "#BFDBFE" 140 60  160 60  5
}

# Burn.Rate — 불꽃 (빨강/주황)
Make-Icon "$Root\Applications\Tools\System\Burn.Rate\Resources\app.ico" {
    param($g)
    Poly $g "#991B1B" @(@(128,20),@(168,100),@(208,60),@(196,140),@(220,120),@(212,196),@(44,196),@(36,120),@(60,140),@(48,60),@(88,100))
    Poly $g "#EF4444" @(@(128,48),@(160,112),@(192,80),@(180,148),@(200,132),@(196,196),@(60,196),@(56,132),@(76,148),@(64,80),@(96,112))
    Poly $g "#F97316" @(@(128,76),@(152,128),@(172,108),@(168,156),@(184,144),@(180,196),@(76,196),@(72,144),@(88,156),@(84,108),@(104,128))
    Poly $g "#FEF08A" @(@(128,104),@(144,148),@(156,136),@(160,196),@(96,196),@(100,136),@(112,148))
    FillC $g "#FFFFFF" 128 168 18 160
}

# Env.Guard — 방패 + $ (초록)
Make-Icon "$Root\Applications\Tools\System\Env.Guard\Resources\app.ico" {
    param($g)
    $shield=[System.Drawing.Drawing2D.GraphicsPath]::new()
    $shield.AddLines([System.Drawing.PointF[]]@(
        [System.Drawing.PointF]::new(128, 228),
        [System.Drawing.PointF]::new(32,  160),
        [System.Drawing.PointF]::new(32,  52),
        [System.Drawing.PointF]::new(128, 28),
        [System.Drawing.PointF]::new(224, 52),
        [System.Drawing.PointF]::new(224, 160)
    ))
    $shield.CloseFigure()
    $b=[System.Drawing.SolidBrush]::new((C "#166534")); $g.FillPath($b,$shield); $b.Dispose()
    $shield.Dispose()
    Tx $g "#4ADE80" "`$" 120 108 152 $true
}

# Spec.Report — CPU + 바차트 (청록)
Make-Icon "$Root\Applications\Tools\System\Spec.Report\Resources\app.ico" {
    param($g)
    Frr $g "#164E63" 44 36 128 128 12
    Srr $g "#06B6D4" 44 36 128 128 12 6
    for ($i=0;$i-lt3;$i++) {
        Ln $g "#06B6D4" (68+$i*28) 28 (68+$i*28) 44 5
        Ln $g "#06B6D4" (68+$i*28) 156 (68+$i*28) 172 5
        Ln $g "#06B6D4" 36 (60+$i*28) 52 (60+$i*28) 5
        Ln $g "#06B6D4" 164 (60+$i*28) 180 (60+$i*28) 5
    }
    Tx $g "#67E8F9" "CPU" 64 64 100 $true
    $hs = @(72, 56, 92, 44)
    for ($i=0;$i-lt4;$i++) { Frr $g "#06B6D4" (188+$i*17) (228-$hs[$i]) 12 $hs[$i] 3 }
    Ln $g "#0E7490" 184 228 240 228 4
}

# Sys.Clean — 빗자루 (파랑)
Make-Icon "$Root\Applications\Tools\System\Sys.Clean\Resources\app.ico" {
    param($g)
    Ln $g "#1D4ED8" 68 24 188 168 14
    $bristleBase = @(96,116,136,156,176)
    for ($i=0;$i-lt5;$i++) {
        Ln $g "#3B82F6" ($bristleBase[$i]) 188 ($bristleBase[$i]-40) 232 9
        Ln $g "#60A5FA" ($bristleBase[$i]+8) 188 ($bristleBase[$i]-24) 232 7
    }
    Frr $g "#1E3A8A" 72 168 132 28 8
}

# Tray.Stats — 트레이바 + 바차트 (보라)
Make-Icon "$Root\Applications\Tools\System\Tray.Stats\Resources\app.ico" {
    param($g)
    Frr $g "#4C1D95" 16 192 224 44 10
    $hs = @(80, 120, 56, 100, 140, 72)
    for ($i=0;$i-lt6;$i++) {
        Frr $g "#A78BFA" (24+$i*36) (192-$hs[$i]) 28 $hs[$i] 4
    }
    Ln $g "#7C3AED" 16 192 240 192 5
    for ($j=0;$j-lt3;$j++) { FillC $g "#C4B5FD" (164+$j*28) 212 10 }
}

# Dungeon.Dash — 검 (보라)
Make-Icon "$Root\Games\Action\Dungeon.Dash\Resources\app.ico" {
    param($g)
    $bladePath=[System.Drawing.Drawing2D.GraphicsPath]::new()
    $bladePath.AddLines([System.Drawing.PointF[]]@(
        [System.Drawing.PointF]::new(60,  196),
        [System.Drawing.PointF]::new(50,  186),
        [System.Drawing.PointF]::new(168, 52),
        [System.Drawing.PointF]::new(186, 50),
        [System.Drawing.PointF]::new(184, 68),
        [System.Drawing.PointF]::new(70,  196)
    ))
    $bladePath.CloseFigure()
    $b=[System.Drawing.SolidBrush]::new((C "#C4B5FD")); $g.FillPath($b,$bladePath); $b.Dispose()
    $bladePath.Dispose()
    Frr $g "#7C3AED" 48 176 96 24 6
    Frr $g "#7C3AED" 80 164 24 48 6
    Frr $g "#4C1D95" 86 210 20 40 6
    Sparkle $g "#DDD6FE" 48 44 24
}

# Hook.Cast — 낚시 바늘 (파랑)
Make-Icon "$Root\Games\Casual\Hook.Cast\Resources\app.ico" {
    param($g)
    Ln $g "#93C5FD" 128 20 128 84 6
    Ar $g "#3B82F6" 128 152 68 270 190 12
    Ln $g "#3B82F6" 60  152 60 84 12
    Ln $g "#60A5FA" 60 220 88 200 8
    FillC $g "#FDE68A" 128 84 16
    FillC $g "#F59E0B" 128 84 9
    Ar $g "#60A5FA" 128 220 36 0 180 6
    Ar $g "#93C5FD" 128 220 52 0 180 5
}

# Brick.Blitz — 벽돌 패턴 (주황)
Make-Icon "$Root\Games\Arcade\Brick.Blitz\Resources\app.ico" {
    param($g)
    $rowH  = 40
    $brickW= 88
    for ($r=0;$r-lt5;$r++) {
        $y = 28+$r*$rowH
        $startX = if ($r % 2 -eq 0) { 20 } else { -24 }
        $clr = if ($r % 2 -eq 0) { "#EA580C" } else { "#C2410C" }
        for ($c=0;$c-lt4;$c++) {
            $x=$startX+$c*($brickW+8)
            if ($x+$brickW-20 -ge 20 -and $x -le 236) {
                $rx=[Math]::Max(20,$x)
                $rw=[Math]::Min($brickW, $x+$brickW-$rx)
                Frr $g $clr $rx $y $rw ($rowH-6) 7
            }
        }
    }
    Sparkle $g "#FED7AA" 210 36 24
}

# Dash.City — 도시 스카이라인 (청록)
Make-Icon "$Root\Games\Arcade\Dash.City\Resources\app.ico" {
    param($g)
    $buildings = @(@(20,80,44,156),@(72,108,44,128),@(124,52,44,184),@(176,92,44,144),@(216,68,20,168))
    foreach ($bd in $buildings) { Frr $g "#0E7490" $bd[0] $bd[1] $bd[2] $bd[3] 4 }
    $wclr = "#67E8F9"
    Frr $g $wclr 28  100 12 12 2; Frr $g $wclr 44  100 12 12 2
    Frr $g $wclr 132 72  12 12 2; Frr $g $wclr 148 72  12 12 2
    Frr $g $wclr 132 96  12 12 2; Frr $g $wclr 148 96  12 12 2
    Frr $g $wclr 184 108 12 12 2; Frr $g $wclr 200 108 12 12 2
    FillC $g "#FDE68A" 196 36 24
    FillC $g "#0A1628" 204 28 20
    Ln $g "#06B6D4" 20 236 236 236 6
}

# Neon.Run — 달리기 (라임)
Make-Icon "$Root\Games\Arcade\Neon.Run\Resources\app.ico" {
    param($g)
    FillC $g "#84CC16" 128 56 28
    Ln $g "#84CC16" 128 84 128 160 10
    Ln $g "#84CC16" 128 108 72 88 9
    Ln $g "#84CC16" 128 108 184 128 9
    Ln $g "#84CC16" 128 160 88 216 9
    Ln $g "#84CC16" 128 160 168 200 9
    Ln $g "#BEF264" 20 80  64 80  6
    Ln $g "#BEF264" 16 108 52 108 5
    Ln $g "#D9F99D" 20 136 48 136 4
}

# Neon.Slice — 검 슬래시 (청록)
Make-Icon "$Root\Games\Arcade\Neon.Slice\Resources\app.ico" {
    param($g)
    Ln $g "#67E8F9" 32 196 220 36 20 120
    Ln $g "#06B6D4" 32 196 220 36 16
    Ln $g "#CFFAFE" 32 196 220 36 6
    Sparkle $g "#CFFAFE" 56 172 20
    Sparkle $g "#CFFAFE" 200 52 16
    Sparkle $g "#A5F3FC" 128 116 12
}

# Orbit.Craft — 행성 + 궤도 (보라)
Make-Icon "$Root\Games\Puzzle\Orbit.Craft\Resources\app.ico" {
    param($g)
    $ot=[System.Drawing.Pen]::new((C "#7C3AED"),6); $g.DrawEllipse($ot, 20,72, 216, 112); $ot.Dispose()
    $ot2=[System.Drawing.Pen]::new((C "#4C1D95"),6); $g.DrawEllipse($ot2, 72,20, 112, 216); $ot2.Dispose()
    FillC $g "#8B5CF6" 128 128 44
    FillC $g "#6D28D9" 128 128 30
    FillC $g "#A78BFA" 116 116 14
    FillC $g "#C4B5FD" 196 80 16
}

# Gravity.Flip — 화살표 뒤집기 (주황)
Make-Icon "$Root\Games\Puzzle\Gravity.Flip\Resources\app.ico" {
    param($g)
    Poly $g "#F97316" @(@(128,28),@(172,80),@(148,80),@(148,128),@(108,128),@(108,80),@(84,80))
    Poly $g "#EA580C" @(@(128,228),@(84,176),@(108,176),@(108,128),@(148,128),@(148,176),@(172,176))
    Ln $g "#FED7AA" 56 128 200 128 5
}

# Hue.Flow — 색상 그라디언트 원 (다색)
Make-Icon "$Root\Games\Puzzle\Hue.Flow\Resources\app.ico" {
    param($g)
    $colors = @("#EF4444","#F97316","#EAB308","#22C55E","#06B6D4","#3B82F6","#8B5CF6","#EC4899")
    for ($i=0;$i-lt8;$i++) {
        $startAngle = $i * 45 - 22.5
        $p=[System.Drawing.Drawing2D.GraphicsPath]::new()
        $p.AddPie(20,20,216,216,$startAngle,52)
        $b=[System.Drawing.SolidBrush]::new((C $colors[$i])); $g.FillPath($b,$p)
        $b.Dispose(); $p.Dispose()
    }
    FillC $g "#0A0A18" 128 128 52
    Ar $g "#FFFFFF" 128 128 28 0 300 7
    Poly $g "#FFFFFF" @(@(128,100),@(112,90),@(140,86))
}

# Sand.Fall — 모래시계 (노랑)
Make-Icon "$Root\Games\Sandbox\Sand.Fall\Resources\app.ico" {
    param($g)
    $hg=[System.Drawing.Drawing2D.GraphicsPath]::new()
    $hg.AddLines([System.Drawing.PointF[]]@(
        [System.Drawing.PointF]::new(48,  28),
        [System.Drawing.PointF]::new(208, 28),
        [System.Drawing.PointF]::new(148, 128),
        [System.Drawing.PointF]::new(208, 228),
        [System.Drawing.PointF]::new(48,  228),
        [System.Drawing.PointF]::new(108, 128)
    ))
    $hg.CloseFigure()
    $b=[System.Drawing.SolidBrush]::new((C "#92400E")); $g.FillPath($b,$hg); $b.Dispose()
    $hg.Dispose()
    $top=[System.Drawing.Drawing2D.GraphicsPath]::new()
    $top.AddLines([System.Drawing.PointF[]]@(
        [System.Drawing.PointF]::new(56,  36),
        [System.Drawing.PointF]::new(200, 36),
        [System.Drawing.PointF]::new(148, 120),
        [System.Drawing.PointF]::new(108, 120)
    ))
    $top.CloseFigure()
    $b=[System.Drawing.SolidBrush]::new((C "#EAB308")); $g.FillPath($b,$top); $b.Dispose()
    $top.Dispose()
    $bot=[System.Drawing.Drawing2D.GraphicsPath]::new()
    $bot.AddLines([System.Drawing.PointF[]]@(
        [System.Drawing.PointF]::new(108, 136),
        [System.Drawing.PointF]::new(148, 136),
        [System.Drawing.PointF]::new(196, 220),
        [System.Drawing.PointF]::new(60,  220)
    ))
    $bot.CloseFigure()
    $b=[System.Drawing.SolidBrush]::new((C "#FDE047")); $g.FillPath($b,$bot); $b.Dispose()
    $bot.Dispose()
    FillC $g "#FEF08A" 128 128 6
    FillC $g "#FEF08A" 128 140 5
    FillC $g "#FEF08A" 128 152 4
}

# Leaf.Grow — 새싹 (초록)
Make-Icon "$Root\Games\Simulation\Leaf.Grow\Resources\app.ico" {
    param($g)
    Ln $g "#15803D" 128 220 128 100 10
    $leaf1=[System.Drawing.Drawing2D.GraphicsPath]::new()
    $leaf1.AddBeziers([System.Drawing.PointF[]]@(
        [System.Drawing.PointF]::new(128,164),
        [System.Drawing.PointF]::new(72, 120),
        [System.Drawing.PointF]::new(48, 80),
        [System.Drawing.PointF]::new(80, 60),
        [System.Drawing.PointF]::new(116,100),
        [System.Drawing.PointF]::new(128,164),
        [System.Drawing.PointF]::new(128,164)
    ))
    $b=[System.Drawing.SolidBrush]::new((C "#22C55E")); $g.FillPath($b,$leaf1); $b.Dispose()
    $leaf1.Dispose()
    $leaf2=[System.Drawing.Drawing2D.GraphicsPath]::new()
    $leaf2.AddBeziers([System.Drawing.PointF[]]@(
        [System.Drawing.PointF]::new(128,120),
        [System.Drawing.PointF]::new(184,76),
        [System.Drawing.PointF]::new(216,40),
        [System.Drawing.PointF]::new(180,28),
        [System.Drawing.PointF]::new(140,68),
        [System.Drawing.PointF]::new(128,120),
        [System.Drawing.PointF]::new(128,120)
    ))
    $b=[System.Drawing.SolidBrush]::new((C "#4ADE80")); $g.FillPath($b,$leaf2); $b.Dispose()
    $leaf2.Dispose()
    FillC $g "#86EFAC" 128 96 16
    FillC $g "#22C55E" 128 96 9
}

# Nitro.Drift — 자동차 + 속도선 (주황)
Make-Icon "$Root\Games\Racing\Nitro.Drift\Resources\app.ico" {
    param($g)
    Ln $g "#FED7AA" 20 100 100 100 7 130
    Ln $g "#FED7AA" 20 128 96 128 6 100
    Ln $g "#FED7AA" 20 156 104 156 5 70
    Frr $g "#C2410C" 80 104 140 76 12
    Frr $g "#EA580C" 88 80 104 48 14
    Frr $g "#7DD3FC" 96 88 88 32 8
    FillC $g "#1C1917" 96  184 26
    FillC $g "#78716C" 96  184 16
    FillC $g "#1C1917" 184 184 26
    FillC $g "#78716C" 184 184 16
    Poly $g "#FBBF24" @(@(80,152),@(32,136),@(56,152),@(24,168),@(60,160))
}

# Beat.Drop — 음표 + 파형 (청록)
Make-Icon "$Root\Games\Rhythm\Beat.Drop\Resources\app.ico" {
    param($g)
    $noteHead=[System.Drawing.Drawing2D.GraphicsPath]::new()
    $noteHead.AddEllipse(80,136,72,52)
    $mat=[System.Drawing.Drawing2D.Matrix]::new()
    $mat.RotateAt(-20,[System.Drawing.PointF]::new(116,162))
    $noteHead.Transform($mat)
    $b=[System.Drawing.SolidBrush]::new((C "#0D9488")); $g.FillPath($b,$noteHead); $b.Dispose()
    $noteHead.Dispose(); $mat.Dispose()
    Ln $g "#0D9488" 152 166 152 60 10
    $fl=[System.Drawing.Drawing2D.GraphicsPath]::new()
    $fl.AddBeziers([System.Drawing.PointF[]]@(
        [System.Drawing.PointF]::new(152,60),
        [System.Drawing.PointF]::new(200,68),
        [System.Drawing.PointF]::new(196,96),
        [System.Drawing.PointF]::new(152,100)
    ))
    $p2=[System.Drawing.Pen]::new((C "#14B8A6"),9); $g.DrawPath($p2,$fl); $p2.Dispose()
    $fl.Dispose()
    Ln    $g "#2DD4BF" 72 160 72 216 8
    Poly  $g "#2DD4BF" @(@(56,208),@(72,228),@(88,208))
    Ar $g "#99F6E4" 196 140 24 0 360 5
    Ar $g "#99F6E4" 196 140 38 0 360 4
}

# Chord.Strike — 기타 (핑크)
Make-Icon "$Root\Games\Rhythm\Chord.Strike\Resources\app.ico" {
    param($g)
    FillC $g "#9D174D" 128 168 60
    FillC $g "#BE185D" 128 168 48
    FillC $g "#9D174D" 128 100 48
    FillC $g "#BE185D" 128 100 38
    Fr $g "#BE185D" 102 100 52 68
    FillC $g "#4A0020" 128 168 22
    StrkC $g "#F9A8D4" 128 168 22 4
    Frr $g "#831843" 116 28 24 96 8
    for ($i=0;$i-lt6;$i++) { Ln $g "#FCE7F3" (112+$i*6) 28 (112+$i*6) 220 2 }
    Ln $g "#F472B6" 172 72 228 36 7 140
    Sparkle $g "#FBB6CE" 216 44 18
}

# Dodge.Blitz — 번개 (노랑)
Make-Icon "$Root\Games\Shooter\Dodge.Blitz\Resources\app.ico" {
    param($g)
    Poly $g "#CA8A04" @(@(144,20),@(76,128),@(116,128),@(72,236),@(180,108),@(136,108))
    Poly $g "#EAB308" @(@(140,28),@(80,124),@(120,124),@(80,228),@(172,112),@(128,112))
    Poly $g "#FEF08A" @(@(132,44),@(96,120),@(128,120),@(96,208),@(160,116),@(124,116))
}

# Star.Strike — 별 + 조준선 (청록)
Make-Icon "$Root\Games\Shooter\Star.Strike\Resources\app.ico" {
    param($g)
    $outerR = 88; $innerR = 36
    $starPts = [System.Drawing.PointF[]]@()
    for ($i=0;$i-lt5;$i++) {
        $oa = ($i * 72 - 90) * [Math]::PI / 180
        $ia = ($i * 72 - 90 + 36) * [Math]::PI / 180
        $starPts += [System.Drawing.PointF]::new(128 + $outerR*[Math]::Cos($oa), 118 + $outerR*[Math]::Sin($oa))
        $starPts += [System.Drawing.PointF]::new(128 + $innerR*[Math]::Cos($ia), 118 + $innerR*[Math]::Sin($ia))
    }
    $b=[System.Drawing.SolidBrush]::new((C "#0E7490")); $g.FillPolygon($b,$starPts); $b.Dispose()
    StrkC $g "#22D3EE" 128 118 108 6
    Ln $g "#22D3EE" 20 118 86 118 6
    Ln $g "#22D3EE" 170 118 236 118 6
    Ln $g "#22D3EE" 128 10 128 76 6
    Ln $g "#22D3EE" 128 160 128 226 6
}

# Tower.Guard — 성탑 (보라)
Make-Icon "$Root\Games\Strategy\Tower.Guard\Resources\app.ico" {
    param($g)
    Frr $g "#4C1D95" 44 80 168 160 8
    Srr $g "#7C3AED" 44 80 168 160 8 5
    for ($i=0;$i-lt4;$i++) { Frr $g "#7C3AED" (44+$i*44) 44 36 48 6 }
    $dpath=[System.Drawing.Drawing2D.GraphicsPath]::new()
    $dpath.AddArc(96,132,64,64,180,180)
    $dpath.AddLine(160,164,160,240)
    $dpath.AddLine(160,240,96,240)
    $dpath.AddLine(96,240,96,164)
    $dpath.CloseFigure()
    $b=[System.Drawing.SolidBrush]::new((C "#2E1065")); $g.FillPath($b,$dpath); $b.Dispose()
    $dpath.Dispose()
    Ar $g "#A78BFA" 128 116 24 180 180 5
    Frr $g "#A78BFA" 108 116 40 32 4
    Ln $g "#C4B5FD" 128 44 128 20 6
    Poly $g "#8B5CF6" @(@(128,20),@(160,32),@(128,44))
}

# ════════════════════════════════════════════════════════════════════════════
Write-Host ""
Write-Host "======================================" -ForegroundColor Cyan
Write-Host " 완료: $script:_ok 성공 / $script:_fail 실패" -ForegroundColor $(if ($script:_fail -eq 0) { "Green" } else { "Yellow" })
Write-Host "======================================" -ForegroundColor Cyan
Write-Host ""


