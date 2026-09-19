@echo off
chcp 65001 >nul
setlocal enabledelayedexpansion

:: ======================================================================
::  DUPES - 펭귄 중복 파일 찾기 
::  (Applications\Files\Manager\File.Duplicates 의 cmd 판)
::
::  폴더 안에서 내용이 똑같은 파일과 비슷한 사진을 찾아 정리한다  
::   · 중복: 크기 → 앞부분 64 KB 해시 → 전체 SHA-256 (0 바이트 파일 제외)
::   · 유사 이미지: dHash 9x8 · Hamming 거리 10 이하 (jpg png bmp gif tif) 
::   · 남길 파일: 가장 오래된 · 가장 최근 · 경로가 가장 짧은 · 먼저 찾은 파일 중 선택  
::   · 정리: 휴지통으로 · 다른 폴더로 이동 · CSV 보고서 
::   · 원본 앱과 다른 점: 부분 해시 단계 추가 · 최소 크기 · 남길 파일 기준 · 보고서 
::                       절약량은 남길 파일을 뺀 실제 값 · 유사 이미지는 따로 골라야 정리  
::   · 사용법: dupes.cmd [폴더 ...]   (폴더 여러 개를 이 파일 위에 끌어다 놓아도 된다) 
::
::  처리 엔진은 파일 끝 ::PS_ENGINE_BEGIN 아래 PowerShell 코드 (ASCII 전용) 
::
::  콘솔 테마: .claude/docs/cmd-console-style.md (마스코트: 펭귄) 
::  주의: 멀티바이트가 든 줄은 멀티바이트로 끝나지 않게, 짝수 바이트로 끝낸다  
::  주의: 지연 확장 상태이므로 문구에 느낌표를 쓰지 않는다 
:: ======================================================================

set "DP_SELF=%~f0"
title Duplicate Finder

:: 끌어다 놓은 폴더 여러 개를 ; 로 잇는다 (shift 는 %%0 까지 밀므로 DP_SELF 를 먼저 저장) 
set "DP_ARG="
:ARG_LOOP
if "%~1"=="" if [%1]==[] goto :ARG_DONE
if defined DP_ARG (set "DP_ARG=!DP_ARG!;%~1") else (set "DP_ARG=%~1")
shift
goto :ARG_LOOP
:ARG_DONE
set "DP_ENGINE=%TEMP%\dupes.engine.ps1"
set "DP_GROUPS=%TEMP%\dupes.groups.tsv"
set "DP_VIEW=%TEMP%\dupes.view.txt"
set "DP_SUM=%TEMP%\dupes.summary.txt"
set "DP_L_KEEP=남김"
set "DP_L_DEL=정리" 
set "DP_L_DUP=중복" 
set "DP_L_SIM=유사 사진"
set "DP_L_FILES=개"
set "DP_L_SAVE=정리하면"
set "DP_L_DIST=거리"
set "DP_L_MORE=나머지 그룹은 보고서에서 볼 수 있어요" 
set "DP_L_COLLECT=파일 모으는 중" 
set "DP_L_HASH=내용 비교 중" 
set "DP_L_IMG=사진 비교 중"
set "DP_L_WORK=정리 중"
set "DP_L_CSV=그룹,종류,처리,경로,크기(바이트),수정한 날짜" 

:: ----------------------------------------------------------------------
:: 콘솔 테마 (G1~G6 배너: 얼음 하늘색 → 남색)
:: ----------------------------------------------------------------------
for /f %%E in ('echo prompt $E^| cmd') do set "ESC=%%E"
set "C0=%ESC%[0m"
set "CW=%ESC%[97m"
set "CD=%ESC%[90m"
set "CC=%ESC%[96m"
set "CB=%ESC%[38;5;75m"
set "CG=%ESC%[92m"
set "CY=%ESC%[93m"
set "CR=%ESC%[91m"
set "CP=%ESC%[38;5;117m"
set "G1=%ESC%[38;5;195m"
set "G2=%ESC%[38;5;159m"
set "G3=%ESC%[38;5;123m"
set "G4=%ESC%[38;5;117m"
set "G5=%ESC%[38;5;111m"
set "G6=%ESC%[38;5;105m"
set "RULE=%CD%──────────────────────────────────────────────────────────────────%C0%" 
set "CARD=──────────────────────────────────────────────" 
set "CARD_END=──────────────────────────────────────────────────────────────" 
set "STEP_TOTAL=4"

:: 펭귄 
set "PG_1=  .--."
set "PG_2= ( •ө• )"
set "PG_3= /)___(\ "
set "PG_HAPPY=(^ө^)" 
set "PG_SAD=(;ө;)" 
set "PG_WOW=(°ө°)" 

:: ======================================================================
:: [1] 폴더 
:: ======================================================================
:START
cls
call :BANNER
call :STEP 1 "찾을 폴더"
echo   %CD%   여러 폴더는 ; 로 구분 (예: D:\사진;E:\백업) · 하위 폴더 포함%C0%
:ASK_DIR
set "DP_DIRS="
if defined DP_ARG (
    set "DP_DIRS=!DP_ARG!"
    set "DP_ARG="
    set "MSG=    !CD!└!C0! 폴더  !CW!!DP_DIRS!!C0!  !CD!(끌어다 놓은 폴더)!C0!" & echo !MSG! 
) else (
    set /p "DP_DIRS=!C0!    !CD!└!C0! 폴더  !CY!›!C0! " 
)
if not defined DP_DIRS goto :ASK_DIR
set "DP_DIRS=!DP_DIRS:"=!"
set "_bad="
for %%p in ("%DP_DIRS:;=" "%") do if not "%%~p"=="" if not exist "%%~p\*" set "_bad=%%~p"
if not defined _bad goto :ASK_OPT
echo   !CR!✗!C0! 폴더를 찾을 수 없습니다  !CD!!_bad!!C0!
goto :ASK_DIR

:: ======================================================================
:: [2] 옵션 
:: ======================================================================
:ASK_OPT
call :STEP 2 "찾는 방식"
echo   %CC%■%C0% %CW%최소 크기%C0%
echo     %CD%└%C0% %CY%[1]%C0% 모든 파일   %CY%[2]%C0% 100 KB 이상   %CY%[3]%C0% 1 MB 이상  
choice /C 123 /N /M "   선택 > "
if errorlevel 3 (set "DP_MIN=1048576") else if errorlevel 2 (set "DP_MIN=102400") else (set "DP_MIN=1")
echo.
echo   %CC%■%C0% %CW%비슷한 사진도 찾기%C0%  %CD%크기 · 화질만 다른 같은 장면 (dHash)%C0% 
echo     %CD%└%C0% %CY%[1]%C0% 찾기   %CY%[2]%C0% 안 찾기   %CD%사진이 많으면 오래 걸려요%C0% 
choice /C 12 /N /M "   선택 > " 
set /a "DP_IMG=2 - %errorlevel%"
echo.
echo   %CC%■%C0% %CW%어떤 파일을 남길까요%C0%  %CD%그룹마다 하나를 남기고 나머지가 정리 대상%C0% 
echo     %CD%└%C0% %CY%[1]%C0% 가장 오래된 파일   %CD%원본일 가능성이 높음 (권장)%C0% 
echo     %CD%└%C0% %CY%[2]%C0% 가장 최근 파일 
echo     %CD%└%C0% %CY%[3]%C0% 경로가 가장 짧은 파일   %CD%정리된 폴더 쪽%C0%
echo     %CD%└%C0% %CY%[4]%C0% 먼저 찾은 파일   %CD%입력한 폴더 순서 (원본 앱 방식)%C0% 
choice /C 1234 /N /M "   선택 > " 
set "DP_KEEP=%errorlevel%"

:: ======================================================================
:: [3] 스캔 
:: ======================================================================
call :STEP 3 "찾는 중" 
set "DP_MODE=scan"
call :RUN_ENGINE
if not exist "%DP_SUM%" goto :ENGINE_FAIL
for %%v in (FILES GROUPS DUPFILES WASTE SIMGROUPS SIMFILES IMAGES SECS) do set "S_%%v=0"
for /f "usebackq tokens=1,* delims==" %%a in ("%DP_SUM%") do set "S_%%a=%%b"

:: ======================================================================
:: [4] 결과 
:: ======================================================================
:RESULT
call :STEP 4 "결과" 
echo.
echo   %CC%╭─%C0% 🐧 %CW%찾은 결과%C0% %CC%%CARD%%C0% 
set "MSG=  !CC!│!C0!  !CD!살펴본 파일!C0!    !S_FILES!개  !CD!(사진 !S_IMAGES!개 · !S_SECS!초)!C0!" & echo !MSG! 
set "MSG=  !CC!│!C0!  !CD!내용이 같은 파일!C0!  !CW!!S_GROUPS!!C0!그룹 · !S_DUPFILES!개   !CD!정리하면!C0! !CY!!S_WASTE!!C0!" & echo !MSG! 
set "MSG=  !CC!│!C0!  !CD!비슷한 사진!C0!    !CW!!S_SIMGROUPS!!C0!그룹 · !S_SIMFILES!개   !CD!자동 정리에서는 빠짐!C0!" & echo !MSG!
echo   %CC%╰%CARD_END%%C0%
if "!S_GROUPS!"=="0" if "!S_SIMGROUPS!"=="0" goto :NOTHING
echo.
type "%DP_VIEW%"

:ACTION
echo.
echo   %RULE%
echo.
echo   %CC%■%C0% %CW%정리%C0%  %CD%✗ 표시된 파일이 대상 · ✓ 표시는 남김%C0% 
echo     %CD%└%C0% %CY%[1]%C0% 휴지통으로 보내기   %CD%되돌릴 수 있음 (권장)%C0%
echo     %CD%└%C0% %CY%[2]%C0% 다른 폴더로 옮기기 
echo     %CD%└%C0% %CY%[3]%C0% 보고서만 저장   %CD%CSV%C0%
echo     %CD%└%C0% %CY%[0]%C0% 끝내기  
choice /C 1230 /N /M "   선택 > " 
if errorlevel 4 goto :QUIT
if errorlevel 3 goto :REPORT
if errorlevel 2 goto :ASK_MOVE
set "DP_MODE=recycle"
goto :SCOPE

:ASK_MOVE
set "DP_MOVE_TO="
set /p "DP_MOVE_TO=!C0!    !CD!└!C0! 옮길 폴더  !CY!›!C0! " 
if not defined DP_MOVE_TO goto :ACTION
set "DP_MOVE_TO=!DP_MOVE_TO:"=!"
if not exist "!DP_MOVE_TO!\*" mkdir "!DP_MOVE_TO!" >nul 2>&1
if not exist "!DP_MOVE_TO!\*" (
    echo   !CR!✗!C0! 폴더를 만들 수 없습니다  
    goto :ASK_MOVE
)
set "DP_MODE=move"

:SCOPE
set "DP_SIM=0"
if "!S_SIMGROUPS!"=="0" goto :CONFIRM
echo.
echo   %CC%■%C0% %CW%범위%C0% 
echo     %CD%└%C0% %CY%[1]%C0% 내용이 같은 파일만   %CD%권장%C0%
echo     %CD%└%C0% %CY%[2]%C0% 비슷한 사진까지     %CD%목록을 꼭 확인하세요%C0% 
choice /C 12 /N /M "   선택 > " 
set /a "DP_SIM=%errorlevel% - 1"

:CONFIRM
set "DP_COUNT_ONLY=1"
set "_mode=%DP_MODE%"
set "DP_MODE=count"
call :RUN_ENGINE
set "DP_MODE=%_mode%"
set "DP_COUNT_ONLY="
set "C_N=0"
set "C_SIZE=0"
for /f "usebackq tokens=1,* delims==" %%a in ("%DP_SUM%") do set "C_%%a=%%b"
echo.
set "_what=휴지통으로 보냅니다"
if "!DP_MODE!"=="move" set "_what=!DP_MOVE_TO! 로 옮깁니다"
set "MSG=  !CY!▲!C0! 파일 !CW!!C_N!!C0!개 !CD!(!C_SIZE!)!C0! 를 !_what!" & echo !MSG! 
choice /C 10 /N /M "   [1] 진행   [0] 취소 > "
if errorlevel 2 goto :ACTION
echo.
call :RUN_ENGINE
for %%v in (DONE FAIL SIZE) do set "R_%%v=0"
for /f "usebackq tokens=1,* delims==" %%a in ("%DP_SUM%") do set "R_%%a=%%b"
echo.
echo   %CC%╭─%C0% 🐧 %CW%정리 결과%C0% %CC%%CARD%%C0% 
set "MSG=  !CC!│!C0!  !CG!✓!C0! 처리한 파일   !CW!!R_DONE!!C0!개  !CD!(!R_SIZE!)!C0!" & echo !MSG!
set "_fc=!CG!"
if not "!R_FAIL!"=="0" set "_fc=!CR!"
set "MSG=  !CC!│!C0!  !_fc!✗!C0! 실패          !R_FAIL!개" & echo !MSG!
echo   %CC%╰%CARD_END%%C0%
echo.
echo    %CP%!PG_1!%C0%
if "!R_FAIL!"=="0" (
    set "MSG=   !CP!!PG_HAPPY!!C0!   !CG!✓ DONE!C0! ✨   !CD!뒤뚱뒤뚱, 똑같이 생긴 친구들을 정리했어요!C0!"
) else (
    set "MSG=   !CP!!PG_SAD!!C0!   !CY!▲ CHECK!C0!   !CD!몇 개는 사용 중이라 못 옮겼어요!C0!"
)
echo !MSG!
echo    %CP%!PG_3!%C0%
goto :END_MENU

:REPORT
set "DP_REPORT=%USERPROFILE%\Desktop"
if not exist "!DP_REPORT!\" set "DP_REPORT=%USERPROFILE%"
for /f %%T in ('powershell -NoProfile -Command "Get-Date -Format yyyyMMdd_HHmm"') do set "_stamp=%%T"
set "DP_REPORT=!DP_REPORT!\Duplicates_!_stamp!.csv"
set "DP_MODE=report"
call :RUN_ENGINE
echo.
echo   %CG%✓%C0% 보고서를 저장했어요 
set "MSG=    !CD!└!C0! !DP_REPORT!" & echo !MSG!
goto :ACTION

:NOTHING
echo.
echo    %CP%!PG_1!%C0%
echo    %CP%!PG_WOW!%C0%   %CG%✓ CLEAN%C0%   %CD%똑같은 파일이 하나도 없어요, 깔끔하네요%C0% 
echo    %CP%!PG_3!%C0%

:END_MENU
echo.
echo   %RULE%
echo.
choice /C 10 /N /M "   [1] 처음으로   [0] 종료 > "
if errorlevel 2 goto :QUIT
goto :START

:ENGINE_FAIL
echo   %CR%✗%C0% 처리 엔진을 실행하지 못했어요 %CD%(PowerShell)%C0%
goto :QUIT

:QUIT
echo.
echo   %CD%  "펭귄은 다 똑같아 보여도 파일은 달라야죠"%C0% !PG_HAPPY!
echo.
endlocal
exit /b 0

:: ======================================================================
:: 서브루틴 
:: ======================================================================
:BANNER
echo.
echo   %G1%██████╗ ██╗   ██╗██████╗ ███████╗███████╗%C0%
echo   %G2%██╔══██╗██║   ██║██╔══██╗██╔════╝██╔════╝%C0%     %CP%!PG_1!%C0% 
echo   %G3%██║  ██║██║   ██║██████╔╝█████╗  ███████╗%C0%    %CP%!PG_2!%C0%   %CD%waddle waddle, spot the twins~%C0% 
echo   %G4%██║  ██║██║   ██║██╔═══╝ ██╔══╝  ╚════██║%C0%    %CP%!PG_3!%C0%
echo   %G5%██████╔╝╚██████╔╝██║     ███████╗███████║%C0%
echo   %G6%╚═════╝  ╚═════╝ ╚═╝     ╚══════╝╚══════╝%C0%
echo   %CD%   펭귄 중복 파일 찾기  ·  같은 내용 · 비슷한 사진 🐧%C0% 
echo.
echo   %CC%■%C0% %date% %time:~0,8% 
echo   %RULE%
goto :eof

:STEP
set "_DOTS="
for /l %%i in (1,1,%STEP_TOTAL%) do if %%i leq %~1 (set "_DOTS=!_DOTS!●") else (set "_DOTS=!_DOTS!○") 
echo.
echo   !CC!■!C0! !CB!STEP %~1/%STEP_TOTAL%!C0!  !CC!!_DOTS!!C0!  !CW!%~2!C0!
echo   !RULE!
goto :eof

:RUN_ENGINE
if exist "%DP_SUM%" del /f /q "%DP_SUM%" >nul 2>&1
powershell -NoProfile -ExecutionPolicy Bypass -Command "$s = [IO.File]::ReadAllText($env:DP_SELF, [Text.Encoding]::UTF8); $m = [Environment]::NewLine + '::PS_ENGINE_BEGIN'; [IO.File]::WriteAllText($env:DP_ENGINE, $s.Substring($s.IndexOf($m) + $m.Length)); & $env:DP_ENGINE"
goto :eof

:: ======================================================================
:: PowerShell 처리 엔진 (cmd 는 여기까지 오지 않는다 - ASCII 전용 코드) 
::  scan    : DP_DIRS 를 훑어 DP_GROUPS(TSV) · DP_VIEW(화면용) · DP_SUM(요약) 
::  count   : 정리 대상 개수 · 크기  
::  recycle : 정리 대상을 휴지통으로          move : DP_MOVE_TO 로 이동  
::  report  : DP_REPORT 에 CSV 
:: ======================================================================
::PS_ENGINE_BEGIN
$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = New-Object System.Text.UTF8Encoding $false
$e = [char]27; $C0 = "$e[0m"; $CD = "$e[90m"; $CW = "$e[97m"; $CC = "$e[96m"; $CG = "$e[92m"; $CR = "$e[91m"; $CY = "$e[93m"; $CP = "$e[38;5;117m"
$utf8 = New-Object System.Text.UTF8Encoding $false
$inv = [Globalization.CultureInfo]::InvariantCulture

function Fmt([double]$b) {
    if ($b -ge 1GB) { return ($b / 1GB).ToString('0.0', $inv) + ' GB' }
    if ($b -ge 1MB) { return ($b / 1MB).ToString('0.0', $inv) + ' MB' }
    if ($b -ge 1KB) { return ($b / 1KB).ToString('0', $inv) + ' KB' }
    return ([long]$b).ToString() + ' B'
}
function Progress([string]$label, [int]$i, [int]$n) {
    $w = 24; $on = if ($n -gt 0) { [int][Math]::Floor($i * $w / $n) } else { $w }
    [Console]::Write("`r$e[2K    $CP" + ([string][char]0x25B0) * $on + $CD + ([string][char]0x25B1) * ($w - $on) + "$C0  $label  $i / $n")
}
function Write-Kv([string[]]$lines) { [IO.File]::WriteAllLines($env:DP_SUM, $lines, $utf8) }

# ------------------------------------------------------------------ scan
if ($env:DP_MODE -eq 'scan') {
    $sw = [Diagnostics.Stopwatch]::StartNew()
    Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System; using System.IO; using System.Drawing; using System.Drawing.Drawing2D; using System.Drawing.Imaging;
public static class DHash {
    public static ulong Compute(string path) {
        using (var fs = File.OpenRead(path))
        using (var img = Image.FromStream(fs, false, false))
        using (var bmp = new Bitmap(9, 8, PixelFormat.Format24bppRgb)) {
            using (var g = Graphics.FromImage(bmp)) { g.InterpolationMode = InterpolationMode.Bilinear; g.DrawImage(img, 0, 0, 9, 8); }
            ulong h = 0;
            for (int y = 0; y < 8; y++) for (int x = 0; x < 8; x++) {
                Color l = bmp.GetPixel(x, y), r = bmp.GetPixel(x + 1, y);
                int lb = (l.R * 299 + l.G * 587 + l.B * 114) / 1000, rb = (r.R * 299 + r.G * 587 + r.B * 114) / 1000;
                if (lb > rb) h |= 1UL << (y * 8 + x);
            }
            return h;
        }
    }
    static int Pop(ulong v) { int c = 0; while (v != 0) { v &= v - 1; c++; } return c; }
    public static int Dist(ulong a, ulong b) { return Pop(a ^ b); }
    static int Find(int[] p, int i) { while (p[i] != i) { p[i] = p[p[i]]; i = p[i]; } return i; }
    public static int[] Group(ulong[] h, int threshold) {
        int n = h.Length; var p = new int[n];
        for (int i = 0; i < n; i++) p[i] = i;
        for (int i = 0; i < n; i++) for (int j = i + 1; j < n; j++)
            if (Pop(h[i] ^ h[j]) <= threshold) { int a = Find(p, i), b = Find(p, j); if (a != b) p[b] = a; }
        for (int i = 0; i < n; i++) p[i] = Find(p, i);
        return p;
    }
}
'@
    $roots = @(([string]$env:DP_DIRS).Split(';') | Where-Object { $_.Trim() } | ForEach-Object { [IO.Path]::GetFullPath($_.Trim()).TrimEnd('\') })
    $min = [long]$env:DP_MIN

    # 1) collect (order = folder order, then enumeration order)
    $files = New-Object 'System.Collections.Generic.List[object]'; $seen = @{}; $order = 0
    foreach ($root in $roots) {
        $stack = New-Object 'System.Collections.Generic.Stack[string]'; $stack.Push($root)
        while ($stack.Count -gt 0) {
            $dir = $stack.Pop()
            try { foreach ($f in [IO.Directory]::GetFiles($dir)) {
                $k = $f.ToLowerInvariant(); if ($seen.ContainsKey($k)) { continue }; $seen[$k] = 1
                $fi = New-Object IO.FileInfo $f
                if ($fi.Length -lt $min -or $fi.Length -eq 0) { continue }
                $files.Add([pscustomobject]@{ Path = $f; Size = $fi.Length; Time = $fi.LastWriteTime; Order = $order++; Root = $root })
                if ($order % 200 -eq 0) { [Console]::Write("`r$e[2K    $CD$($env:DP_L_COLLECT)  $order$C0") }
            } } catch { }
            try { $subs = [IO.Directory]::GetDirectories($dir); [Array]::Reverse($subs); foreach ($s in $subs) {
                if (([IO.File]::GetAttributes($s) -band [IO.FileAttributes]::ReparsePoint) -eq 0) { $stack.Push($s) }
            } } catch { }
        }
    }

    function Sha([string]$p, [long]$limit) {
        $h = [Security.Cryptography.SHA256]::Create(); $s = [IO.File]::OpenRead($p)
        try {
            if ($limit -gt 0) { $buf = New-Object byte[] ([Math]::Min($limit, $s.Length)); $n = $s.Read($buf, 0, $buf.Length); return [BitConverter]::ToString($h.ComputeHash($buf, 0, $n)) }
            return [BitConverter]::ToString($h.ComputeHash($s))
        } finally { $s.Dispose(); $h.Dispose() }
    }

    # 2) size -> partial hash -> full hash
    $cands = @($files | Group-Object Size | Where-Object { $_.Count -gt 1 } | ForEach-Object { $_.Group })
    $groups = New-Object 'System.Collections.Generic.List[object]'
    $i = 0; $n = $cands.Count
    foreach ($sg in @($cands | Group-Object Size)) {
        $part = @{}
        foreach ($f in $sg.Group) {
            $i++; Progress $env:DP_L_HASH $i $n
            try { $k = Sha $f.Path 65536; if (-not $part.ContainsKey($k)) { $part[$k] = New-Object 'System.Collections.Generic.List[object]' }; $part[$k].Add($f) } catch { }
        }
        foreach ($pg in $part.Values) {
            if ($pg.Count -lt 2) { continue }
            $full = @{}
            foreach ($f in $pg) {
                try { $k = if ($f.Size -le 65536) { 'P' } else { Sha $f.Path 0 }; if (-not $full.ContainsKey($k)) { $full[$k] = New-Object 'System.Collections.Generic.List[object]' }; $full[$k].Add($f) } catch { }
            }
            foreach ($fg in $full.Values) { if ($fg.Count -ge 2) { $groups.Add((New-Object PSObject -Property @{ Type = 'H'; Dist = 0; Files = [object[]]$fg.ToArray() })) } }
        }
    }

    # keep rule
    function Pick($list) {
        switch ($env:DP_KEEP) {
            '2' { return @($list | Sort-Object Time -Descending)[0] }
            '3' { return @($list | Sort-Object { $_.Path.Length }, Order)[0] }
            '4' { return @($list | Sort-Object Order)[0] }
            default { return @($list | Sort-Object Time, Order)[0] }
        }
    }
    foreach ($g in $groups) { $g | Add-Member -NotePropertyName Keep -NotePropertyValue (Pick $g.Files) }

    # 3) similar images (hash duplicates except the keeper are left out)
    $simCount = 0; $imgCount = 0
    if ($env:DP_IMG -eq '1') {
        $drop = @{}; foreach ($g in $groups) { foreach ($f in $g.Files) { if ($f -ne $g.Keep) { $drop[$f.Path] = 1 } } }
        $imgs = @($files | Where-Object { @('.jpg', '.jpeg', '.png', '.bmp', '.gif', '.tif', '.tiff') -contains [IO.Path]::GetExtension($_.Path).ToLowerInvariant() -and -not $drop.ContainsKey($_.Path) })
        $ok = New-Object 'System.Collections.Generic.List[object]'; $hs = New-Object 'System.Collections.Generic.List[UInt64]'
        $i = 0
        foreach ($f in $imgs) {
            $i++; if ($i % 3 -eq 1 -or $i -eq $imgs.Count) { Progress $env:DP_L_IMG $i $imgs.Count }
            try { $hs.Add([DHash]::Compute($f.Path)); $ok.Add($f) } catch { }
        }
        $imgCount = $ok.Count
        if ($ok.Count -gt 1) {
            $arr = $hs.ToArray(); $root = [DHash]::Group($arr, 10)
            $by = @{}
            for ($k = 0; $k -lt $arr.Length; $k++) { $r = $root[$k]; if (-not $by.ContainsKey($r)) { $by[$r] = New-Object 'System.Collections.Generic.List[int]' }; $by[$r].Add($k) }
            foreach ($members in $by.Values) {
                if ($members.Count -lt 2) { continue }
                $mx = 0; foreach ($a in $members) { foreach ($b in $members) { $dd = [DHash]::Dist($arr[$a], $arr[$b]); if ($dd -gt $mx) { $mx = $dd } } }
                $fl = @($members | ForEach-Object { $ok[$_] })
                $g = New-Object PSObject -Property @{ Type = 'S'; Dist = $mx; Files = [object[]]$fl }; $g | Add-Member -NotePropertyName Keep -NotePropertyValue (Pick $fl)
                $groups.Add($g); $simCount++
            }
        }
    }
    [Console]::Write("`r$e[2K")

    # save groups (sorted by reclaimable size)
    foreach ($g in $groups) { $g | Add-Member -NotePropertyName Waste -NotePropertyValue ([long](($g.Files | Where-Object { $_ -ne $g.Keep } | Measure-Object Size -Sum).Sum)) }
    $sorted = @($groups | Sort-Object @{ Expression = 'Type'; Descending = $false }, @{ Expression = 'Waste'; Descending = $true })
    $rows = New-Object 'System.Collections.Generic.List[string]'; $gi = 0
    foreach ($g in $sorted) {
        $gi++
        foreach ($f in $g.Files) { $rows.Add(($gi, $g.Type, $g.Dist, $(if ($f -eq $g.Keep) { 1 } else { 0 }), $f.Path, $f.Size, $f.Time.ToString('yyyy-MM-dd HH:mm')) -join "`t") }
    }
    [IO.File]::WriteAllLines($env:DP_GROUPS, $rows, $utf8)

    # screen view (top 10 duplicate groups + top 6 similar groups)
    $v = New-Object 'System.Collections.Generic.List[string]'
    function Short($f) { $p = $f.Path; if ($p.StartsWith($f.Root + '\', 'OrdinalIgnoreCase')) { $p = $p.Substring($f.Root.Length + 1) }; if ($p.Length -gt 52) { $p = [char]0x2026 + $p.Substring($p.Length - 51) }; $p }
    $shownH = 0; $shownS = 0; $hidden = 0; $gi = 0
    foreach ($g in $sorted) {
        $gi++
        if (($g.Type -eq 'H' -and $shownH -ge 10) -or ($g.Type -eq 'S' -and $shownS -ge 6)) { $hidden++; continue }
        if ($g.Type -eq 'H') { $shownH++; $title = "$CW$($env:DP_L_DUP) $gi$C0  ${CD}SHA-256$C0"; $icon = [char]::ConvertFromUtf32(0x1F4C4) }
        else { $shownS++; $title = "$CW$($env:DP_L_SIM) $gi$C0  $CD$($env:DP_L_DIST) $($g.Dist)$C0"; $icon = [char]::ConvertFromUtf32(0x1F5BC) }
        $v.Add("  $CC$([char]0x25A0)$C0 $title  $CD$([char]0x00B7)  $($g.Files.Count)$($env:DP_L_FILES)  $([char]0x00B7)  $($env:DP_L_SAVE)$C0 $CY$(Fmt $g.Waste)$C0")
        foreach ($f in @($g.Files | Sort-Object Order)) {
            $mark = if ($f -eq $g.Keep) { "$CG$([char]0x2713) $($env:DP_L_KEEP)$C0" } else { "$CR$([char]0x2717) $($env:DP_L_DEL)$C0" }
            $v.Add(("    {0}  {1,-53} {2,9}  {3}" -f $mark, (Short $f), (Fmt $f.Size), "$CD$($f.Time.ToString('yyyy-MM-dd'))$C0"))
        }
        $v.Add('')
    }
    if ($hidden -gt 0) { $v.Add("  $CD$([char]0x2026) +$hidden  $($env:DP_L_MORE)$C0") }
    [IO.File]::WriteAllLines($env:DP_VIEW, $v, $utf8)

    $h = @($groups | Where-Object { $_.Type -eq 'H' }); $s = @($groups | Where-Object { $_.Type -eq 'S' })
    Write-Kv @(
        "FILES=$($files.Count)", "IMAGES=$imgCount", "SECS=$([int]$sw.Elapsed.TotalSeconds)",
        "GROUPS=$($h.Count)", "DUPFILES=$((($h | ForEach-Object { $_.Files.Count }) | Measure-Object -Sum).Sum + 0)",
        "WASTE=$(Fmt ((($h | ForEach-Object { $_.Waste }) | Measure-Object -Sum).Sum + 0))",
        "SIMGROUPS=$simCount", "SIMFILES=$((($s | ForEach-Object { $_.Files.Count }) | Measure-Object -Sum).Sum + 0)"
    )
    exit 0
}

# ------------------------------------------------------------------ count / recycle / move / report
$rows = @([IO.File]::ReadAllLines($env:DP_GROUPS, $utf8) | ForEach-Object { $c = $_ -split "`t"; [pscustomobject]@{ G = $c[0]; Type = $c[1]; Keep = $c[3]; Path = $c[4]; Size = [long]$c[5]; Time = $c[6] } })

if ($env:DP_MODE -eq 'report') {
    $out = New-Object 'System.Collections.Generic.List[string]'; $out.Add($env:DP_L_CSV)
    foreach ($r in $rows) {
        $kind = if ($r.Type -eq 'H') { $env:DP_L_DUP } else { $env:DP_L_SIM }
        $act = if ($r.Keep -eq '1') { $env:DP_L_KEEP } else { $env:DP_L_DEL }
        $out.Add((($r.G, $kind, $act, ('"' + $r.Path.Replace('"', '""') + '"'), $r.Size, $r.Time) -join ','))
    }
    [IO.File]::WriteAllLines($env:DP_REPORT, $out, (New-Object System.Text.UTF8Encoding $true))
    Write-Kv @('OK=1'); exit 0
}

$targets = @($rows | Where-Object { $_.Keep -eq '0' -and ($_.Type -eq 'H' -or $env:DP_SIM -eq '1') })
$bytes = [long](($targets | Measure-Object Size -Sum).Sum + 0)
if ($env:DP_MODE -eq 'count') { Write-Kv @("N=$($targets.Count)", "SIZE=$(Fmt $bytes)"); exit 0 }

Add-Type -TypeDefinition @"
using System; using System.Runtime.InteropServices;
public static class Bin {
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct OP { public IntPtr hwnd; public uint wFunc; public string pFrom; public string pTo; public ushort fFlags; public bool aborted; public IntPtr map; public string title; }
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] static extern int SHFileOperation(ref OP op);
    // FO_DELETE + FOF_ALLOWUNDO | FOF_NOCONFIRMATION | FOF_SILENT | FOF_NOERRORUI : recycle bin, no dialogs at all
    public static int Recycle(string path) { var op = new OP { wFunc = 3, pFrom = path + "\0\0", fFlags = 0x0040 | 0x0010 | 0x0004 | 0x0400 }; return SHFileOperation(ref op); }
}
"@
$done = 0; $fail = 0; $moved = [long]0; $i = 0
foreach ($t in $targets) {
    $i++; Progress $env:DP_L_WORK $i $targets.Count
    try {
        if ($env:DP_MODE -eq 'recycle') {
            $rc = [Bin]::Recycle($t.Path); if ($rc -ne 0 -or (Test-Path -LiteralPath $t.Path)) { throw "recycle failed $rc" }
        } else {
            $dst = Join-Path $env:DP_MOVE_TO ([IO.Path]::GetFileName($t.Path))
            $b = [IO.Path]::GetFileNameWithoutExtension($t.Path); $x = [IO.Path]::GetExtension($t.Path); $k = 1
            while (Test-Path -LiteralPath $dst) { $dst = Join-Path $env:DP_MOVE_TO ("{0}_{1}{2}" -f $b, $k, $x); $k++ }
            [IO.File]::Move($t.Path, $dst)
        }
        $done++; $moved += $t.Size
    } catch { $fail++ }
}
[Console]::Write("`r$e[2K")
Write-Kv @("DONE=$done", "FAIL=$fail", "SIZE=$(Fmt $moved)")
