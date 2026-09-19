@echo off
chcp 65001 >nul
setlocal enabledelayedexpansion

:: ======================================================================
::  HASH - 오징어 해시 생성기 
::
::  입력한 문자열로 여러 해시를 한 번에 만든다  
::   · MD5 · SHA-1 · SHA-256 · SHA-384 · SHA-512 · CRC32 · Base64
::   · 인코딩 선택: UTF-8 (기본) · UTF-16 LE · CP949 - 같은 글자도 바이트가 다르면 해시가 달라진다 
::   · 결과 복사 (클립보드) · 해시 대조 (가지고 있는 해시가 어느 알고리즘과 맞는지) 
::
::  계산 엔진은 파일 끝 ::PS_ENGINE_BEGIN 아래 PowerShell 코드 (ASCII 전용) 
::  입력 문자열은 환경 변수로 넘긴다 (한글 · 특수문자 안전)
::
::  콘솔 테마: .claude/docs/cmd-console-style.md (마스코트: 오징어)
::  주의: 멀티바이트가 든 줄은 멀티바이트로 끝나지 않게, 짝수 바이트로 끝낸다  
::  주의: 지연 확장 상태이므로 문구에 느낌표를 쓰지 않는다 
:: ======================================================================

title Hash

set "HT_SELF=%~f0"
set "HT_ENGINE=%TEMP%\hash.engine.ps1"
set "HT_OUT=%TEMP%\hash.result.txt"
set "HT_ENC=utf8"
set "ENC_TXT=UTF-8"

:: ----------------------------------------------------------------------
:: 콘솔 테마 (G1~G6 배너: 오징어 먹물 보라 → 자홍) 
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
set "CP=%ESC%[38;5;213m"
set "H1=%ESC%[38;5;183m"
set "H2=%ESC%[38;5;147m"
set "G1=%ESC%[38;5;189m"
set "G2=%ESC%[38;5;183m"
set "G3=%ESC%[38;5;177m"
set "G4=%ESC%[38;5;171m"
set "G5=%ESC%[38;5;170m"
set "G6=%ESC%[38;5;133m"
set "RULE=%CD%──────────────────────────────────────────────────────────────────%C0%" 
set "CARD=──────────────────────────────────────────────" 
set "CARD_END=──────────────────────────────────────────────────────────────────────────────" 

:: 오징어  
set "SQ_1=   ___"
set "SQ_2=くコ:彡" 
set "SQ_3=   ‾‾‾" 
set "SQ_HAPPY=くコ*彡" 
set "SQ_WOW=くコ°彡"
set "SQ_SAD=くコ;彡" 

:: 알고리즘 (표시 이름 · 결과 키 · 평가) 
set "AL_1=MD5"
set "AK_1=MD5"
set "AL_2=SHA-1"
set "AK_2=SHA1"
set "AL_3=SHA-256"
set "AK_3=SHA256"
set "AL_4=SHA-384"
set "AK_4=SHA384"
set "AL_5=SHA-512"
set "AK_5=SHA512"
set "AL_6=CRC32"
set "AK_6=CRC32"
set "AL_7=Base64"
set "AK_7=B64"
set "AN_1=%CR%충돌 취약 · 체크섬 용도만%C0%" 
set "AN_2=%CR%충돌 취약%C0%"
set "AN_3=%CG%권장%C0%" 
set "AN_4=%CG%권장%C0%" 
set "AN_5=%CG%권장%C0%" 
set "AN_6=%CD%오류 검출용 · 보안 아님%C0%"
set "AN_7=%CD%인코딩 · 해시 아님%C0%" 

:: ======================================================================
:: 입력 
:: ======================================================================
:ASK
cls
call :BANNER
:ASK_AGAIN
echo.
echo   %CC%■%C0% %CW%문자열 입력%C0%  %CD%Enter 만 누르면 종료%C0% 
set "HT_TEXT="
set /p "HT_TEXT=!C0!    !CD!└!C0! 문자열  !CY!›!C0! "
if not defined HT_TEXT goto :QUIT

:: ======================================================================
:: 계산 + 결과  
:: ======================================================================
:CALC
call :RUN_ENGINE
if not exist "%HT_OUT%" goto :ENGINE_FAIL
for /f "usebackq tokens=1,* delims==" %%a in ("%HT_OUT%") do set "R_%%a=%%b"
call :SHOW

:MENU
echo.
echo   %RULE%
echo.
echo     %CD%└%C0% %CY%[1]%C0% 다른 문자열   %CY%[2]%C0% 인코딩 바꾸기   %CY%[3]%C0% 복사   %CY%[4]%C0% 해시 대조   %CY%[0]%C0% 종료 
choice /C 12340 /N /M "   선택 > "
if errorlevel 5 goto :QUIT
if errorlevel 4 goto :VERIFY
if errorlevel 3 goto :COPY
if errorlevel 2 goto :ENCODING
goto :ASK

:: ---- 인코딩 바꾸기 ----
:ENCODING
echo.
echo   %CC%■%C0% %CW%인코딩%C0%  %CD%지금: !ENC_TXT!%C0% 
echo     %CD%└%C0% %CY%[1]%C0% UTF-8        %CD%웹 · 리눅스 · 대부분의 API 기본%C0% 
echo     %CD%└%C0% %CY%[2]%C0% UTF-16 LE    %CD%Windows 내부 문자열 · .NET string%C0% 
echo     %CD%└%C0% %CY%[3]%C0% CP949        %CD%옛 한글 Windows (EUC-KR 확장)%C0%
choice /C 123 /N /M "   선택 > "
if errorlevel 3 (set "HT_ENC=cp949" & set "ENC_TXT=CP949") else if errorlevel 2 (set "HT_ENC=utf16" & set "ENC_TXT=UTF-16 LE") else (set "HT_ENC=utf8" & set "ENC_TXT=UTF-8")
cls
call :BANNER
goto :CALC

:: ---- 복사 ---- 
:COPY
echo.
echo   %CC%■%C0% %CW%복사할 값%C0%
echo     %CD%└%C0% %CY%[1]%C0% MD5  %CY%[2]%C0% SHA-1  %CY%[3]%C0% SHA-256  %CY%[4]%C0% SHA-384  %CY%[5]%C0% SHA-512  %CY%[6]%C0% CRC32  %CY%[7]%C0% Base64 
choice /C 1234567 /N /M "   선택 > "
set "_k=%errorlevel%"
for %%a in (!AK_%_k%!) do set "HT_COPY=!R_%%a!"
set "_name=!AL_%_k%!"
powershell -NoProfile -Command "Set-Clipboard -Value $env:HT_COPY"
if errorlevel 1 (echo   !CR!✗!C0! 클립보드에 복사하지 못했어요) else (echo   !CG!✓!C0! !_name! 값을 클립보드에 복사했어요) 
goto :MENU

:: ---- 해시 대조 ----
:VERIFY
echo.
echo   %CC%■%C0% %CW%해시 대조%C0%  %CD%가지고 있는 해시 값을 붙여 넣으세요 (대소문자 · 공백 무시)%C0%
set "EXP="
set /p "EXP=!C0!    !CD!└!C0! 해시  !CY!›!C0! " 
if not defined EXP goto :MENU
set "EXP=!EXP: =!"
set "_hit="
for /l %%k in (1,1,7) do for %%a in (!AK_%%k!) do if /i "!R_%%a!"=="!EXP!" set "_hit=!AL_%%k!"
echo.
if defined _hit goto :VERIFY_OK
echo    %CP%!SQ_1!%C0%
echo    %CP%!SQ_SAD!%C0%   %CR%✗ 불일치%C0%   %CD%어느 알고리즘과도 맞지 않아요. 인코딩을 바꿔 보세요 (지금 !ENC_TXT!)%C0% 
echo    %CP%!SQ_3!%C0%
goto :MENU
:VERIFY_OK
echo    %CP%!SQ_1!%C0%
echo    %CP%!SQ_WOW!%C0%   %CG%✓ 일치%C0% ✨   %CW%!_hit!%C0% %CD%· !ENC_TXT! 기준으로 같은 값이에요%C0%
echo    %CP%!SQ_3!%C0%
goto :MENU

:ENGINE_FAIL
echo   %CR%✗%C0% 해시 엔진을 실행하지 못했어요 %CD%(PowerShell)%C0%
goto :MENU

:QUIT
echo.
echo   %CD%  "비밀은 먹물 속에, 또 만나요"%C0% !SQ_HAPPY!
echo.
endlocal
exit /b 0

:: ======================================================================
:: 서브루틴: 배너 
:: ======================================================================
:BANNER
echo.
echo   %G1%██╗  ██╗ █████╗ ███████╗██╗  ██╗%C0% 
echo   %G2%██║  ██║██╔══██╗██╔════╝██║  ██║%C0%     %CP%!SQ_1!%C0%
echo   %G3%███████║███████║███████╗███████║%C0%    %CP%!SQ_2!%C0%   %CD%squish squish, hashing ink~%C0% 
echo   %G4%██╔══██║██╔══██║╚════██║██╔══██║%C0%     %CP%!SQ_3!%C0%
echo   %G5%██║  ██║██║  ██║███████║██║  ██║%C0% 
echo   %G6%╚═╝  ╚═╝╚═╝  ╚═╝╚══════╝╚═╝  ╚═╝%C0% 
echo   %CD%   오징어 해시 생성기  ·  MD5 · SHA-1 · SHA-2 · CRC32 · Base64 🔐%C0% 
echo.
echo   %CC%■%C0% %date% %time:~0,8%  %CD%·%C0%  %CD%인코딩%C0% %CW%!ENC_TXT!%C0%
echo   %RULE%
goto :eof

:: ======================================================================
:: 서브루틴: 엔진 실행 (HT_TEXT · HT_ENC → HT_OUT) 
:: ======================================================================
:RUN_ENGINE
if exist "%HT_OUT%" del /f /q "%HT_OUT%" >nul 2>&1
powershell -NoProfile -ExecutionPolicy Bypass -Command "$s = [IO.File]::ReadAllText($env:HT_SELF, [Text.Encoding]::UTF8); $m = [Environment]::NewLine + '::PS_ENGINE_BEGIN'; [IO.File]::WriteAllText($env:HT_ENGINE, $s.Substring($s.IndexOf($m) + $m.Length)); & $env:HT_ENGINE"
goto :eof

:: ======================================================================
:: 서브루틴: 결과 카드 (긴 해시는 64자씩 줄바꿈, 8자마다 색을 번갈아 읽기 쉽게)
:: ======================================================================
:SHOW
set "_shown=!HT_TEXT!"
if not "!_shown:~40,1!"=="" set "_shown=!_shown:~0,40!…"
echo.
echo   %CC%╭─%C0% 🔐 %CW%해시 결과%C0% %CC%%CARD%%C0% 
set "MSG=  !CC!│!C0!  !CD!입력!C0!       !CW!!_shown!!C0!  !CD!(!ENC_TXT! · !R_NBYTES!바이트)!C0!" & echo !MSG! 
set "MSG=  !CC!│!C0!  !CD!바이트!C0!     !CD!!R_HEAD!!C0!" & echo !MSG!
echo   %CC%│%C0%
for /l %%k in (1,1,7) do (
    for %%a in (!AK_%%k!) do set "_v=!R_%%a!"
    call :SHOW_ROW %%k
)
echo   %CC%╰%CARD_END%%C0%
echo.
echo    %CP%!SQ_1!%C0%
echo    %CP%!SQ_HAPPY!%C0%   %CG%✓ DONE%C0% ✨   %CD%먹물 일곱 방울, 해시 일곱 개 완성%C0%
echo    %CP%!SQ_3!%C0%
goto :eof

:SHOW_ROW
set "_nm=!AL_%~1!          "
set "_nm=!_nm:~0,9!"
set "_rest=!_v!"
set "_first=1"
:SHOW_ROW_LINE
set "_seg=!_rest:~0,64!"
set "_rest=!_rest:~64!"
call :COLOR_GROUPS
if defined _first (
    set "MSG=  !CC!│!C0!  !CW!!_nm!!C0!  !_col!   !AN_%~1!" 
) else (
    set "MSG=  !CC!│!C0!             !_col!"
)
echo !MSG!
set "_first="
if defined _rest goto :SHOW_ROW_LINE
goto :eof

:: _seg 를 8자씩 두 색으로 번갈아 칠해 _col 에 담는다 
:COLOR_GROUPS
set "_col="
set "_i=0"
set "_alt=0"
:CG_LOOP
set "_g=!_seg:~%_i%,8!"
if not defined _g goto :eof
if !_alt! equ 0 (set "_col=!_col!!H1!!_g!!C0!") else (set "_col=!_col!!H2!!_g!!C0!")
set /a "_i+=8, _alt=1-_alt"
goto :CG_LOOP

:: ======================================================================
:: PowerShell 해시 엔진 (cmd 는 여기까지 오지 않는다 - ASCII 전용 코드) 
::  입력: HT_TEXT HT_ENC(utf8 / utf16 / cp949)   출력: HT_OUT 에 KEY=VALUE 
:: ======================================================================
::PS_ENGINE_BEGIN
$ErrorActionPreference = 'Stop'
$text = [string]$env:HT_TEXT
switch ($env:HT_ENC) {
    'utf16' { $enc = [Text.Encoding]::Unicode }
    'cp949' { $enc = [Text.Encoding]::GetEncoding(949) }
    default { $enc = New-Object System.Text.UTF8Encoding $false }
}
$bytes = $enc.GetBytes($text)

function Hex([byte[]]$b) { -join ($b | ForEach-Object { $_.ToString('x2') }) }
function HashOf([string]$name) {
    $alg = [Security.Cryptography.HashAlgorithm]::Create($name)
    try { return Hex $alg.ComputeHash($bytes) } finally { $alg.Dispose() }
}

$crc = [long]4294967295
foreach ($b in $bytes) {
    $crc = $crc -bxor [long]$b
    for ($k = 0; $k -lt 8; $k++) {
        if (($crc -band 1) -eq 1) { $crc = ($crc -shr 1) -bxor [long]3988292384 } else { $crc = $crc -shr 1 }
    }
}
$crc = ($crc -bxor [long]4294967295) -band [long]4294967295

$head = Hex ($bytes | Select-Object -First 16)
$head = ($head -replace '(..)', '$1 ').Trim()
if ($bytes.Length -gt 16) { $head += ' ...' }

$lines = @(
    'NBYTES=' + $bytes.Length
    'HEAD=' + $head
    'MD5=' + (HashOf 'MD5')
    'SHA1=' + (HashOf 'SHA1')
    'SHA256=' + (HashOf 'SHA256')
    'SHA384=' + (HashOf 'SHA384')
    'SHA512=' + (HashOf 'SHA512')
    'CRC32=' + $crc.ToString('x8')
    'B64=' + [Convert]::ToBase64String($bytes)
)
[IO.File]::WriteAllLines($env:HT_OUT, $lines, (New-Object System.Text.UTF8Encoding $false))
