@echo off
chcp 65001 >nul
setlocal enabledelayedexpansion

:: ======================================================================
::  PHOTO - 토끼 사진사 사진·동영상 날짜 정리  
::  (Applications\Photo.Picture\Photo.Video.Organizer 의 cmd 판)
::
::  원본 폴더의 사진·동영상을 촬영일 기준 날짜 폴더로 복사(또는 이동)한다 
::   · 폴더 구조: yyyy\MM (기본) · yyyy\MM\yyyy-MM-dd · 직접 패턴 (예: yyyy/MM/dd)
::   · 새 이름: yyyy-MM-dd HH.mm.ss.확장자 · 이름이 겹치면 _1, _2 ... 
::   · 촬영일 우선순위: EXIF 원본 촬영일 → EXIF 디지털화 → EXIF 날짜 → 동영상 mvhd 생성일  
::                     → 파일명 패턴 (20231225_123456 · 2023-12-25 12.34.56 · 20231225) → 수정일  
::   · 대상 폴더에 내용이 같은 파일이 있으면 건너뜀 (크기 → SHA-256)
::   · 미리보기 후 실행, CSV 로그 선택  
::   · 원본 앱과 다른 점: EXIF 자동 회전 없음 (JPEG 재압축으로 화질 손실)
::                       HEIC · RAW 는 EXIF 를 못 읽어 파일명 → 수정일 순으로 판단 
::   · 사용법: photo.cmd [원본 폴더]   (폴더를 이 파일 위에 끌어다 놓아도 된다) 
::
::  처리 엔진은 파일 끝 ::PS_ENGINE_BEGIN 아래 PowerShell 코드 (ASCII 전용) 
::
::  콘솔 테마: .claude/docs/cmd-console-style.md (마스코트: 토끼 사진사) 
::  주의: 멀티바이트가 든 줄은 멀티바이트로 끝나지 않게, 짝수 바이트로 끝낸다  
::  주의: 지연 확장 상태이므로 문구에 느낌표를 쓰지 않는다 
:: ======================================================================

set "PH_ARG=%~1"
title Photo Organizer

set "PH_SELF=%~f0"
set "PH_ENGINE=%TEMP%\photo.engine.ps1"
set "PH_PLAN=%TEMP%\photo.plan.tsv"
set "PH_SUM=%TEMP%\photo.summary.txt"
set "PH_SET_DIR=%LOCALAPPDATA%\Playground\PhotoOrganizer"
set "PH_SET_FILE=%PH_SET_DIR%\settings.ini"
set "PH_L_SCAN=분석 중"
set "PH_L_APPLY=정리 중" 
set "PH_L_CSV=원본 경로,대상 경로,미디어 날짜,미디어 유형,결과,비고"
set "PH_L_OK=성공"
set "PH_L_DUP=중복 건너뜀" 
set "PH_L_ERR=오류" 
set "PH_L_IMG=사진" 
set "PH_L_VID=동영상"
set "PH_L_ETC=기타" 

:: ----------------------------------------------------------------------
:: 콘솔 테마 (G1~G6 배너: 벚꽃 분홍 → 라일락)
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
set "CP=%ESC%[38;5;225m"
set "G1=%ESC%[38;5;224m"
set "G2=%ESC%[38;5;218m"
set "G3=%ESC%[38;5;212m"
set "G4=%ESC%[38;5;177m"
set "G5=%ESC%[38;5;141m"
set "G6=%ESC%[38;5;105m"
set "RULE=%CD%──────────────────────────────────────────────────────────────────%C0%" 
set "CARD=──────────────────────────────────────────────" 
set "CARD_END=──────────────────────────────────────────────────────────────" 
set "STEP_TOTAL=4"

:: 토끼 사진사 
set "RB_1= (\_/)"
set "RB_2= (•ㅅ•)" 
set "RB_3= / >📷" 
set "RB_HAPPY=(^ㅅ^)"
set "RB_SAD=(;ㅅ;)"
set "RB_WOW=(°ㅅ°)"

:: 지난 설정  
set "LAST_DST="
set "LAST_STRUCT=1"
set "LAST_PATTERN=yyyy/MM/dd"
if exist "%PH_SET_FILE%" for /f "usebackq tokens=1,* delims==" %%a in ("%PH_SET_FILE%") do set "LAST_%%a=%%b"

:: ======================================================================
:: [1] 원본 폴더  
:: ======================================================================
:START
cls
call :BANNER
call :STEP 1 "원본 폴더"
echo   %CD%   사진·동영상이 들어 있는 폴더 (하위 폴더 포함)%C0%
:ASK_SRC
set "PH_SRC="
if defined PH_ARG (
    set "PH_SRC=!PH_ARG!"
    set "PH_ARG="
    set "MSG=    !CD!└!C0! 원본 폴더  !CW!!PH_SRC!!C0!  !CD!(끌어다 놓은 폴더)!C0!" & echo !MSG! 
) else (
    set /p "PH_SRC=!C0!    !CD!└!C0! 원본 폴더  !CY!›!C0! " 
)
if not defined PH_SRC goto :ASK_SRC
set "PH_SRC=!PH_SRC:"=!"
if "!PH_SRC:~-1!"=="\" set "PH_SRC=!PH_SRC:~0,-1!"
if exist "!PH_SRC!\*" goto :ASK_DST
echo   !CR!✗!C0! 폴더를 찾을 수 없습니다  !CD!!PH_SRC!!C0!
goto :ASK_SRC

:: ======================================================================
:: [2] 대상 폴더  
:: ======================================================================
:ASK_DST
call :STEP 2 "대상 폴더"
set "DEF_DST=!PH_SRC!_정리" 
if defined LAST_DST set "DEF_DST=!LAST_DST!"
set "PH_DST="
set /p "PH_DST=!C0!    !CD!└!C0! 대상 폴더  !CD!(Enter = !DEF_DST!)!C0!  !CY!›!C0! "
if not defined PH_DST set "PH_DST=!DEF_DST!"
set "PH_DST=!PH_DST:"=!"
if "!PH_DST:~-1!"=="\" set "PH_DST=!PH_DST:~0,-1!"
if /i "!PH_DST!"=="!PH_SRC!" (
    echo   !CR!✗!C0! 원본과 같은 폴더는 쓸 수 없어요 
    goto :ASK_DST
)
if not exist "!PH_DST!\*" (
    mkdir "!PH_DST!" >nul 2>&1
    if not exist "!PH_DST!\*" (
        echo   !CR!✗!C0! 대상 폴더를 만들 수 없습니다  !CD!!PH_DST!!C0! 
        goto :ASK_DST
    )
    set "MSG=    !CD!└ 새 폴더를 만들었어요!C0!" & echo !MSG!
)

:: ======================================================================
:: [3] 옵션 
:: ======================================================================
:ASK_OPT
call :STEP 3 "정리 방식"
echo   %CC%■%C0% %CW%폴더 구조%C0%  %CD%지난번: [!LAST_STRUCT!]%C0%
echo     %CD%└%C0% %CY%[1]%C0% 연\월            %CD%2024\06\2024-06-15 14.30.00.jpg%C0% 
echo     %CD%└%C0% %CY%[2]%C0% 연\월\날짜       %CD%2024\06\2024-06-15\2024-06-15 14.30.00.jpg%C0%
echo     %CD%└%C0% %CY%[3]%C0% 직접 패턴        %CD%/ 로 구분 (예: yyyy/MM/dd, yyyy/yyyy-MM)%C0%
choice /C 123 /N /M "   선택 > "
set "PH_STRUCT=%errorlevel%"
set "PH_PATTERN=!LAST_PATTERN!"
if "!PH_STRUCT!"=="3" (
    set "_p="
    set /p "_p=!C0!    !CD!└!C0! 패턴  !CD!(Enter = !LAST_PATTERN!)!C0!  !CY!›!C0! "
    if defined _p set "PH_PATTERN=!_p!"
)
echo.
echo   %CC%■%C0% %CW%방식%C0% 
echo     %CD%└%C0% %CY%[1]%C0% 복사   %CD%원본은 그대로 둠 (권장)%C0%
echo     %CD%└%C0% %CY%[2]%C0% 이동   %CD%원본 폴더에서 옮겨 감%C0%
choice /C 12 /N /M "   선택 > " 
set /a "PH_MOVE=%errorlevel% - 1"
echo.
echo   %CC%■%C0% %CW%CSV 로그%C0%  %CD%대상 폴더에 organizer_log_날짜.csv%C0%
echo     %CD%└%C0% %CY%[1]%C0% 저장   %CY%[2]%C0% 안 함 
choice /C 12 /N /M "   선택 > " 
set /a "PH_LOG=2 - %errorlevel%"
call :SAVE_SETTINGS

:: ======================================================================
:: [4] 미리보기 
:: ======================================================================
:PREVIEW
call :STEP 4 "미리보기" 
set "PH_MODE=plan"
call :RUN_ENGINE
if not exist "%PH_SUM%" goto :ENGINE_FAIL
for %%v in (FILES IMG VID ETC SRC_EXIF SRC_VIDEO SRC_NAME SRC_MTIME SIZE_MB FOLDERS SAMPLES PATTERN_BAD) do set "S_%%v=0"
for /f "usebackq tokens=1,* delims==" %%a in ("%PH_SUM%") do set "S_%%a=%%b"
if not "!S_PATTERN_BAD!"=="0" (
    echo   !CR!✗!C0! 폴더 패턴이 올바르지 않아요  !CD!!PH_PATTERN!!C0!
    goto :ASK_OPT
)
if "!S_FILES!"=="0" goto :NOTHING

set "_mode=%CG%복사%C0%"
if "!PH_MOVE!"=="1" set "_mode=%CY%이동%C0%"
echo.
echo   %CC%╭─%C0% 📷 %CW%정리 계획%C0% %CC%%CARD%%C0% 
set "MSG=  !CC!│!C0!  !CD!파일!C0!        !CW!!S_FILES!!C0!개  !CD!(사진 !S_IMG! · 동영상 !S_VID! · 기타 !S_ETC!)  !S_SIZE_MB! MB!C0!" & echo !MSG! 
set "MSG=  !CC!│!C0!  !CD!방식!C0!        !_mode!  !CD!→!C0! !PH_DST!" & echo !MSG! 
echo   %CC%│%C0%
echo   %CC%│%C0%  %CW%촬영일을 찾은 곳%C0% 
call :SRC_ROW "EXIF 촬영일  " !S_SRC_EXIF! 
call :SRC_ROW "동영상 정보  " !S_SRC_VIDEO!
call :SRC_ROW "파일 이름    " !S_SRC_NAME!
call :SRC_ROW "수정한 날짜  " !S_SRC_MTIME!
echo   %CC%│%C0%
echo   %CC%│%C0%  %CW%폴더별 파일 수%C0%  %CD%많은 순 최대 10개%C0% 
for /l %%i in (1,1,!S_FOLDERS!) do for /f "tokens=1,2 delims=|" %%a in ("!S_FOLDER_%%i!") do (
    set "_f=%%a                    "
    call :BAR %%b !S_FOLDER_MAX! 20 _g
    set "MSG=  !CC!│!C0!    !_f:~0,22! !_g! !CD!%%b!C0!"
    echo !MSG!
)
echo   %CC%│%C0%
echo   %CC%│%C0%  %CW%예시%C0%
for /l %%i in (1,1,!S_SAMPLES!) do for /f "tokens=1,2 delims=|" %%a in ("!S_SAMPLE_%%i!") do (
    set "MSG=  !CC!│!C0!    !CD!%%a!C0!  !CB!→!C0!  %%b"
    echo !MSG!
)
echo   %CC%╰%CARD_END%%C0%
if "!PH_MOVE!"=="1" echo   %CY%▲%C0% 이동 모드는 원본 폴더에서 파일이 사라집니다 
echo.
choice /C 120 /N /M "   [1] 실행   [2] 옵션 다시 고르기   [0] 취소 > " 
if errorlevel 3 goto :QUIT
if errorlevel 2 goto :ASK_OPT

:: ======================================================================
:: 실행 
:: ======================================================================
echo.
echo   %RULE%
echo.
echo   %CC%■%C0% %CB%RUN%C0%  %CW%정리 중%C0%  %CD%Ctrl+C 로 중단할 수 있어요 (처리한 파일은 그대로 남음)%C0% 
set "PH_MODE=apply"
call :RUN_ENGINE
if not exist "%PH_SUM%" goto :ENGINE_FAIL
for %%v in (OK DUP ERR LOG) do set "R_%%v="
for /f "usebackq tokens=1,* delims==" %%a in ("%PH_SUM%") do set "R_%%a=%%b"
echo.
echo   %CC%╭─%C0% 📷 %CW%정리 결과%C0% %CC%%CARD%%C0% 
set "MSG=  !CC!│!C0!  !CG!✓!C0! 정리함        !CW!!R_OK!!C0!개" & echo !MSG!
set "MSG=  !CC!│!C0!  !CY!–!C0! 중복 건너뜀   !R_DUP!개  !CD!(대상에 같은 내용이 이미 있음)!C0!" & echo !MSG! 
set "_ec=!CG!"
if not "!R_ERR!"=="0" set "_ec=!CR!"
set "MSG=  !CC!│!C0!  !_ec!✗!C0! 오류          !R_ERR!개" & echo !MSG! 
if defined R_LOG set "MSG=  !CC!│!C0!  !CD!로그!C0!          !CD!!R_LOG!!C0!" & echo !MSG!
echo   %CC%╰%CARD_END%%C0%
echo.
echo    %CP%!RB_1!%C0%
if "!R_ERR!"=="0" (
    set "MSG=   !CP!!RB_HAPPY!!C0!   !CG!✓ DONE!C0! ✨   !CD!찰칵, 날짜별로 착착 정리했어요!C0!"
) else (
    set "MSG=   !CP!!RB_SAD!!C0!   !CY!▲ CHECK!C0!   !CD!몇 장은 옮기지 못했어요. 파일이 열려 있는지 확인해 주세요!C0!" 
)
echo !MSG!
echo    %CP%!RB_3!%C0%
echo.
echo   %RULE%
echo.
choice /C 120 /N /M "   [1] 대상 폴더 열기   [2] 처음으로   [0] 종료 > "
if errorlevel 3 goto :QUIT
if errorlevel 2 goto :START
start "" explorer "!PH_DST!"
goto :QUIT

:NOTHING
echo.
echo    %CP%!RB_1!%C0%
echo    %CP%!RB_WOW!%C0%   %CY%▲ EMPTY%C0%   %CD%정리할 사진·동영상이 없어요%C0% 
echo    %CP%!RB_3!%C0%
echo.
choice /C 10 /N /M "   [1] 처음으로   [0] 종료 > "
if errorlevel 2 goto :QUIT
goto :START

:ENGINE_FAIL
echo   %CR%✗%C0% 처리 엔진을 실행하지 못했어요 %CD%(PowerShell)%C0%
goto :QUIT

:QUIT
echo.
echo   %CD%  "다음 여행 사진도 맡겨 주세요, 깡충"%C0% !RB_HAPPY!
echo.
endlocal
exit /b 0

:: ======================================================================
:: 서브루틴 
:: ======================================================================
:BANNER
echo.
echo   %G1%██████╗ ██╗  ██╗ ██████╗ ████████╗ ██████╗ %C0%
echo   %G2%██╔══██╗██║  ██║██╔═══██╗╚══██╔══╝██╔═══██╗%C0%     %CP%!RB_1!%C0% 
echo   %G3%██████╔╝███████║██║   ██║   ██║   ██║   ██║%C0%     %CP%!RB_2!%C0%   %CD%click click, sorting memories~%C0%
echo   %G4%██╔═══╝ ██╔══██║██║   ██║   ██║   ██║   ██║%C0%     %CP%!RB_3!%C0% 
echo   %G5%██║     ██║  ██║╚██████╔╝   ██║   ╚██████╔╝%C0%
echo   %G6%╚═╝     ╚═╝  ╚═╝ ╚═════╝    ╚═╝    ╚═════╝ %C0%
echo   %CD%   토끼 사진사  ·  사진·동영상을 촬영일 폴더로 정리 📷%C0%
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

:: 촬영일 출처 한 줄 (%1 = 이름, %2 = 개수)
:SRC_ROW
call :BAR %~2 !S_FILES! 20 _g
set "MSG=  !CC!│!C0!    %~1 !_g! !CD!%~2!C0!" 
echo !MSG!
goto :eof

:: 막대 (%1 = 값, %2 = 최댓값, %3 = 칸 수, %4 = 저장할 변수명)
:BAR
set "_on=0"
if %~2 gtr 0 set /a "_on=(%~1 * %~3 + %~2 - 1) / %~2"
if %_on% gtr %~3 set "_on=%~3"
set /a "_off=%~3 - _on"
set "_full=▰▰▰▰▰▰▰▰▰▰▰▰▰▰▰▰▰▰▰▰"
set "_empty=▱▱▱▱▱▱▱▱▱▱▱▱▱▱▱▱▱▱▱▱" 
set "%~4=!CP!!_full:~0,%_on%!!CD!!_empty:~0,%_off%!!C0!"
goto :eof

:SAVE_SETTINGS
if not exist "%PH_SET_DIR%" mkdir "%PH_SET_DIR%" >nul 2>&1
> "%PH_SET_FILE%" (
    echo DST=!PH_DST!
    echo STRUCT=!PH_STRUCT!
    echo PATTERN=!PH_PATTERN!
)
set "LAST_DST=!PH_DST!"
set "LAST_STRUCT=!PH_STRUCT!"
set "LAST_PATTERN=!PH_PATTERN!"
goto :eof

:RUN_ENGINE
if exist "%PH_SUM%" del /f /q "%PH_SUM%" >nul 2>&1
powershell -NoProfile -ExecutionPolicy Bypass -Command "$s = [IO.File]::ReadAllText($env:PH_SELF, [Text.Encoding]::UTF8); $m = [Environment]::NewLine + '::PS_ENGINE_BEGIN'; [IO.File]::WriteAllText($env:PH_ENGINE, $s.Substring($s.IndexOf($m) + $m.Length)); & $env:PH_ENGINE"
goto :eof

:: ======================================================================
:: PowerShell 처리 엔진 (cmd 는 여기까지 오지 않는다 - ASCII 전용 코드) 
::  PH_MODE=plan  : 원본을 훑어 PH_PLAN(TSV) 과 PH_SUM(요약) 작성 
::  PH_MODE=apply : PH_PLAN 대로 복사/이동하고 PH_SUM(결과) 작성  
:: ======================================================================
::PS_ENGINE_BEGIN
$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = New-Object System.Text.UTF8Encoding $false
Add-Type -AssemblyName System.Drawing
$e = [char]27
$C0 = "$e[0m"; $CD = "$e[90m"; $CP = "$e[38;5;225m"
$utf8 = New-Object System.Text.UTF8Encoding $false

$imgExt = '.jpg', '.jpeg', '.heic', '.heif', '.tiff', '.tif', '.cr2', '.nef', '.arw', '.dng', '.raf', '.orf'
$vidExt = '.mp4', '.mov', '.m4v', '.3gp', '.avi', '.mkv', '.wmv', '.mpg', '.mpeg'
$etcExt = '.png', '.gif', '.bmp', '.webp', '.ico', '.svg'
$qtExt = '.mp4', '.mov', '.m4v', '.3gp'

function Write-Progress2([int]$i, [int]$n, [string]$label, [string]$name) {
    $w = 24; $on = if ($n -gt 0) { [int][Math]::Floor($i * $w / $n) } else { 0 }
    $bar = ([string][char]0x25B0) * $on + ([string][char]0x25B1) * ($w - $on)
    if ($name.Length -gt 36) { $name = $name.Substring(0, 35) + [char]0x2026 }
    [Console]::Write("`r$e[2K    $CP$bar$C0  $i / $n  $CD$label  $name$C0")
}

function Read-U32BE($fs) { $b = New-Object byte[] 4; [void]$fs.Read($b, 0, 4); [Array]::Reverse($b); [BitConverter]::ToUInt32($b, 0) }
function Read-U64BE($fs) { $b = New-Object byte[] 8; [void]$fs.Read($b, 0, 8); [Array]::Reverse($b); [BitConverter]::ToUInt64($b, 0) }
function Read-Type($fs) { $b = New-Object byte[] 4; [void]$fs.Read($b, 0, 4); [Text.Encoding]::ASCII.GetString($b) }

# QuickTime / MP4: moov > mvhd creation time (seconds since 1904-01-01 UTC)
function Get-MvhdDate([string]$path) {
    $fs = [IO.File]::OpenRead($path)
    try {
        $end = $fs.Length; $pos = [long]0
        while ($pos + 8 -le $end) {
            $fs.Position = $pos
            $size = [long](Read-U32BE $fs); $type = Read-Type $fs; $hdr = 8
            if ($size -eq 1) { $size = [long](Read-U64BE $fs); $hdr = 16 } elseif ($size -eq 0) { $size = $end - $pos }
            if ($size -lt 8) { return $null }
            if ($type -eq 'moov') {
                $cpos = $pos + $hdr; $cend = [Math]::Min($end, $pos + $size)
                while ($cpos + 8 -le $cend) {
                    $fs.Position = $cpos
                    $csize = [long](Read-U32BE $fs); $ctype = Read-Type $fs
                    if ($ctype -eq 'mvhd') {
                        $ver = $fs.ReadByte(); $fs.Position += 3
                        $ct = if ($ver -eq 1) { [double](Read-U64BE $fs) } else { [double](Read-U32BE $fs) }
                        if ($ct -le 0) { return $null }
                        $d = (New-Object DateTime 1904, 1, 1, 0, 0, 0, ([DateTimeKind]::Utc)).AddSeconds($ct).ToLocalTime()
                        if ($d.Year -gt 1970) { return $d } else { return $null }
                    }
                    if ($csize -lt 8) { break }
                    $cpos += $csize
                }
                return $null
            }
            $pos += $size
        }
        return $null
    } finally { $fs.Dispose() }
}

function Get-ExifDate([string]$path) {
    $fs = $null; $img = $null
    try {
        $fs = [IO.File]::OpenRead($path)
        $img = [Drawing.Image]::FromStream($fs, $false, $false)
        foreach ($id in 36867, 36868, 306) {
            if ($img.PropertyIdList -contains $id) {
                $s = [Text.Encoding]::ASCII.GetString($img.GetPropertyItem($id).Value).Trim([char]0, ' ')
                $d = [DateTime]::MinValue
                if ([DateTime]::TryParseExact($s, 'yyyy:MM:dd HH:mm:ss', [Globalization.CultureInfo]::InvariantCulture, 'None', [ref]$d)) { return $d }
            }
        }
    } catch { } finally { if ($img) { $img.Dispose() }; if ($fs) { $fs.Dispose() } }
    return $null
}

$namePatterns = @(
    '(\d{4})(\d{2})(\d{2})_(\d{2})(\d{2})(\d{2})',
    '(\d{4})-(\d{2})-(\d{2})[\s_](\d{2})\.(\d{2})\.(\d{2})',
    '(\d{4})(\d{2})(\d{2})'
)
function Get-NameDate([string]$name) {
    foreach ($p in $namePatterns) {
        $m = [regex]::Match($name, $p)
        if (-not $m.Success) { continue }
        $y = [int]$m.Groups[1].Value; $mo = [int]$m.Groups[2].Value; $dd = [int]$m.Groups[3].Value
        if ($y -lt 1990 -or $y -gt 2100 -or $mo -lt 1 -or $mo -gt 12 -or $dd -lt 1 -or $dd -gt [DateTime]::DaysInMonth($y, $mo)) { continue }
        try {
            if ($m.Groups.Count -gt 6 -and $m.Groups[4].Success) {
                return New-Object DateTime $y, $mo, $dd, ([int]$m.Groups[4].Value), ([int]$m.Groups[5].Value), ([int]$m.Groups[6].Value)
            }
            return New-Object DateTime $y, $mo, $dd
        } catch { continue }
    }
    return $null
}

function Get-DestDir([datetime]$d) {
    switch ($env:PH_STRUCT) {
        '2' { return (Join-Path $env:PH_DST ($d.ToString('yyyy') + '\' + $d.ToString('MM') + '\' + $d.ToString('yyyy-MM-dd'))) }
        '3' {
            $parts = @($env:PH_PATTERN -split '/' | Where-Object { $_.Trim() } | ForEach-Object { $d.ToString($_.Trim()) })
            return (Join-Path $env:PH_DST ($parts -join '\'))
        }
        default { return (Join-Path $env:PH_DST ($d.ToString('yyyy') + '\' + $d.ToString('MM'))) }
    }
}

function Write-Kv([string[]]$lines) { [IO.File]::WriteAllLines($env:PH_SUM, $lines, $utf8) }

# ---------------------------------------------------------------- plan
if ($env:PH_MODE -eq 'plan') {
    if ($env:PH_STRUCT -eq '3') {
        try {
            $t = New-Object DateTime 2024, 6, 15, 14, 30, 0
            $parts = @($env:PH_PATTERN -split '/' | Where-Object { $_.Trim() })
            if ($parts.Count -eq 0) { throw 'empty' }
            foreach ($p in $parts) { $r = $t.ToString($p.Trim()); if ($r.IndexOfAny([IO.Path]::GetInvalidFileNameChars()) -ge 0) { throw 'bad' } }
        } catch { Write-Kv @('PATTERN_BAD=1'); exit 0 }
    }
    $dst = [IO.Path]::GetFullPath($env:PH_DST).TrimEnd('\') + '\'
    $all = @([IO.Directory]::EnumerateFiles($env:PH_SRC, '*', 'AllDirectories') | Where-Object {
        $x = [IO.Path]::GetExtension($_).ToLowerInvariant()
        (($imgExt -contains $x) -or ($vidExt -contains $x) -or ($etcExt -contains $x)) -and -not $_.StartsWith($dst, 'OrdinalIgnoreCase')
    })
    $n = $all.Count
    $rows = New-Object 'System.Collections.Generic.List[string]'
    $cnt = @{ IMG = 0; VID = 0; ETC = 0; EXIF = 0; VIDEO = 0; NAME = 0; MTIME = 0 }
    $folders = @{}; $bytes = [long]0; $samples = New-Object 'System.Collections.Generic.List[string]'
    $i = 0
    foreach ($f in $all) {
        $i++
        if ($i % 5 -eq 1 -or $i -eq $n) { Write-Progress2 $i $n $env:PH_L_SCAN ([IO.Path]::GetFileName($f)) }
        $x = [IO.Path]::GetExtension($f).ToLowerInvariant()
        $fi = New-Object IO.FileInfo $f
        $bytes += $fi.Length
        $kind = if ($imgExt -contains $x) { 'IMG' } elseif ($vidExt -contains $x) { 'VID' } else { 'ETC' }
        $cnt[$kind]++
        $d = $null; $src = 'MTIME'
        if ($kind -ne 'ETC') {
            if ($kind -eq 'IMG') { $d = Get-ExifDate $f; if ($d) { $src = 'EXIF' } }
            if (-not $d -and $kind -eq 'VID' -and ($qtExt -contains $x)) { try { $d = Get-MvhdDate $f } catch { $d = $null }; if ($d) { $src = 'VIDEO' } }
            if (-not $d) { $d = Get-NameDate ([IO.Path]::GetFileNameWithoutExtension($f)); if ($d) { $src = 'NAME' } }
        }
        if (-not $d) { $d = $fi.LastWriteTime; $src = 'MTIME' }
        $cnt[$src]++
        $dir = Get-DestDir $d
        $newName = $d.ToString('yyyy-MM-dd HH.mm.ss') + [IO.Path]::GetExtension($f)
        $rel = $dir.Substring($env:PH_DST.TrimEnd('\').Length).TrimStart('\')
        if ($folders.ContainsKey($rel)) { $folders[$rel]++ } else { $folders[$rel] = 1 }
        $rows.Add(($f, $dir, $newName, $d.ToString('yyyy-MM-dd HH:mm:ss'), $src, $kind) -join "`t")
    }
    [Console]::Write("`r$e[2K")
    [IO.File]::WriteAllLines($env:PH_PLAN, $rows, $utf8)

    $kv = New-Object 'System.Collections.Generic.List[string]'
    $kv.Add("FILES=$n"); $kv.Add("IMG=$($cnt.IMG)"); $kv.Add("VID=$($cnt.VID)"); $kv.Add("ETC=$($cnt.ETC)")
    $kv.Add("SRC_EXIF=$($cnt.EXIF)"); $kv.Add("SRC_VIDEO=$($cnt.VIDEO)"); $kv.Add("SRC_NAME=$($cnt.NAME)"); $kv.Add("SRC_MTIME=$($cnt.MTIME)")
    $kv.Add('SIZE_MB=' + ($bytes / 1MB).ToString('0.0', [Globalization.CultureInfo]::InvariantCulture))
    $top = @($folders.GetEnumerator() | Sort-Object Value -Descending | Select-Object -First 10)
    $kv.Add("FOLDERS=$($top.Count)")
    $kv.Add('FOLDER_MAX=' + $(if ($top.Count) { $top[0].Value } else { 0 }))
    $k = 0; foreach ($t in $top) { $k++; $kv.Add("FOLDER_$k=$($t.Key -replace '[|=!%]', ' ')|$($t.Value)") }
    $step = [Math]::Max(1, [int][Math]::Floor($n / 6)); $k = 0
    for ($j = 0; $j -lt $n -and $k -lt 6; $j += $step) {
        $c = $rows[$j] -split "`t"; $k++
        $a = [IO.Path]::GetFileName($c[0]); if ($a.Length -gt 28) { $a = $a.Substring(0, 27) + [char]0x2026 }
        $b = (Join-Path ($c[1].Substring($env:PH_DST.TrimEnd('\').Length).TrimStart('\')) $c[2])
        $kv.Add("SAMPLE_$k=$($a -replace '[|=!%]', ' ')|$($b -replace '[|=!%]', ' ')")
    }
    $kv.Add("SAMPLES=$k")
    Write-Kv $kv.ToArray()
    exit 0
}

# ---------------------------------------------------------------- apply
$rows = [IO.File]::ReadAllLines($env:PH_PLAN, $utf8)
$n = $rows.Count; $ok = 0; $dup = 0; $err = 0; $i = 0
$move = $env:PH_MOVE -eq '1'
$log = New-Object 'System.Collections.Generic.List[string]'
$kindName = @{ IMG = $env:PH_L_IMG; VID = $env:PH_L_VID; ETC = $env:PH_L_ETC }
function Csv([string]$s) { '"' + $s.Replace('"', '""') + '"' }
function Hash([string]$p) { $h = [Security.Cryptography.SHA256]::Create(); $s = [IO.File]::OpenRead($p); try { [BitConverter]::ToString($h.ComputeHash($s)) } finally { $s.Dispose(); $h.Dispose() } }

foreach ($r in $rows) {
    $i++
    $c = $r -split "`t"; $srcPath = $c[0]; $dir = $c[1]; $name = $c[2]
    Write-Progress2 $i $n $env:PH_L_APPLY ([IO.Path]::GetFileName($srcPath))
    $result = $env:PH_L_OK; $target = ''; $note = ''
    try {
        [void][IO.Directory]::CreateDirectory($dir)
        $len = (New-Object IO.FileInfo $srcPath).Length
        $same = $null
        foreach ($other in [IO.Directory]::EnumerateFiles($dir)) {
            if ((New-Object IO.FileInfo $other).Length -eq $len) {
                if ($null -eq $same) { $srcHash = Hash $srcPath; $same = $false }
                if ((Hash $other) -eq $srcHash) { $same = $true; $target = $other; break }
            }
        }
        if ($same -eq $true) {
            $dup++; $result = $env:PH_L_DUP
        } else {
            $target = Join-Path $dir $name
            $base = [IO.Path]::GetFileNameWithoutExtension($name); $ext = [IO.Path]::GetExtension($name); $k = 1
            while (Test-Path -LiteralPath $target) { $target = Join-Path $dir ("{0}_{1}{2}" -f $base, $k, $ext); $k++ }
            if ($move) { [IO.File]::Move($srcPath, $target) } else { [IO.File]::Copy($srcPath, $target, $false) }
            $ok++
        }
    } catch { $err++; $result = $env:PH_L_ERR; $note = $_.Exception.Message }
    $log.Add(((Csv $srcPath), (Csv $target), (Csv $c[3]), (Csv $kindName[$c[5]]), (Csv $result), (Csv $note)) -join ',')
}
[Console]::Write("`r$e[2K")
$logPath = ''
if ($env:PH_LOG -eq '1') {
    $logPath = Join-Path $env:PH_DST ('organizer_log_' + (Get-Date).ToString('yyyyMMdd_HHmmss') + '.csv')
    $all = New-Object 'System.Collections.Generic.List[string]'; $all.Add($env:PH_L_CSV); $all.AddRange($log)
    [IO.File]::WriteAllLines($logPath, $all, (New-Object System.Text.UTF8Encoding $true))
}
Write-Kv @("OK=$ok", "DUP=$dup", "ERR=$err", "LOG=$logPath")
