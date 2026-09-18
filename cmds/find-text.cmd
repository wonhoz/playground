@echo off
chcp 65001 >nul

:: ======================================================================
::  FIND TEXT - 폴더 하위 전체 파일 문자열 검색  
::
::  지정 폴더 아래 모든 텍스트 파일에서 문자열을 찾아  
::  파일 위치 · 줄 번호 · 내용을 색상과 함께 보여준다  
::   · 대화형:  find-text.cmd                 (더블 클릭) 
::   · 인자형:  find-text.cmd 검색어 [폴더] [확장자목록] [/c] [/r]
::              /c 대소문자 구분  ·  /r 정규식  
::              예) find-text.cmd TODO C:\src cs,xaml /c 
::   · 인코딩:  UTF-8 / UTF-8 BOM / UTF-16 / CP949 자동 판별, 바이너리 제외 
::   · 제외 폴더: .git .vs .idea bin obj node_modules packages __pycache__ 
::   · 종료 코드: 0 = 찾음, 1 = 없음, 2 = 입력 오류, 3 = 정규식 오류  
::
::  검색 엔진은 파일 끝 ::PS_ENGINE_BEGIN 아래 PowerShell 코드 (ASCII 전용) 
::  한글 문구는 환경 변수로 엔진에 넘긴다 (PS 5.1 인코딩 문제 회피)
::  엔진은 %TEMP% 의 .ps1 로 추출해 파일로 실행한다  
::   (iex · scriptblock 은 대량 루프에서 수십 배 느림 - 1.2만 파일 203초 vs 4초)
::
::  콘솔 테마: .claude/docs/cmd-console-style.md (ANSI 256 색, Windows 10+)
::  주의: 멀티바이트 문자가 있는 줄은 공백을 붙여 짝수 바이트로 끝낸다  
::  주의: 지연 확장 상태이므로 출력 문구에 느낌표를 쓰지 않는다  
:: ======================================================================

:: ---- 인자 해석 (검색어의 느낌표 보존을 위해 지연 확장 전에 처리) ----
:: shift 는 %0 까지 밀어내므로 자기 경로는 인자 해석 전에 저장한다 
set "FT_SELF=%~f0"
set "FT_TERM="
set "FT_ROOT="
set "FT_EXT="
set "FT_MODE=1"
set "FT_ARGS="
set "_pos=0"

:PARSE_ARGS
:: 빈 인자("")는 끝이 아니다 - 따옴표까지 비어 있을 때만 종료 
if "%~1"=="" if [%1]==[] goto :ARGS_DONE
set "FT_ARGS=1"
if /i "%~1"=="/c" set "FT_MODE=2" & shift & goto :PARSE_ARGS
if /i "%~1"=="/r" set "FT_MODE=3" & shift & goto :PARSE_ARGS
if "%~1"=="/?" set "FT_ARGS=" & goto :ARGS_DONE
set /a _pos+=1
if %_pos%==1 set "FT_TERM=%~1"
if %_pos%==2 set "FT_ROOT=%~1"
if %_pos%==3 set "FT_EXT=%~1"
shift
goto :PARSE_ARGS

:ARGS_DONE
:: 현재 폴더는 테마 변수 CD(회색)가 %CD% 를 가리기 전에 저장한다  
set "START_DIR=%CD%"
setlocal enabledelayedexpansion
title Find Text

:: ----------------------------------------------------------------------
:: 콘솔 테마 (G1~G6 배너 그라데이션: 하늘 → 남보라, 원본 테마 그대로)
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
set "CP=%ESC%[38;5;218m"
set "G1=%ESC%[38;5;123m"
set "G2=%ESC%[38;5;87m"
set "G3=%ESC%[38;5;81m"
set "G4=%ESC%[38;5;75m"
set "G5=%ESC%[38;5;69m"
set "G6=%ESC%[38;5;63m"
set "RULE=%CD%──────────────────────────────────────────────────────────────────%C0%" 
set "CARD=──────────────────────────────────────────────" 
set "CARD_END=──────────────────────────────────────────────────────────" 

:: 엔진 입출력 
set "FT_SUMMARY=%TEMP%\find-text.summary.txt"
set "FT_OUT=%TEMP%\find-text.result.txt"
set "FT_ENGINE=%TEMP%\find-text.engine.ps1"
set "FT_L_SCAN=검사 중"
set "FT_L_MORE=줄은 화면 출력 생략 (결과 파일에 전체 기록)"

:BANNER
cls
echo.
echo   %G1%███████╗██╗███╗   ██╗██████╗ %C0%
echo   %G2%██╔════╝██║████╗  ██║██╔══██╗%C0%     %CP%,_,%C0%
echo   %G3%█████╗  ██║██╔██╗ ██║██║  ██║%C0%    %CP%(O,O)%C0%   %CD%hoot hoot, where is it~%C0% 
echo   %G4%██╔══╝  ██║██║╚██╗██║██║  ██║%C0%    %CP%(   )%C0%🔍 
echo   %G5%██║     ██║██║ ╚████║██████╔╝%C0%    %CP%-"-"-%C0% 
echo   %G6%╚═╝     ╚═╝╚═╝  ╚═══╝╚═════╝ %C0%
echo   %CD%   폴더 전체 문자열 검색  ·  위치와 내용을 한눈에%C0% 
echo.
echo   %CC%■%C0% %date% %time:~0,8% 
echo   %RULE%

if "%~1"=="/?" goto :USAGE
if defined FT_ARGS goto :CHECK_ARGS

:: ======================================================================
:: [입력] 검색 조건 (대화형) 
:: ======================================================================
:ASK_ROOT
echo.
echo   %CC%■%C0% %CW%검색 조건%C0%
set "_in="
set /p "_in=!C0!    !CD!└!C0! 폴더      !CD!(Enter = !START_DIR!)!C0!  !CY!›!C0! "
if not defined _in set "_in=!START_DIR!"
set "FT_ROOT=!_in:"=!"
if exist "!FT_ROOT!\*" goto :ASK_TERM
echo   !CR!✗!C0! 폴더를 찾을 수 없습니다  !CD!!FT_ROOT!!C0! 
goto :ASK_ROOT

:ASK_TERM
set "FT_TERM="
set /p "FT_TERM=!C0!    !CD!└!C0! 검색어    !CY!›!C0! "
if not defined FT_TERM (
    echo   !CY!▲!C0! 검색어를 입력하세요 
    goto :ASK_TERM
)
set "FT_EXT="
set /p "FT_EXT=!C0!    !CD!└!C0! 확장자    !CD!(예: cs,xaml,md · Enter = 전체)!C0!  !CY!›!C0! "
echo.
echo   %CC%■%C0% %CW%검색 방식%C0%
echo     %CD%└%C0% %CY%[1]%C0% 일반          %CD%대소문자 무시%C0%
echo     %CD%└%C0% %CY%[2]%C0% 정확히        %CD%대소문자 구분%C0% 
echo     %CD%└%C0% %CY%[3]%C0% 정규식        %CD%.NET Regex, 대소문자 무시%C0% 
choice /C 123 /N /M "   선택 > "
set "FT_MODE=%errorlevel%"
goto :RUN

:: ======================================================================
:: [입력] 인자 모드 검증  
:: ======================================================================
:CHECK_ARGS
if not defined FT_TERM goto :USAGE
if not defined FT_ROOT set "FT_ROOT=!START_DIR!"
if exist "!FT_ROOT!\*" goto :RUN
echo.
echo   !CR!✗!C0! 폴더를 찾을 수 없습니다  !CD!!FT_ROOT!!C0! 
echo.
endlocal
exit /b 2

:: ======================================================================
:: 검색 실행  
:: ======================================================================
:RUN
if "!FT_ROOT:~-1!"=="\" if not "!FT_ROOT:~-2!"==":\" set "FT_ROOT=!FT_ROOT:~0,-1!"
set "MODE_TXT=일반 · 대소문자 무시"
if "!FT_MODE!"=="2" set "MODE_TXT=정확히 · 대소문자 구분" 
if "!FT_MODE!"=="3" set "MODE_TXT=정규식"
set "EXT_TXT=전체"
if defined FT_EXT set "EXT_TXT=!FT_EXT!"
if exist "!FT_SUMMARY!" del /f /q "!FT_SUMMARY!" >nul 2>&1

echo.
echo   %RULE%
echo.
echo   %CC%■%C0% %CB%SEARCH%C0%  %CW%!FT_TERM!%C0%
set "MSG=    !CD!└ 폴더!C0!    !FT_ROOT!" & echo !MSG!
set "MSG=    !CD!└ 확장자!C0!  !EXT_TXT!   !CD!·!C0!   !CD!방식!C0!  !MODE_TXT!" & echo !MSG! 
echo   %RULE%

powershell -NoProfile -ExecutionPolicy Bypass -Command "$s = [IO.File]::ReadAllText($env:FT_SELF, [Text.Encoding]::UTF8); $m = [Environment]::NewLine + '::PS_ENGINE_BEGIN'; [IO.File]::WriteAllText($env:FT_ENGINE, $s.Substring($s.IndexOf($m) + $m.Length)); & $env:FT_ENGINE; exit $LASTEXITCODE"
set "RC=%errorlevel%"

if "!RC!"=="3" goto :REGEX_FAIL
if not exist "!FT_SUMMARY!" goto :ENGINE_FAIL

set "N_SCAN=0"
set "N_FILES=0"
set "N_LINES=0"
set "N_HITS=0"
set "N_SEC=0"
set "N_CUT=0"
for /f "usebackq tokens=1-6" %%a in ("!FT_SUMMARY!") do (
    set "N_SCAN=%%a"
    set "N_FILES=%%b"
    set "N_LINES=%%c"
    set "N_HITS=%%d"
    set "N_SEC=%%e"
    set "N_CUT=%%f"
)

:: ======================================================================
:: 결과 요약  
:: ======================================================================
echo.
echo   %RULE%
echo.
echo   %CC%╭─%C0% %CW%검색 결과%C0% %CC%%CARD%%C0%
echo   %CC%│%C0%  %CD%검색어%C0%     %CW%!FT_TERM!%C0% 
echo   %CC%│%C0%  %CD%검사 파일%C0%  !N_SCAN!개  
echo   %CC%│%C0%  %CD%일치%C0%       %CY%!N_FILES!%C0%개 파일  %CD%·%C0%  %CY%!N_LINES!%C0%줄  %CD%·%C0%  %CY%!N_HITS!%C0%건 
echo   %CC%│%C0%  %CD%소요 시간%C0%  !N_SEC!초 
echo   %CC%│%C0%  %CD%결과 파일%C0%  %CD%!FT_OUT!%C0% 
echo   %CC%╰%CARD_END%%C0%
if not "!N_CUT!"=="0" echo   !CY!▲!C0! !N_CUT!!FT_L_MORE! 
echo.
if "!N_FILES!"=="0" goto :NOT_FOUND
echo      %CP%,_,%C0%
echo     %CP%(^^,^^)%C0%   %CG%✓ FOUND%C0% ✨   %CD%찾았어요, 위 경로를 Ctrl+클릭하면 파일이 열립니다%C0% 
echo     %CP%(   )%C0%
echo     %CP%-"-"-%C0%
set "RC=0"
goto :AFTER

:NOT_FOUND
echo      %CP%,_,%C0%
echo     %CP%(;,;)%C0%   %CY%▲ NOT FOUND%C0%   %CD%일치하는 내용이 없습니다%C0%
echo     %CP%(   )%C0%
echo     %CP%-"-"-%C0%
set "RC=1"

:AFTER
echo.
if defined FT_ARGS goto :END
echo   %RULE%
echo.
choice /C ROQ /N /M "   [R] 새 검색   [O] 결과 파일 열기   [Q] 종료 > "
if errorlevel 3 goto :END
if errorlevel 2 (
    start "" notepad "!FT_OUT!"
    goto :AFTER
)
goto :BANNER

:: ======================================================================
:: 오류 처리 / 사용법  
:: ======================================================================
:REGEX_FAIL
echo.
echo   !CR!✗!C0! 정규식이 올바르지 않습니다  !CD!!FT_TERM!!C0!
set "RC=3"
goto :AFTER

:ENGINE_FAIL
echo.
echo   !CR!✗!C0! 검색 엔진 실행에 실패했습니다 !CD!(PowerShell 종료 코드 !RC!)!C0! 
set "RC=2"
goto :AFTER

:USAGE
echo.
echo   %CC%■%C0% %CW%사용법%C0%
echo     %CD%└%C0% find-text.cmd                         %CD%대화형%C0%
echo     %CD%└%C0% find-text.cmd 검색어 [폴더] [확장자] [/c] [/r] 
echo.
echo   %CC%■%C0% %CW%옵션%C0% 
echo     %CD%└%C0% %CY%/c%C0%   대소문자 구분 
echo     %CD%└%C0% %CY%/r%C0%   정규식 %CD%(.NET Regex)%C0%
echo.
echo   %CC%■%C0% %CW%예시%C0% 
echo     %CD%└%C0% find-text.cmd TODO 
echo     %CD%└%C0% find-text.cmd "connection string" C:\src cs,config 
echo     %CD%└%C0% find-text.cmd "Log(Error|Warn)" . cs /r
echo.
endlocal
exit /b 2

:END
endlocal & exit /b %RC%

:: ======================================================================
:: PowerShell 검색 엔진 (cmd 는 여기까지 오지 않는다 - ASCII 전용 코드) 
::  입력: FT_ROOT FT_TERM FT_EXT FT_MODE FT_SUMMARY FT_OUT FT_L_SCAN
::  출력: 콘솔 (색상 + OSC 8 링크), FT_OUT (전체 결과), FT_SUMMARY (집계) 
:: ======================================================================
::PS_ENGINE_BEGIN
$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = New-Object System.Text.UTF8Encoding $false
$e = [char]27
$C0 = "$e[0m"; $CD = "$e[90m"; $CW = "$e[97m"; $CC = "$e[96m"; $CN = "$e[38;5;75m"
$HL = "$e[30;48;5;220m"
$bar = [string][char]0x2502
$dots = [string][char]0x2026
$icon = [char]::ConvertFromUtf32(0x1F4C4)
$maxShow = 1500
$maxWidth = 150
$skipDirs = @('.git', '.vs', '.idea', '.svn', 'bin', 'obj', 'node_modules', 'packages', '__pycache__')

$root = [IO.Path]::GetFullPath($env:FT_ROOT).TrimEnd('\')
$term = $env:FT_TERM
$mode = $env:FT_MODE
$exts = @()
if ($env:FT_EXT) {
    $exts = @($env:FT_EXT -split '[,; ]+' | Where-Object { $_ } |
        ForEach-Object { '.' + $_.Trim().TrimStart('*').TrimStart('.').ToLowerInvariant() })
}

$opt = [Text.RegularExpressions.RegexOptions]::CultureInvariant
if ($mode -ne '2') { $opt = $opt -bor [Text.RegularExpressions.RegexOptions]::IgnoreCase }
$pat = if ($mode -eq '3') { $term } else { [regex]::Escape($term) }
try { $rx = New-Object Text.RegularExpressions.Regex($pat, $opt) }
catch { exit 3 }

$sw = [Diagnostics.Stopwatch]::StartNew()

# 1) collect files (skip noisy folders and reparse points)
$files = New-Object 'System.Collections.Generic.List[string]'
$stack = New-Object 'System.Collections.Generic.Stack[string]'
$stack.Push($root)
while ($stack.Count -gt 0) {
    $dir = $stack.Pop()
    try {
        foreach ($s in [IO.Directory]::GetDirectories($dir)) {
            $name = [IO.Path]::GetFileName($s).ToLowerInvariant()
            if ($skipDirs -contains $name) { continue }
            if (([IO.File]::GetAttributes($s) -band [IO.FileAttributes]::ReparsePoint) -ne 0) { continue }
            $stack.Push($s)
        }
    } catch { }
    try {
        foreach ($f in [IO.Directory]::GetFiles($dir)) {
            if ($exts.Count -eq 0 -or $exts -contains [IO.Path]::GetExtension($f).ToLowerInvariant()) { $files.Add($f) }
        }
    } catch { }
}
$files.Sort([StringComparer]::OrdinalIgnoreCase)

# 2) decode helpers
$utf8Strict = New-Object System.Text.UTF8Encoding($false, $true)
$cp949 = [Text.Encoding]::GetEncoding(949)
function Read-Text([byte[]]$b) {
    if ($b.Length -ge 3 -and $b[0] -eq 0xEF -and $b[1] -eq 0xBB -and $b[2] -eq 0xBF) { return [Text.Encoding]::UTF8.GetString($b, 3, $b.Length - 3) }
    if ($b.Length -ge 2 -and $b[0] -eq 0xFF -and $b[1] -eq 0xFE) { return [Text.Encoding]::Unicode.GetString($b, 2, $b.Length - 2) }
    if ($b.Length -ge 2 -and $b[0] -eq 0xFE -and $b[1] -eq 0xFF) { return [Text.Encoding]::BigEndianUnicode.GetString($b, 2, $b.Length - 2) }
    try { return $utf8Strict.GetString($b) } catch { return $cp949.GetString($b) }
}
$eval = [Text.RegularExpressions.MatchEvaluator] { param($m) "$HL$($m.Value)$C0" }

# 3) scan
$scanned = 0; $mFiles = 0; $mLines = 0; $hits = 0; $shown = 0; $lastTick = -1000
$total = $files.Count
$out = New-Object 'System.Collections.Generic.List[string]'
foreach ($f in $files) {
    $scanned++
    if ($sw.ElapsedMilliseconds - $lastTick -ge 100) {
        $lastTick = $sw.ElapsedMilliseconds
        [Console]::Write("`r$e[2K   $CD$($env:FT_L_SCAN)  $scanned / $total$C0")
    }
    try {
        $len = (New-Object IO.FileInfo($f)).Length
        if ($len -eq 0 -or $len -gt 20MB) { continue }
        $b = [IO.File]::ReadAllBytes($f)
    } catch { continue }
    $isU16 = $b.Length -ge 2 -and (($b[0] -eq 0xFF -and $b[1] -eq 0xFE) -or ($b[0] -eq 0xFE -and $b[1] -eq 0xFF))
    if (-not $isU16 -and [Array]::IndexOf($b, [byte]0, 0, [Math]::Min(8000, $b.Length)) -ge 0) { continue }
    $text = Read-Text $b
    if (-not $rx.IsMatch($text)) { continue }

    $lines = $text -split "`n"
    $found = New-Object 'System.Collections.Generic.List[object]'
    for ($i = 0; $i -lt $lines.Length; $i++) {
        $ln = $lines[$i].TrimEnd("`r")
        $ms = $rx.Matches($ln)
        if ($ms.Count -gt 0) { $found.Add(@(($i + 1), $ln)); $hits += $ms.Count }
    }
    if ($found.Count -eq 0) { continue }
    $mFiles++; $mLines += $found.Count

    $rel = $f.Substring($root.Length).TrimStart('\')
    $dirPart = [IO.Path]::GetDirectoryName($rel)
    $name = [IO.Path]::GetFileName($rel)
    $label = if ($dirPart) { "$CD$dirPart\$C0$CW$name$C0" } else { "$CW$name$C0" }
    $uri = 'file:///' + ($f -replace '\\', '/')

    if ($shown -lt $maxShow) {
        [Console]::Write("`r$e[2K")
        [Console]::WriteLine('')
        [Console]::WriteLine("  $icon ${e}]8;;$uri${e}\$label${e}]8;;${e}\  $CD($($found.Count))$C0")
    }
    foreach ($h in $found) {
        $out.Add(('{0}:{1}: {2}' -f $rel, $h[0], $h[1].Trim()))
        if ($shown -ge $maxShow) { continue }
        $shown++
        $disp = ($h[1] -replace "`t", '    ' -replace "$e", '?').TrimStart()
        if ($disp.Length -gt $maxWidth) {
            $m0 = $rx.Match($disp)
            $st = [Math]::Max(0, $m0.Index - 50)
            $cut = $disp.Substring($st, [Math]::Min($maxWidth, $disp.Length - $st))
            if ($st + $cut.Length -lt $disp.Length) { $cut = $cut + $dots }
            if ($st -gt 0) { $cut = $dots + $cut }
            $disp = $cut
        }
        $disp = $rx.Replace($disp, $eval)
        [Console]::WriteLine(("     {0}{1,6}{2} {3}{4}{5} {6}" -f $CN, $h[0], $C0, $CD, $bar, $C0, $disp))
    }
}
[Console]::Write("`r$e[2K")
$sw.Stop()

$sec = $sw.Elapsed.TotalSeconds.ToString('0.00', [Globalization.CultureInfo]::InvariantCulture)
$cutCount = $mLines - $shown
[IO.File]::WriteAllLines($env:FT_OUT, $out, (New-Object System.Text.UTF8Encoding $true))
[IO.File]::WriteAllText($env:FT_SUMMARY, "$scanned $mFiles $mLines $hits $sec $cutCount")
if ($mFiles -gt 0) { exit 0 } else { exit 1 }
