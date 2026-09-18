@echo off
chcp 65001 >nul
setlocal enabledelayedexpansion

:: ======================================================================
::  PC CHECK - 왓치독 PC 건강검진  
::
::  PC 상태를 한 번에 점검해 진단 카드로 보여 준다  
::   · 기본 정보 · CPU · 메모리 · 디스크 · 네트워크 · 배터리 · 메모리 많이 쓰는 프로그램 
::   · 진단: 디스크 여유 · 메모리 · CPU · 재부팅 대기 · 가동 시간 · 실시간 보호 · 시스템 오류 · 인터넷  
::   · 사용법: sys-check.cmd            대화형 (다시 검진 · 보고서 저장) 
::             sys-check.cmd /nopause   한 번 보여 주고 종료 (로그온 스크립트용)
::   · 종료 코드: 0 = 건강, 1 = 주의, 2 = 위험 
::
::  수집 엔진은 파일 끝 ::PS_ENGINE_BEGIN 아래 PowerShell 코드 (ASCII 전용) 
::  %TEMP% 에 .ps1 로 추출해 백그라운드 실행하고, 그동안 검진 애니메이션을 보여 준다 
::
::  콘솔 테마: .claude/docs/cmd-console-style.md (마스코트: 왓치독)
::  주의: 멀티바이트가 든 줄은 멀티바이트로 끝나지 않게, 짝수 바이트로 끝낸다  
::  주의: 지연 확장 상태이므로 문구에 느낌표를 쓰지 않는다 
::  주의: 캐럿이 든 이모티콘은 느낌표가 없는 set 줄에서만 정의한다 
::  주의: 퍼센트가 든 문구는 call 인자로 넘기지 않는다 (call 이 %% 를 한 번 더 해석)
:: ======================================================================

set "NOPAUSE="
if /i "%~1"=="/nopause" set "NOPAUSE=1"
title PC Check

set "SC_SELF=%~f0"
set "SC_ENGINE=%TEMP%\sys-check.engine.ps1"
set "SC_OUT=%TEMP%\sys-check.result.txt"
set "REPORT_DIR=%USERPROFILE%\Desktop"

call :THEME
call :CALIBRATE

:: 왓치독 (캐럿 포함 - 이 줄들에는 느낌표를 쓰지 않는다) 
set "DOG_1=   _     _"
set "DOG_2=  U・ᴥ・U" 
set "DOG_3=   (🩺 )"
set "DOG_HAPPY=U^ᴥ^U" 
set "DOG_WORRY=U;ᴥ;U" 
set "DOG_ALARM=U◉ᴥ◉U" 

:: 검진 중 문구  
set "STAGE_0=CPU 맥박 재는 중" 
set "STAGE_1=메모리 혈압 재는 중" 
set "STAGE_2=디스크 엑스레이 찍는 중" 
set "STAGE_3=네트워크 청진하는 중" 
set "STAGE_4=이벤트 로그 문진하는 중" 
set "STAGE_5=진단서 쓰는 중"
set "SPIN_CH=⠋⠙⠹⠸⠼⠴⠦⠧⠇⠏"

:: ======================================================================
:: 검진 
:: ======================================================================
:CHECKUP
cls
call :BANNER
call :COLLECT
if not exist "%SC_OUT%" goto :ENGINE_FAIL
call :LOAD
call :DIAGNOSE
call :RENDER
if defined NOPAUSE goto :END

:MENU
echo.
echo   %RULE%
echo.
choice /C 120 /N /M "   [1] 다시 검진   [2] 보고서 저장   [0] 종료 > " 
if errorlevel 3 goto :END
if errorlevel 2 goto :SAVE_REPORT
goto :CHECKUP

:SAVE_REPORT
for /f %%T in ('powershell -NoProfile -Command "Get-Date -Format yyyyMMdd_HHmm"') do set "_stamp=%%T"
if not exist "%REPORT_DIR%\" set "REPORT_DIR=%USERPROFILE%"
set "REPORT=%REPORT_DIR%\PC-Check_!_stamp!.txt"
rem 색 변수를 비우면 CD 가 %CD%(현재 폴더)로 풀리므로, 색 있는 출력을 받은 뒤 ANSI 코드만 지운다  
set "SC_TMP=%TEMP%\sys-check.report.tmp"
> "!SC_TMP!" (
    echo PC CHECK - !_stamp!
    call :RENDER
)
set "SC_REPORT=!REPORT!"
powershell -NoProfile -Command "$t = [IO.File]::ReadAllText($env:SC_TMP, [Text.Encoding]::UTF8) -replace '\x1b\[[0-9;?]*[a-zA-Z]', ''; [IO.File]::WriteAllText($env:SC_REPORT, $t, (New-Object Text.UTF8Encoding $true))"
del /f /q "!SC_TMP!" >nul 2>&1
echo.
echo   %CG%✓%C0% 보고서를 저장했어요 
set "MSG=    !CD!└!C0! !REPORT!" & echo !MSG! 
goto :MENU

:ENGINE_FAIL
echo.
echo   %CR%✗%C0% 검진 엔진이 결과를 만들지 못했어요 %CD%(PowerShell 실행 또는 시간 초과)%C0% 
set "RC=2"
goto :END_RC

:END
set "RC=!DIAG_RC!"
:END_RC
echo.
if not defined NOPAUSE echo   %CD%  "건강한 PC 가 일도 잘한다멍, 또 검진 받으러 오라멍"%C0% !DOG_HAPPY!
echo.
endlocal & exit /b %RC%

:: ======================================================================
:: 서브루틴: 테마 
:: ======================================================================
:THEME
for /f %%E in ('echo prompt $E^| cmd') do set "ESC=%%E"
set "C0=%ESC%[0m"
set "CW=%ESC%[97m"
set "CD=%ESC%[90m"
set "CC=%ESC%[96m"
set "CB=%ESC%[38;5;75m"
set "CG=%ESC%[92m"
set "CY=%ESC%[93m"
set "CR=%ESC%[91m"
set "CP=%ESC%[38;5;223m"
set "G1=%ESC%[38;5;159m"
set "G2=%ESC%[38;5;123m"
set "G3=%ESC%[38;5;87m"
set "G4=%ESC%[38;5;51m"
set "G5=%ESC%[38;5;44m"
set "G6=%ESC%[38;5;37m"
set "RULE=%CD%──────────────────────────────────────────────────────────────────%C0%" 
set "CARD=──────────────────────────────────────────────" 
set "CARD_END=──────────────────────────────────────────────────────────────" 
set "CUR_OFF=%ESC%[?25l"
set "CUR_ON=%ESC%[?25h"
goto :eof

:: ======================================================================
:: 서브루틴: 배너 
:: ======================================================================
:BANNER
echo.
echo   %G1%██████╗  ██████╗     ██████╗██╗  ██╗███████╗ ██████╗██╗  ██╗%C0% 
echo   %G2%██╔══██╗██╔════╝    ██╔════╝██║  ██║██╔════╝██╔════╝██║ ██╔╝%C0% 
echo   %G3%██████╔╝██║         ██║     ███████║█████╗  ██║     █████╔╝ %C0% 
echo   %G4%██╔═══╝ ██║         ██║     ██╔══██║██╔══╝  ██║     ██╔═██╗ %C0% 
echo   %G5%██║     ╚██████╗    ╚██████╗██║  ██║███████╗╚██████╗██║  ██╗%C0% 
echo   %G6%╚═╝      ╚═════╝     ╚═════╝╚═╝  ╚═╝╚══════╝ ╚═════╝╚═╝  ╚═╝%C0% 
echo   %CD%   왓치독 PC 건강검진  ·  CPU · 메모리 · 디스크 · 네트워크 한 번에 🩺%C0%
echo.
echo   %CC%■%C0% %date% %time:~0,8% 
echo.
echo    %CP%!DOG_1!%C0%      🐶 %CD%왓치독 선생님%C0%   "멍, 오늘도 꼼꼼히 봐 드릴게요" 
echo    %CP%!DOG_2!%C0%
echo    %CP%!DOG_3!%C0%
echo.
echo   %RULE%
goto :eof

:: ======================================================================
:: 서브루틴: 수집 (엔진을 백그라운드로 돌리고 검진 애니메이션) 
:: ======================================================================
:COLLECT
if exist "%SC_OUT%" del /f /q "%SC_OUT%" >nul 2>&1
start "" /b powershell -NoProfile -ExecutionPolicy Bypass -Command "$s = [IO.File]::ReadAllText($env:SC_SELF, [Text.Encoding]::UTF8); $m = [Environment]::NewLine + '::PS_ENGINE_BEGIN'; [IO.File]::WriteAllText($env:SC_ENGINE, $s.Substring($s.IndexOf($m) + $m.Length)); & $env:SC_ENGINE" >nul 2>&1
echo.
<nul set /p "=%CUR_OFF%"
set "_f=0"
:COLLECT_LOOP
if exist "%SC_OUT%" goto :COLLECT_DONE
if !_f! geq 900 goto :COLLECT_DONE
set /a "_si=_f %% 10, _st=(_f / 7) %% 6, _walk=(_f / 2) %% 12"
set "_sp=!SPIN_CH:~%_si%,1!"
for %%s in (!_st!) do set "_msg=!STAGE_%%s!"
set "_path=············"
set "_path=!_path:~0,%_walk%!"
<nul set /p "=%ESC%[1G%ESC%[2K  %CC%!_sp!%C0% %CY%»%C0% !_msg!...   %CD%!_path!%C0%🐕%CD%·%C0%🦴" 
call :SLEEP 90
set /a _f+=1
goto :COLLECT_LOOP
:COLLECT_DONE
<nul set /p "=%ESC%[1G%ESC%[2K  %CG%✓%C0% 검진 완료%CUR_ON%"
echo.
goto :eof

:: ======================================================================
:: 서브루틴: 결과 읽기 (KEY=VALUE → SC_KEY) 
:: ======================================================================
:LOAD
for %%v in (DISK_COUNT PROC_COUNT BAT_PCT BAT_AC DEF_RT NET_PING ERR24 PENDING GPU) do set "SC_%%v="
for /f "usebackq tokens=1,* delims==" %%a in ("%SC_OUT%") do set "SC_%%a=%%b"
goto :eof

:: ======================================================================
:: 서브루틴: 진단 (D_n = 상태|항목|설명, DIAG_OK/WARN/BAD, DIAG_RC) 
::  설명 문구에 %% 가 들어가므로 ADD_DIAG 에는 문구 대신 변수 이름(_t)을 넘긴다  
::  항목 이름은 표시 폭 16칸으로 미리 맞춘다 (한글 1자 = 2칸) 
:: ======================================================================
:DIAGNOSE
set /a "DIAG_N=0, DIAG_OK=0, DIAG_WARN=0, DIAG_BAD=0"
rem ---- 디스크 ---- 
set "_s=OK"
set "_t=모든 드라이브 여유 20%% 이상" 
for /l %%i in (1,1,!SC_DISK_COUNT!) do for /f "tokens=1-5 delims=|" %%a in ("!SC_DISK_%%i!") do (
    set /a "_free=100-%%d"
    if !_free! lss 20 if not "!_s!"=="BAD" (set "_s=WARN" & set "_t=%%a 여유 !_free!%% - 정리가 필요해요") 
    if !_free! lss 10 (set "_s=BAD" & set "_t=%%a 여유 !_free!%% - 곧 가득 차요")
)
call :ADD_DIAG !_s! "디스크 여유 공간" _t
rem ---- 메모리 ---- 
set "_s=OK"
set "_t=사용률 !SC_MEM_PCT!%%" 
if !SC_MEM_PCT! geq 80 set "_s=WARN" & set "_t=사용률 !SC_MEM_PCT!%% - 프로그램을 좀 닫아 주세요" 
if !SC_MEM_PCT! geq 92 set "_s=BAD" & set "_t=사용률 !SC_MEM_PCT!%% - 메모리가 거의 없어요" 
call :ADD_DIAG !_s! "메모리          " _t
rem ---- CPU ----
set "_s=OK"
set "_t=사용률 !SC_CPU_LOAD!%%"
if !SC_CPU_LOAD! geq 85 set "_s=WARN" & set "_t=사용률 !SC_CPU_LOAD!%% - 바쁜 프로그램이 있어요" 
call :ADD_DIAG !_s! "CPU             " _t
rem ---- 재부팅 대기 ----
set "_s=OK"
set "_t=대기 중인 업데이트 재부팅 없음"
if "!SC_PENDING!"=="1" set "_s=WARN" & set "_t=업데이트 적용을 위해 재부팅이 필요해요" 
call :ADD_DIAG !_s! "재부팅 대기     " _t
rem ---- 가동 시간 ---- 
set "_s=OK"
set "_t=!SC_UP_D!일째 가동" 
if !SC_UP_D! geq 7 set "_s=WARN" & set "_t=!SC_UP_D!일째 가동 - 한 번 재부팅하면 가벼워져요"
call :ADD_DIAG !_s! "가동 시간       " _t 
rem ---- 실시간 보호 (확인할 수 없으면 생략) ---- 
set "_t=Microsoft Defender 켜짐"
if "!SC_DEF_RT!"=="1" call :ADD_DIAG OK "실시간 보호     " _t
set "_t=Microsoft Defender 실시간 보호가 꺼져 있어요"
if "!SC_DEF_RT!"=="0" call :ADD_DIAG BAD "실시간 보호     " _t 
rem ---- 시스템 오류 ----
set "_s=OK"
set "_t=최근 24시간 오류 !SC_ERR24!건" 
if !SC_ERR24! geq 20 set "_s=WARN" & set "_t=최근 24시간 오류 !SC_ERR24!건 - 이벤트 뷰어 확인 권장" 
call :ADD_DIAG !_s! "시스템 오류     " _t
rem ---- 인터넷 ---- 
set "_s=OK"
set "_t=응답 !SC_NET_PING!ms (1.1.1.1)" 
if !SC_NET_PING! lss 0 set "_s=WARN" & set "_t=1.1.1.1 응답 없음 - 연결을 확인하세요" 
call :ADD_DIAG !_s! "인터넷          " _t
rem ---- 배터리 ---- 
set "_t=!SC_BAT_PCT!%% 남음 - 충전기를 연결하세요" 
if defined SC_BAT_PCT if !SC_BAT_PCT! lss 20 if "!SC_BAT_AC!"=="0" call :ADD_DIAG WARN "배터리          " _t 

set "DIAG_RC=0"
set "DIAG_TXT=%CG%건강%C0%" 
set "DIAG_FACE=!DOG_HAPPY!"
set "DIAG_TALK=멍멍, 아주 건강해요. 이대로만 쓰세요" 
if !DIAG_WARN! gtr 0 set "DIAG_RC=1" & set "DIAG_TXT=%CY%주의 !DIAG_WARN!건%C0%" & set "DIAG_FACE=!DOG_WORRY!" & set "DIAG_TALK=조금 신경 써 주면 금방 좋아져요"
if !DIAG_BAD! gtr 0 set "DIAG_RC=2" & set "DIAG_TXT=%CR%위험 !DIAG_BAD!건%C0%" & set "DIAG_FACE=!DOG_ALARM!" & set "DIAG_TALK=왈왈, 빨간 항목부터 바로 처치가 필요해요" 
goto :eof

:: 인수: %1 = OK/WARN/BAD, %2 = 항목 이름, %3 = 설명이 든 변수 이름 
:ADD_DIAG
set /a DIAG_N+=1
set "D_!DIAG_N!=%~1|%~2|!%~3!"
if /i "%~1"=="OK" set /a DIAG_OK+=1
if /i "%~1"=="WARN" set /a DIAG_WARN+=1
if /i "%~1"=="BAD" set /a DIAG_BAD+=1
goto :eof

:: ======================================================================
:: 서브루틴: 결과 화면 (보고서 저장 때도 재사용 - ANSI 는 저장 후 제거) 
:: ======================================================================
:RENDER
echo.
echo   %CC%■%C0% %CW%기본 정보%C0%
set "MSG=    !CD!└!C0! 컴퓨터      !SC_HOST!  !CD!(!SC_USER!)!C0!" & echo !MSG!
set "MSG=    !CD!└!C0! 운영체제    !SC_OS!  !CD!!SC_OS_VER!!C0!" & echo !MSG! 
set "MSG=    !CD!└!C0! 가동 시간   !SC_UP_D!일 !SC_UP_H!시간 !SC_UP_M!분  !CD!(부팅 !SC_BOOT!)!C0!" & echo !MSG!
if defined SC_GPU set "MSG=    !CD!└!C0! 그래픽      !SC_GPU!" & echo !MSG!
echo.
echo   %CC%■%C0% %CW%CPU%C0%  %CD%심장 박동%C0% 
set "MSG=    !CD!└!C0! 모델        !SC_CPU_NAME!" & echo !MSG!
call :GAUGE !SC_CPU_LOAD! 20 _g
set "MSG=    !CD!└!C0! 사용률      !_g!  !SC_CPU_LOAD!%%  !CD!(!SC_CPU_CORES!코어 !SC_CPU_THREADS!스레드)!C0!" & echo !MSG! 
echo.
echo   %CC%■%C0% %CW%메모리%C0%  %CD%혈압%C0%
call :GAUGE !SC_MEM_PCT! 20 _g
set "MSG=    !CD!└!C0! 사용량      !_g!  !SC_MEM_PCT!%%  !CD!(!SC_MEM_USED! / !SC_MEM_TOTAL! GB)!C0!" & echo !MSG! 
echo.
echo   %CC%■%C0% %CW%디스크%C0%  %CD%엑스레이%C0%
for /l %%i in (1,1,!SC_DISK_COUNT!) do for /f "tokens=1-5 delims=|" %%a in ("!SC_DISK_%%i!") do (
    call :GAUGE %%d 20 _g
    set "_lab="
    if not "%%e"=="" set "_lab=!CD!%%e!C0!"
    set "MSG=    !CD!└!C0! %%a          !_g!  %%d%%  !CD!여유!C0! %%c !CD!/ %%b GB!C0!  !_lab!" 
    echo !MSG!
)
echo.
echo   %CC%■%C0% %CW%네트워크%C0%  %CD%청진%C0% 
set "MSG=    !CD!└!C0! 어댑터      !SC_NET_NAME!  !CD!!SC_NET_SPEED!!C0!" & echo !MSG! 
set "MSG=    !CD!└!C0! IP 주소     !SC_NET_IP!" & echo !MSG!
set "_ping=!CY!응답 없음!C0!" 
if !SC_NET_PING! geq 0 set "_ping=!CG!✓!C0! 응답 !SC_NET_PING!ms !CD!(1.1.1.1)!C0!" 
set "MSG=    !CD!└!C0! 인터넷      !_ping!" & echo !MSG! 
if not defined SC_BAT_PCT goto :RENDER_PROC
echo.
echo   %CC%■%C0% %CW%배터리%C0%
call :GAUGE_REV !SC_BAT_PCT! 20 _g
set "_ac=배터리 사용 중"
if "!SC_BAT_AC!"=="1" set "_ac=충전기 연결됨" 
set "MSG=    !CD!└!C0! 잔량        !_g!  !SC_BAT_PCT!%%  !CD!(!_ac!)!C0!" & echo !MSG!
:RENDER_PROC
echo.
echo   %CC%■%C0% %CW%메모리를 많이 쓰는 프로그램%C0%
for /l %%i in (1,1,!SC_PROC_COUNT!) do for /f "tokens=1-4 delims=|" %%a in ("!SC_PROC_%%i!") do (
    set "_pn=%%a                    "
    set "_mb=          %%b"
    call :BAR %%d 12 _g
    set "MSG=    !CD!└!C0! !_pn:~0,18! !_mb:~-8! MB  !_g!  !CD!%%c개!C0!"
    echo !MSG!
)
echo.
echo   %CC%╭─%C0% 🩺 %CW%진단 결과%C0% %CC%%CARD%%C0% 
for /l %%i in (1,1,!DIAG_N!) do for /f "tokens=1-3 delims=|" %%a in ("!D_%%i!") do (
    set "_ic=!CG!✓!C0!" 
    if "%%a"=="WARN" set "_ic=!CY!▲!C0!"
    if "%%a"=="BAD" set "_ic=!CR!✗!C0!" 
    set "MSG=  !CC!│!C0!  !_ic! %%b  !CD!%%c!C0!" 
    echo !MSG!
)
echo   %CC%│%C0%
set "MSG=  !CC!│!C0!  !CW!종합 판정!C0!   !DIAG_TXT!   !CD!정상 !DIAG_OK! · 주의 !DIAG_WARN! · 위험 !DIAG_BAD!!C0!" & echo !MSG!
echo   %CC%╰%CARD_END%%C0%
echo.
echo    %CP%!DOG_1!%C0%
set "MSG=   !CP!!DIAG_FACE!!C0!   !DIAG_TALK!" & echo !MSG!
echo    %CP%!DOG_3!%C0%
goto :eof

:: ======================================================================
:: 서브루틴: 게이지 (%1 = 퍼센트, %2 = 칸 수, %3 = 저장할 변수명) 
::  GAUGE 는 높을수록 나쁨 (초록 < 70 ≤ 노랑 < 90 ≤ 빨강), GAUGE_REV 는 낮을수록 나쁨 
:: ======================================================================
:GAUGE
set /a "_on=(%~1 * %~2 + 50) / 100"
if %_on% gtr %~2 set "_on=%~2"
set "_col=%CG%"
if %~1 geq 70 set "_col=%CY%"
if %~1 geq 90 set "_col=%CR%"
goto :GAUGE_DRAW
:GAUGE_REV
set /a "_on=(%~1 * %~2 + 50) / 100"
if %_on% gtr %~2 set "_on=%~2"
set "_col=%CG%"
if %~1 lss 50 set "_col=%CY%"
if %~1 lss 20 set "_col=%CR%"
:GAUGE_DRAW
set "_full=▰▰▰▰▰▰▰▰▰▰▰▰▰▰▰▰▰▰▰▰"
set "_empty=▱▱▱▱▱▱▱▱▱▱▱▱▱▱▱▱▱▱▱▱" 
set /a "_off=%~2 - _on"
set "%~3=!_col!!_full:~0,%_on%!!CD!!_empty:~0,%_off%!!C0!"
goto :eof

:BAR
set /a "_on=(%~1 * %~2 + 50) / 100"
if %_on% lss 1 set "_on=1"
if %_on% gtr %~2 set "_on=%~2"
set "_full=▰▰▰▰▰▰▰▰▰▰▰▰"
set "%~3=!CB!!_full:~0,%_on%!!C0!"
goto :eof

:: ======================================================================
:: 서브루틴: 지연 (baseball.cmd 와 동일 - 루프 속도 측정 보정) 
:: ======================================================================
:SLEEP
set /a "_n=%~1 * LOOP_PER_MS"
for /l %%i in (1,1,%_n%) do rem
goto :eof

:CALIBRATE
call :NOW_CS _t0
for /l %%i in (1,1,400000) do rem
call :NOW_CS _t1
set /a "_dt=_t1 - _t0"
if !_dt! lss 0 set /a "_dt+=8640000"
if !_dt! lss 1 set "_dt=1"
set /a "LOOP_PER_MS=400000 / (_dt * 10)"
if !LOOP_PER_MS! lss 1 set "LOOP_PER_MS=1"
goto :eof

:NOW_CS
set "_tm=%time: =0%"
set /a "%~1=((1!_tm:~0,2!-100)*60 + (1!_tm:~3,2!-100))*6000 + (1!_tm:~6,2!-100)*100 + (1!_tm:~9,2!-100)"
goto :eof

:: ======================================================================
:: PowerShell 수집 엔진 (cmd 는 여기까지 오지 않는다 - ASCII 전용 코드) 
::  출력: SC_OUT 에 KEY=VALUE (UTF-8, BOM 없음). 임시 파일에 쓴 뒤 이름을 바꿔 한 번에 나타나게 한다  
:: ======================================================================
::PS_ENGINE_BEGIN
$ErrorActionPreference = 'SilentlyContinue'
$inv = [Globalization.CultureInfo]::InvariantCulture
$kv = New-Object 'System.Collections.Generic.List[string]'
function Add-Kv([string]$k, $v) { $kv.Add(('{0}={1}' -f $k, (([string]$v) -replace '[\r\n=|!%]', ' ').Trim())) }
function Gb([double]$bytes) { ($bytes / 1GB).ToString('0.0', $inv) }

# basic
$os = Get-CimInstance Win32_OperatingSystem
$cv = Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion'
Add-Kv 'HOST' $env:COMPUTERNAME
Add-Kv 'USER' $env:USERNAME
Add-Kv 'OS' ($os.Caption -replace '^Microsoft ', '')
Add-Kv 'OS_VER' ('{0} build {1}.{2}' -f $cv.DisplayVersion, $cv.CurrentBuild, $cv.UBR)
$up = (Get-Date) - $os.LastBootUpTime
Add-Kv 'UP_D' $up.Days
Add-Kv 'UP_H' $up.Hours
Add-Kv 'UP_M' $up.Minutes
Add-Kv 'BOOT' $os.LastBootUpTime.ToString('yyyy-MM-dd HH:mm')
$gpu = Get-CimInstance Win32_VideoController | Where-Object { $_.Name -notmatch 'Basic|Remote|Virtual' } | Select-Object -First 1
if ($gpu) { Add-Kv 'GPU' $gpu.Name }

# cpu
$cpu = Get-CimInstance Win32_Processor
$first = @($cpu)[0]
Add-Kv 'CPU_NAME' (($first.Name -replace '\s+', ' ').Trim())
Add-Kv 'CPU_CORES' (($cpu | Measure-Object NumberOfCores -Sum).Sum)
Add-Kv 'CPU_THREADS' (($cpu | Measure-Object NumberOfLogicalProcessors -Sum).Sum)
$load = ($cpu | Measure-Object LoadPercentage -Average).Average
if ($null -eq $load) { $load = (Get-CimInstance Win32_PerfFormattedData_PerfOS_Processor -Filter "Name='_Total'").PercentProcessorTime }
Add-Kv 'CPU_LOAD' ([int][Math]::Round([double]$load))

# memory
$total = [double]$os.TotalVisibleMemorySize * 1KB
$free = [double]$os.FreePhysicalMemory * 1KB
Add-Kv 'MEM_TOTAL' (Gb $total)
Add-Kv 'MEM_USED' (Gb ($total - $free))
Add-Kv 'MEM_PCT' ([int][Math]::Round(($total - $free) * 100 / $total))

# disks
$n = 0
foreach ($d in (Get-CimInstance Win32_LogicalDisk -Filter 'DriveType=3' | Sort-Object DeviceID)) {
    if (-not $d.Size) { continue }
    $n++
    $pct = [int][Math]::Round(($d.Size - $d.FreeSpace) * 100 / $d.Size)
    $kv.Add(('DISK_{0}={1}|{2}|{3}|{4}|{5}' -f $n, $d.DeviceID, (Gb $d.Size), (Gb $d.FreeSpace), $pct, (([string]$d.VolumeName) -replace '[|=!%]', ' ')))
}
Add-Kv 'DISK_COUNT' $n

# network
$cfg = Get-CimInstance Win32_NetworkAdapterConfiguration -Filter 'IPEnabled=True' | Where-Object { $_.DefaultIPGateway } | Select-Object -First 1
if ($cfg) {
    $ad = Get-CimInstance Win32_NetworkAdapter -Filter "Index=$($cfg.Index)"
    Add-Kv 'NET_NAME' $ad.NetConnectionID
    Add-Kv 'NET_IP' (@($cfg.IPAddress | Where-Object { $_ -match '^\d+\.' })[0])
    if ($ad.Speed) {
        $mb = [double]$ad.Speed / 1e6
        if ($mb -ge 1000) { Add-Kv 'NET_SPEED' ('{0} Gbps' -f ($mb / 1000).ToString('0.#', $inv)) } else { Add-Kv 'NET_SPEED' ('{0} Mbps' -f [int]$mb) }
    }
} else { Add-Kv 'NET_NAME' '-'; Add-Kv 'NET_IP' '-' }
$ping = -1
try { $r = (New-Object System.Net.NetworkInformation.Ping).Send('1.1.1.1', 1500); if ($r.Status -eq 'Success') { $ping = [int]$r.RoundtripTime } } catch { }
Add-Kv 'NET_PING' $ping

# battery
$bat = Get-CimInstance Win32_Battery | Select-Object -First 1
if ($bat) {
    Add-Kv 'BAT_PCT' $bat.EstimatedChargeRemaining
    if ($bat.BatteryStatus -eq 2 -or $bat.BatteryStatus -ge 6) { Add-Kv 'BAT_AC' 1 } else { Add-Kv 'BAT_AC' 0 }
}

# top memory programs (grouped by name)
$i = 0
$groups = @(Get-Process | Group-Object ProcessName | ForEach-Object {
    [pscustomobject]@{ Name = $_.Name; Ws = [double](($_.Group | Measure-Object WorkingSet64 -Sum).Sum); Count = $_.Count }
} | Sort-Object Ws -Descending | Select-Object -First 5)
$maxWs = [Math]::Max([double]1, [double]$groups[0].Ws)
foreach ($g in $groups) {
    $i++
    $kv.Add(('PROC_{0}={1}|{2}|{3}|{4}' -f $i, ($g.Name -replace '[|=!%]', ' '), ([int]($g.Ws / 1MB)).ToString('N0', $inv), $g.Count, [int]($g.Ws * 100 / $maxWs)))
}
Add-Kv 'PROC_COUNT' $i

# health signals
$pending = 0
if (Test-Path 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootPending') { $pending = 1 }
if (Test-Path 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\RebootRequired') { $pending = 1 }
Add-Kv 'PENDING' $pending
$rt = -1
try { $mp = Get-MpComputerStatus; if ($null -ne $mp) { $rt = [int][bool]$mp.RealTimeProtectionEnabled } } catch { }
Add-Kv 'DEF_RT' $rt
$err = 0
try { $err = @(Get-WinEvent -FilterHashtable @{ LogName = 'System'; Level = 1, 2; StartTime = (Get-Date).AddDays(-1) } -MaxEvents 500).Count } catch { }
Add-Kv 'ERR24' $err

$tmp = $env:SC_OUT + '.tmp'
[IO.File]::WriteAllLines($tmp, $kv, (New-Object System.Text.UTF8Encoding $false))
Move-Item -LiteralPath $tmp -Destination $env:SC_OUT -Force
