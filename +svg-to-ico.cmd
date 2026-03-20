@echo off
chcp 65001 >nul
setlocal enabledelayedexpansion

for /f %%a in ('echo prompt $E ^| cmd /q') do set "ESC=%%a"
set "C0=!ESC![0m"
set "C1=!ESC![96m"
set "C2=!ESC![97m"
set "C3=!ESC![93m"
set "C4=!ESC![92m"
set "C5=!ESC![91m"
set "DIM=!ESC![2m"

title SVG to ICO Converter

echo.
echo %C1%  ================================================%C0%
echo %C2%     SVG  ^>  ICO  %DIM%(16 / 32 / 48 / 256 px)%C0%
echo %C1%  ================================================%C0%
echo.

:: ── SVG 경로 입력 ────────────────────────────────────────────
:input_svg
set "SVG_PATH="
set /p "SVG_PATH=%C2%  SVG 파일 경로: %C0%"
if not defined SVG_PATH goto input_svg

:: 따옴표 제거
set "SVG_PATH=!SVG_PATH:"=!"

if not exist "!SVG_PATH!" (
    echo %C5%  오류: 파일을 찾을 수 없습니다. %C0%
    echo %DIM%         !SVG_PATH!%C0%
    echo.
    goto input_svg
)

:: 출력 경로 설정
for %%f in ("!SVG_PATH!") do (
    set "OUT_DIR=%%~dpf"
    set "BASE_NAME=%%~nf"
)
set "ICO_PATH=!OUT_DIR!!BASE_NAME!.ico"

:: ── 덮어쓰기 확인 ────────────────────────────────────────────
if exist "!ICO_PATH!" (
    echo.
    echo %C3%  이미 존재합니다: %C0%%DIM%!ICO_PATH!%C0%
    set "OW_ANS=Y"
    set /p "OW_ANS=%C3%  덮어쓰시겠습니까? (Y/N, 기본값 Y): %C0%"
    if /i "!OW_ANS!"=="N" (
        echo.
        echo %DIM%  취소되었습니다.%C0%
        echo.
        goto end
    )
)

echo.
echo %C1%  변환 중...%C0%
echo %DIM%  입력: !SVG_PATH!%C0%
echo %DIM%  출력: !ICO_PATH!%C0%
echo.

:: ── 임시 .NET 프로젝트 생성 ──────────────────────────────────
set "TMP_DIR=%TEMP%\svg2ico_%RANDOM%_%RANDOM%"
mkdir "!TMP_DIR!" 2>nul

:: csproj
(
echo ^<Project Sdk="Microsoft.NET.Sdk"^>
echo   ^<PropertyGroup^>
echo     ^<OutputType^>Exe^</OutputType^>
echo     ^<TargetFramework^>net10.0^</TargetFramework^>
echo     ^<Nullable^>enable^</Nullable^>
echo     ^<ImplicitUsings^>enable^</ImplicitUsings^>
echo   ^</PropertyGroup^>
echo   ^<ItemGroup^>
echo     ^<PackageReference Include="Svg.Skia" Version="2.0.0" /^>
echo   ^</ItemGroup^>
echo ^</Project^>
) > "!TMP_DIR!\Converter.csproj"

:: Program.cs
(
echo using Svg.Skia;
echo using SkiaSharp;
echo if ^(args.Length ^< 2^) ^{ Console.Error.WriteLine^("Usage: svg ico"^); return 1; ^}
echo string svgPath = args[0];
echo string icoPath = args[1];
echo int[] sizes = [16, 32, 48, 256];
echo var svg = new SKSvg^(^);
echo var picture = svg.Load^(svgPath^);
echo if ^(picture is null^) ^{ Console.Error.WriteLine^("SVG load failed: " + svgPath^); return 1; ^}
echo float srcW = picture.CullRect.Width;
echo float srcH = picture.CullRect.Height;
echo if ^(srcW ^<= 0 ^|^| srcH ^<= 0^) ^{ Console.Error.WriteLine^("Invalid SVG dimensions"^); return 1; ^}
echo var pngList = new List^<byte[]^>^(^);
echo foreach ^(int sz in sizes^)
echo ^{
echo     using var bmp = new SKBitmap^(sz, sz, SKColorType.Bgra8888, SKAlphaType.Premul^);
echo     using var canvas = new SKCanvas^(bmp^);
echo     canvas.Clear^(SKColors.Transparent^);
echo     canvas.Scale^(sz / srcW, sz / srcH^);
echo     canvas.DrawPicture^(picture^);
echo     canvas.Flush^(^);
echo     using var img = SKImage.FromBitmap^(bmp^);
echo     using var data = img.Encode^(SKEncodedImageFormat.Png, 100^);
echo     pngList.Add^(data.ToArray^(^)^);
echo     Console.WriteLine^($"  {sz}x{sz} OK"^);
echo ^}
echo using var ms = new MemoryStream^(^);
echo using var bw = new BinaryWriter^(ms^);
echo bw.Write^(^(ushort^)0^); bw.Write^(^(ushort^)1^); bw.Write^(^(ushort^)sizes.Length^);
echo int offset = 6 + sizes.Length * 16;
echo for ^(int i = 0; i ^< sizes.Length; i+^^+^)
echo ^{
echo     int sz = sizes[i];
echo     bw.Write^(^(byte^)^(sz ^>= 256 ? 0 : sz^)^); bw.Write^(^(byte^)^(sz ^>= 256 ? 0 : sz^)^);
echo     bw.Write^(^(byte^)0^); bw.Write^(^(byte^)0^);
echo     bw.Write^(^(ushort^)1^); bw.Write^(^(ushort^)32^);
echo     bw.Write^(^(uint^)pngList[i].Length^); bw.Write^(^(uint^)offset^);
echo     offset +^^= pngList[i].Length;
echo ^}
echo foreach ^(var png in pngList^) bw.Write^(png^);
echo bw.Flush^(^);
echo File.WriteAllBytes^(icoPath, ms.ToArray^(^)^);
echo Console.WriteLine^("OK"^);
echo return 0;
) > "!TMP_DIR!\Program.cs"

:: ── dotnet run ───────────────────────────────────────────────
dotnet run --project "!TMP_DIR!\Converter.csproj" --configuration Release -- "!SVG_PATH!" "!ICO_PATH!"
set "EXIT_CODE=!ERRORLEVEL!"

:: ── 임시 폴더 정리 ───────────────────────────────────────────
rd /s /q "!TMP_DIR!" 2>nul

:: ── 결과 ─────────────────────────────────────────────────────
echo.
if !EXIT_CODE!==0 (
    echo %C4%  변환 완료!%C0%
    echo %DIM%  !ICO_PATH!%C0%
) else (
    echo %C5%  변환 실패 (종료 코드: !EXIT_CODE!)%C0%
)

:end
echo.
pause
endlocal
