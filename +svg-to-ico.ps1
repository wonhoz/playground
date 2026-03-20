# SVG to ICO Converter
# Converts SVG to multi-size ICO (16/32/48/256px) using Svg.Skia + SkiaSharp via dotnet run

$ErrorActionPreference = 'Stop'

Write-Host ""
Write-Host "  ================================================" -ForegroundColor Cyan
Write-Host "     SVG  >  ICO   (16 / 32 / 48 / 256 px)" -ForegroundColor White
Write-Host "  ================================================" -ForegroundColor Cyan
Write-Host ""

# ── Input ────────────────────────────────────────────────────
:input_svg
$svgPath = (Read-Host "  SVG file path").Trim().Trim('"')

if (-not $svgPath) { goto input_svg }

if (-not (Test-Path $svgPath)) {
    Write-Host "  Error: file not found." -ForegroundColor Red
    Write-Host "         $svgPath" -ForegroundColor DarkGray
    Write-Host ""
    $svgPath = (Read-Host "  SVG file path").Trim().Trim('"')
}

while (-not (Test-Path $svgPath)) {
    Write-Host "  Error: file not found." -ForegroundColor Red
    Write-Host ""
    $svgPath = (Read-Host "  SVG file path").Trim().Trim('"')
    $svgPath = $svgPath.Trim('"')
}

$dir      = Split-Path $svgPath -Parent
$baseName = [IO.Path]::GetFileNameWithoutExtension($svgPath)
$icoPath  = Join-Path $dir "$baseName.ico"

# ── Overwrite check ──────────────────────────────────────────
if (Test-Path $icoPath) {
    Write-Host ""
    Write-Host "  Already exists: $icoPath" -ForegroundColor Yellow
    $ans = Read-Host "  Overwrite? (Y/N, default Y)"
    if ($ans -ieq 'N') {
        Write-Host ""
        Write-Host "  Cancelled." -ForegroundColor DarkGray
        Write-Host ""
        Read-Host "  Press Enter to exit"
        exit 0
    }
}

Write-Host ""
Write-Host "  Converting..." -ForegroundColor Cyan
Write-Host "  Input : $svgPath" -ForegroundColor DarkGray
Write-Host "  Output: $icoPath" -ForegroundColor DarkGray
Write-Host ""

# ── Build temp .NET project ──────────────────────────────────
$tmpDir = Join-Path $env:TEMP "svg2ico_$([System.IO.Path]::GetRandomFileName())"
New-Item -ItemType Directory -Path $tmpDir | Out-Null

$csproj = @'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Svg.Skia" Version="2.0.0" />
  </ItemGroup>
</Project>
'@

$program = @'
using Svg.Skia;
using SkiaSharp;

if (args.Length < 2) { Console.Error.WriteLine("Usage: svg ico"); return 1; }
string svgPath = args[0];
string icoPath = args[1];
int[] sizes = [16, 32, 48, 256];

var svg = new SKSvg();
var picture = svg.Load(svgPath);
if (picture is null) { Console.Error.WriteLine("SVG load failed: " + svgPath); return 1; }
float srcW = picture.CullRect.Width;
float srcH = picture.CullRect.Height;
if (srcW <= 0 || srcH <= 0) { Console.Error.WriteLine("Invalid SVG dimensions"); return 1; }

var pngList = new List<byte[]>();
foreach (int sz in sizes)
{
    using var bmp = new SKBitmap(sz, sz, SKColorType.Bgra8888, SKAlphaType.Premul);
    using var canvas = new SKCanvas(bmp);
    canvas.Clear(SKColors.Transparent);
    canvas.Scale(sz / srcW, sz / srcH);
    canvas.DrawPicture(picture);
    canvas.Flush();
    using var img = SKImage.FromBitmap(bmp);
    using var data = img.Encode(SKEncodedImageFormat.Png, 100);
    pngList.Add(data.ToArray());
    Console.WriteLine($"  {sz}x{sz} OK");
}

// ICO assembly: PNG-in-ICO (Vista+, lossless)
using var ms = new MemoryStream();
using var bw = new BinaryWriter(ms);
bw.Write((ushort)0); bw.Write((ushort)1); bw.Write((ushort)sizes.Length);
int offset = 6 + sizes.Length * 16;
for (int i = 0; i < sizes.Length; i++)
{
    int sz = sizes[i];
    bw.Write((byte)(sz >= 256 ? 0 : sz)); bw.Write((byte)(sz >= 256 ? 0 : sz));
    bw.Write((byte)0); bw.Write((byte)0);
    bw.Write((ushort)1); bw.Write((ushort)32);
    bw.Write((uint)pngList[i].Length); bw.Write((uint)offset);
    offset += pngList[i].Length;
}
foreach (var png in pngList) bw.Write(png);
bw.Flush();
File.WriteAllBytes(icoPath, ms.ToArray());
Console.WriteLine("OK");
return 0;
'@

Set-Content -Path (Join-Path $tmpDir "Converter.csproj") -Value $csproj -Encoding UTF8
Set-Content -Path (Join-Path $tmpDir "Program.cs")       -Value $program -Encoding UTF8

# ── Run ──────────────────────────────────────────────────────
try {
    & dotnet run --project (Join-Path $tmpDir "Converter.csproj") --configuration Release -- $svgPath $icoPath
    $exitCode = $LASTEXITCODE
}
catch {
    Write-Host "  dotnet run failed: $_" -ForegroundColor Red
    $exitCode = 1
}
finally {
    Remove-Item -Recurse -Force $tmpDir -ErrorAction SilentlyContinue
}

# ── Result ───────────────────────────────────────────────────
Write-Host ""
if ($exitCode -eq 0) {
    Write-Host "  Done!" -ForegroundColor Green
    Write-Host "  $icoPath" -ForegroundColor DarkGray
} else {
    Write-Host "  Failed (exit code: $exitCode)" -ForegroundColor Red
}

Write-Host ""
Read-Host "  Press Enter to exit"
