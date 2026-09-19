@echo off
chcp 65001 >nul
setlocal enabledelayedexpansion

:: ======================================================================
::  CLEAN - 생쥐 청소부 시스템 청소 
::  (Applications\System\Manager\Sys.Clean 의 청소 기능 cmd 판) 
::
::  쓸모없는 임시 파일·캐시를 찾아 크기를 보여 주고, 고른 항목만 지운다  
::   · 대상: 사용자 Temp · Windows Temp · 휴지통 · 썸네일 캐시 · 크래시 덤프 · 브라우저 캐시  
::           · Windows Update 캐시 · Prefetch · 최근 문서 목록 · 개발 도구 캐시 
::   · 분석 → 선택 → 확인 → 청소 순서. 사용 중인 파일은 건너뛴다  
::   · 원본 앱과 다른 점: Temp 는 24시간 지난 파일만 · 브라우저 캐시는 모든 프로필 
::                       방문 기록 · 쿠키 · 시작 프로그램 삭제는 넣지 않았다 (되돌리기 어려움) 
::   · 레지스트리 정리: 없는 파일을 가리키는 앱 경로 · 제거 프로그램 목록 · 공유 DLL · 시작 프로그램 항목  
::       정리 전에 바뀔 키를 모두 .reg 로 백업 (백업 실패 시 아무것도 바꾸지 않음), 처음 메뉴 [3] 으로 되돌리기  
::       오탐 방지: 따옴표 · 환경 변수 해석, System32/SysWOW64 · Program Files (x86) 대체 확인,
::                 빠진 드라이브 · 네트워크 경로 · 경로 없는 이름은 판단하지 않음  
::   · 관리자 권한이 필요한 항목은 관리자로 다시 실행해야 청소된다 
::
::  처리 엔진은 파일 끝 ::PS_ENGINE_BEGIN 아래 PowerShell 코드 (ASCII 전용) 
::
::  콘솔 테마: .claude/docs/cmd-console-style.md (마스코트: 생쥐 청소부) 
::  주의: 멀티바이트가 든 줄은 멀티바이트로 끝나지 않게, 짝수 바이트로 끝낸다  
::  주의: 지연 확장 상태이므로 문구에 느낌표를 쓰지 않는다 
:: ======================================================================

title System Clean

set "CL_SELF=%~f0"
set "CL_ENGINE=%TEMP%\clean.engine.ps1"
set "CL_SUM=%TEMP%\clean.summary.txt"
set "CL_STAT_DIR=%LOCALAPPDATA%\Playground\Clean"
set "CL_STAT_FILE=%CL_STAT_DIR%\stats.ini"
set "CL_L_SCAN=살펴보는 중"
set "CL_L_CLEAN=치우는 중"
set "T_COUNT=10"
set "CL_REG=%TEMP%\clean.reg.tsv"
set "CL_REGVIEW=%TEMP%\clean.regview.txt"
set "CL_REG_BAK=%CL_STAT_DIR%\RegBackup"
set "CL_RN_1=앱 경로"
set "CL_RN_2=제거 프로그램 목록"
set "CL_RN_3=공유 DLL"
set "CL_RN_4=시작 프로그램" 
set "RP_1=앱 경로            " 
set "RP_2=제거 프로그램 목록 "
set "RP_3=공유 DLL           "
set "RP_4=시작 프로그램      "
set "RD_1=없는 exe 를 가리키는 App Paths 키"
set "RD_2=설치 폴더와 제거 프로그램이 모두 사라진 항목"
set "RD_3=없는 파일을 세는 SharedDLLs 값" 
set "RD_4=없는 exe 를 실행하려는 Run 값" 
for /l %%i in (1,1,4) do set "RS_%%i=1"

:: 관리자 여부 
set "IS_ADMIN=0"
net session >nul 2>&1 && set "IS_ADMIN=1"

:: ----------------------------------------------------------------------
:: 콘솔 테마 (G1~G6 배너: 민트 → 청록, 깨끗한 느낌)
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
set "CP=%ESC%[38;5;250m"
set "CK=%ESC%[38;5;121m"
set "G1=%ESC%[38;5;194m"
set "G2=%ESC%[38;5;158m"
set "G3=%ESC%[38;5;122m"
set "G4=%ESC%[38;5;86m"
set "G5=%ESC%[38;5;43m"
set "G6=%ESC%[38;5;37m"
set "RULE=%CD%──────────────────────────────────────────────────────────────────%C0%" 
set "CARD=──────────────────────────────────────────────" 
set "CARD_END=──────────────────────────────────────────────────────────────────────" 

:: 생쥐 청소부 
set "MS_1=  ()_()"
set "MS_2= ( •ᴥ• )🧹" 
set "MS_3=  (_)(_)~"
set "MS_HAPPY=<^3 )~"
set "MS_SAD=<;3 )~"
set "MS_WOW=<°3 )~"

:: 청소 대상 (이름 · 설명 · 관리자 필요 · 기본 선택)
set "TN_1=사용자 Temp" 
set "TP_1=사용자 Temp         "  
set "TD_1=%%TEMP%% · 24시간 지난 파일만"
set "TA_1=0"
set "TS_1=1"
set "TN_2=Windows Temp"
set "TP_2=Windows Temp        "
set "TD_2=C:\Windows\Temp · 24시간 지난 파일만" 
set "TA_2=1"
set "TS_2=1"
set "TN_3=휴지통"
set "TP_3=휴지통              "
set "TD_3=모든 드라이브"
set "TA_3=0"
set "TS_3=1"
set "TN_4=썸네일 캐시" 
set "TP_4=썸네일 캐시         "  
set "TD_4=탐색기 미리보기 그림 (.db)"
set "TA_4=0"
set "TS_4=1"
set "TN_5=크래시 덤프" 
set "TP_5=크래시 덤프         "  
set "TD_5=프로그램 · 시스템 오류 덤프"
set "TA_5=0"
set "TS_5=1"
set "TN_6=브라우저 캐시"
set "TP_6=브라우저 캐시       " 
set "TD_6=Chrome · Edge · Brave · Vivaldi · Firefox · Opera" 
set "TA_6=0"
set "TS_6=1"
set "TN_7=Windows Update 캐시"
set "TP_7=Windows Update 캐시 " 
set "TD_7=설치가 끝난 업데이트 다운로드" 
set "TA_7=1"
set "TS_7=1"
set "TN_8=Prefetch"
set "TP_8=Prefetch            "
set "TD_8=지우면 첫 실행이 잠깐 느려짐" 
set "TA_8=1"
set "TS_8=0"
set "TN_9=최근 문서 목록" 
set "TP_9=최근 문서 목록      " 
set "TD_9=바로 가기 목록만 (문서는 그대로)"
set "TA_9=0"
set "TS_9=0"
set "TN_10=개발 도구 캐시"
set "TP_10=개발 도구 캐시      "
set "TD_10=npm · NuGet · pip · Gradle · Maven (다시 받아야 함)" 
set "TA_10=0"
set "TS_10=0"

call :LOAD_STATS

:: ======================================================================
:: 분석 
:: ======================================================================
:START
cls
call :BANNER
echo.
echo   %CC%■%C0% %CW%무엇을 정리할까요%C0%
echo     %CD%└%C0% %CY%[1]%C0% 파일 청소                %CD%Temp · 캐시 · 휴지통 등 10개 항목%C0%
echo     %CD%└%C0% %CY%[2]%C0% 레지스트리 정리          %CD%없는 프로그램을 가리키는 항목 · 정리 전 .reg 백업%C0% 
echo     %CD%└%C0% %CY%[3]%C0% 레지스트리 백업 되돌리기  
echo     %CD%└%C0% %CY%[0]%C0% 종료 
choice /C 1230 /N /M "   선택 > " 
if errorlevel 4 goto :QUIT
if errorlevel 3 goto :REG_RESTORE
if errorlevel 2 goto :REG_START

:FILE_START
cls
call :BANNER
echo.
echo   %CC%■%C0% %CB%SCAN%C0%  %CW%청소할 거리 찾는 중%C0%
set "CL_MODE=scan"
set "CL_SEL="
call :RUN_ENGINE
if not exist "%CL_SUM%" goto :ENGINE_FAIL
for /l %%i in (1,1,%T_COUNT%) do (
    set "B_%%i=0"
    set "BS_%%i=0"
    set "F_%%i=0"
    set "X_%%i=0"
)
for /f "usebackq tokens=1,* delims==" %%a in ("%CL_SUM%") do set "%%a=%%b"

:: ======================================================================
:: 선택 
:: ======================================================================
:SELECT
call :SHOW_TABLE
echo.
echo   %CD%   번호로 켜고 끄기 (예: 8 9)  ·  R 권장  ·  A 모두  ·  N 모두 끄기  ·  Enter 다음%C0% 
set "_in="
set /p "_in=!C0!    !CD!└!C0! 선택  !CY!›!C0! " 
if not defined _in goto :CONFIRM
for %%x in (!_in!) do (
    set "_x=%%x"
    if /i "!_x!"=="A" for /l %%i in (1,1,%T_COUNT%) do set "TS_%%i=1"
    if /i "!_x!"=="N" for /l %%i in (1,1,%T_COUNT%) do set "TS_%%i=0"
    if /i "!_x!"=="R" (
        for /l %%i in (1,1,7) do set "TS_%%i=1"
        for /l %%i in (8,1,10) do set "TS_%%i=0"
    )
    for /l %%i in (1,1,%T_COUNT%) do if "!_x!"=="%%i" (
        if "!TS_%%i!"=="1" (set "TS_%%i=0") else (set "TS_%%i=1")
    )
)
cls
call :BANNER
goto :SELECT

:: ======================================================================
:: 확인 
:: ======================================================================
:CONFIRM
set "CL_SEL="
set "_need_admin=0"
set /a "_sel_mb=0"
for /l %%i in (1,1,%T_COUNT%) do if "!TS_%%i!"=="1" (
    if "!TA_%%i!"=="1" if "!IS_ADMIN!"=="0" set "_need_admin=1"
    set "CL_SEL=!CL_SEL! %%i"
    if not "!X_%%i!"=="-1" set /a "_sel_mb+=B_%%i"
)
set "_sel_txt=!_sel_mb! MB"
if !_sel_mb! equ 0 set "_sel_txt=1 MB 미만" 
if not defined CL_SEL (
    echo   !CY!▲!C0! 고른 항목이 없어요 
    goto :SELECT
)
echo.
echo   %RULE%
echo.
echo   %CC%■%C0% %CW%청소 전 확인%C0%
set "MSG=    !CD!└!C0! 지울 양       약 !CK!!_sel_txt!!C0!  !CD!(사용 중인 파일은 건너뜀)!C0!" & echo !MSG! 
set "MSG=    !CD!└!C0! 방식          !CY!영구 삭제!C0!  !CD!(휴지통을 거치지 않음)!C0!" & echo !MSG! 
if "!TS_6!"=="1" call :CHECK_BROWSERS
if "!_need_admin!"=="1" (
    set "MSG=    !CY!▲!C0! 관리자 권한이 필요한 항목은 건너뜁니다  !CD![9] 로 관리자 권한으로 다시 실행할 수 있어요!C0!"
    echo !MSG!
)
echo.
choice /C 1290 /N /M "   [1] 청소   [2] 다시 고르기   [9] 관리자로 다시 실행   [0] 취소 > "
if errorlevel 4 goto :QUIT
if errorlevel 3 (set "_back=CONFIRM" & goto :ELEVATE)
if errorlevel 2 (
    cls
    call :BANNER
    goto :SELECT
)

:: ======================================================================
:: 청소 
:: ======================================================================
echo.
echo   %CC%■%C0% %CB%CLEAN%C0%  %CW%치우는 중%C0% 
set "CL_MODE=clean"
call :RUN_ENGINE
if not exist "%CL_SUM%" goto :ENGINE_FAIL
for /l %%i in (1,1,%T_COUNT%) do (
    set "RB_%%i=0"
    set "RBS_%%i=0"
    set "RF_%%i=0"
    set "RE_%%i=0"
    set "TOTS=0"
)
for /f "usebackq tokens=1,* delims==" %%a in ("%CL_SUM%") do set "%%a=%%b"
set /a "_tot_mb=0, _tot_err=0"
echo.
echo   %CC%╭─%C0% 🧹 %CW%청소 결과%C0% %CC%%CARD%%C0% 
for /l %%i in (1,1,%T_COUNT%) do if "!TS_%%i!"=="1" call :RESULT_ROW %%i
echo   %CC%│%C0%
set /a "ST_TOTAL_MB+=_tot_mb, ST_RUNS+=1"
call :SAVE_STATS
set "MSG=  !CC!│!C0!  !CW!이번에 비운 양!C0!  !CK!!TOTS!!C0!   !CD!누적 !ST_TOTAL_MB! MB · !ST_RUNS!번째 청소!C0!" & echo !MSG!
echo   %CC%╰%CARD_END%%C0%
echo.
echo    %CP%!MS_1!%C0%
if !_tot_err! equ 0 (
    set "MSG=   !CP!!MS_HAPPY!!C0!   !CG!✓ CLEAN!C0! ✨   !CD!찍찍, 구석구석 다 치웠어요!C0!" 
) else (
    set "MSG=   !CP!!MS_WOW!!C0!   !CG!✓ CLEAN!C0!   !CD!사용 중이라 못 지운 파일 !_tot_err!개는 그대로 뒀어요!C0!" 
)
echo !MSG!
echo    %CP%!MS_3!%C0%
echo.
echo   %RULE%
echo.
choice /C 10 /N /M "   [1] 처음으로   [0] 종료 > "  
if errorlevel 2 goto :QUIT
goto :START

:: ======================================================================
:: 레지스트리 정리 
:: ======================================================================
:REG_START
cls
call :BANNER
echo.
echo   %CC%■%C0% %CB%REG%C0%  %CW%레지스트리 살펴보는 중%C0%  %CD%읽기만 합니다%C0% 
set "CL_MODE=regscan"
call :RUN_ENGINE
if not exist "%CL_SUM%" goto :ENGINE_FAIL
for /l %%i in (1,1,4) do (
    set "RC_%%i=0"
    set "RH_%%i=0"
)
for /f "usebackq tokens=1,* delims==" %%a in ("%CL_SUM%") do set "%%a=%%b"
set /a "_rall=RC_1 + RC_2 + RC_3 + RC_4"

:REG_SELECT
set /a "_rtot=0"
echo.
echo   %CC%╭─%C0% 🗝  %CW%레지스트리 정리 거리%C0% %CC%%CARD%%C0%
for /l %%i in (1,1,4) do call :REG_ROW %%i
echo   %CC%│%C0%
set "MSG=  !CC!│!C0!  !CW!고른 항목!C0!  !CK!!_rtot!건!C0!" & echo !MSG! 
echo   %CC%╰%CARD_END%%C0%
if !_rall! equ 0 goto :REG_NONE
echo.
type "%CL_REGVIEW%"
echo   %CD%   번호로 켜고 끄기 (예: 3)  ·  Enter 다음  ·  0 처음으로%C0%
set "_in="
set /p "_in=!C0!    !CD!└!C0! 선택  !CY!›!C0! " 
if not defined _in goto :REG_CONFIRM
if "!_in!"=="0" goto :START
for %%x in (!_in!) do for /l %%i in (1,1,4) do if "%%x"=="%%i" (
    if "!RS_%%i!"=="1" (set "RS_%%i=0") else (set "RS_%%i=1")
)
cls
call :BANNER
goto :REG_SELECT

:REG_CONFIRM
set "CL_RSEL="
set /a "_rn=0, _rh=0"
for /l %%i in (1,1,4) do if "!RS_%%i!"=="1" (
    set "CL_RSEL=!CL_RSEL! %%i"
    set /a "_rn+=RC_%%i, _rh+=RH_%%i"
)
if !_rn! equ 0 (
    echo   !CY!▲!C0! 고른 항목에 정리할 것이 없어요  
    goto :REG_AFTER
)
echo.
echo   %RULE%
echo.
echo   %CC%■%C0% %CW%정리 전 확인%C0%
set "MSG=    !CD!└!C0! 정리할 항목   !CK!!_rn!건!C0!  !CD!(키 또는 값 삭제)!C0!" & echo !MSG! 
set "MSG=    !CD!└!C0! 백업          !CW!정리 전에 자동 저장!C0!  !CD!!CL_REG_BAK!\registry_날짜.reg!C0!" & echo !MSG!
set "MSG=    !CD!└!C0! 되돌리기      !CD!백업 파일 더블클릭, 또는 처음 메뉴 [3]!C0!" & echo !MSG! 
if "!IS_ADMIN!"=="0" if !_rh! gtr 0 (
    set "MSG=    !CY!▲!C0! HKLM 항목 !_rh!건은 관리자 권한이 필요해 건너뜁니다  !CD![9] 로 관리자 권한으로 다시 실행할 수 있어요!C0!"
    echo !MSG!
)
echo.
choice /C 1290 /N /M "   [1] 백업 후 정리   [2] 다시 고르기   [9] 관리자로 다시 실행   [0] 취소 > " 
if errorlevel 4 goto :START
if errorlevel 3 (set "_back=REG_CONFIRM" & goto :ELEVATE)
if errorlevel 2 (
    cls
    call :BANNER
    goto :REG_SELECT
)
echo.
echo   %CC%■%C0% %CB%REG%C0%  %CW%백업하고 정리하는 중%C0% 
set "CL_MODE=regfix"
call :RUN_ENGINE
if not exist "%CL_SUM%" goto :ENGINE_FAIL
set "BACKUP="
set "BACKUP_FAIL=0"
for /l %%i in (1,1,4) do (
    set "RF_%%i=0"
    set "RX_%%i=0"
    set "RA_%%i=0"
)
for /f "usebackq tokens=1,* delims==" %%a in ("%CL_SUM%") do set "%%a=%%b"
if "!BACKUP_FAIL!"=="1" (
    echo   !CR!✗!C0! 백업을 만들지 못해 아무것도 바꾸지 않았어요 
    goto :REG_AFTER
)
set /a "_rf=0, _rx=0"
echo.
echo   %CC%╭─%C0% 🗝  %CW%레지스트리 정리 결과%C0% %CC%%CARD%%C0%
for /l %%i in (1,1,4) do if "!RS_%%i!"=="1" call :REG_RESULT_ROW %%i
echo   %CC%│%C0%
if defined BACKUP (
    set "MSG=  !CC!│!C0!  !CW!백업!C0!  !CD!!BACKUP!!C0!" 
) else (
    set "MSG=  !CC!│!C0!  !CD!바꾼 항목이 없어 백업도 만들지 않았어요!C0!" 
)
echo !MSG!
echo   %CC%╰%CARD_END%%C0%
echo.
echo    %CP%!MS_1!%C0%
if !_rx! equ 0 (
    set "MSG=   !CP!!MS_HAPPY!!C0!   !CG!✓ CLEAN!C0! ✨   !CD!찍찍, 길 잃은 항목 !_rf!건을 치웠어요. 백업은 잘 챙겨 뒀어요!C0!"
) else (
    set "MSG=   !CP!!MS_WOW!!C0!   !CY!▲ CHECK!C0!   !CD!!_rx!건은 지우지 못했어요 (권한 · 사용 중)!C0!" 
)
echo !MSG!
echo    %CP%!MS_3!%C0%

:REG_AFTER
echo.
echo   %RULE%
echo.
choice /C 120 /N /M "   [1] 처음으로   [2] 백업 폴더 열기   [0] 종료 > "
if errorlevel 3 goto :QUIT
if errorlevel 2 (
    if not exist "%CL_REG_BAK%\" mkdir "%CL_REG_BAK%" >nul 2>&1
    start "" explorer "%CL_REG_BAK%"
    goto :REG_AFTER
)
goto :START

:REG_NONE
echo.
echo    %CP%!MS_1!%C0%
echo    %CP%!MS_HAPPY!%C0%   %CG%✓ CLEAN%C0%   %CD%정리할 레지스트리 항목이 없어요, 깔끔하네요%C0% 
echo    %CP%!MS_3!%C0%
goto :REG_AFTER

:: ---- 백업 되돌리기 (최근 5개) ----
:REG_RESTORE
cls
call :BANNER
echo.
echo   %CC%■%C0% %CW%레지스트리 백업 되돌리기%C0%  %CD%!CL_REG_BAK!%C0%
set "_n=0"
for /f "delims=" %%f in ('dir /b /o-d "%CL_REG_BAK%\registry_*.reg" 2^>nul') do (
    set /a _n+=1
    if !_n! leq 5 set "BK_!_n!=%%f"
)
if !_n! equ 0 (
    echo     %CD%└ 아직 백업이 없어요%C0% 
    goto :REG_AFTER
)
if !_n! gtr 5 set "_n=5"
for /l %%i in (1,1,!_n!) do (
    set "MSG=    !CD!└!C0! !CY![%%i]!C0! !BK_%%i!"
    echo !MSG!
)
set "_pick="
set /p "_pick=!C0!    !CD!└!C0! 번호  !CD!(Enter = 취소)!C0!  !CY!›!C0! " 
if not defined _pick goto :START
set "_file="
for /l %%i in (1,1,!_n!) do if "!_pick!"=="%%i" set "_file=!BK_%%i!"
if not defined _file goto :REG_RESTORE
if "!IS_ADMIN!"=="0" echo   %CY%▲%C0% HKLM 키가 든 백업은 관리자 권한이 있어야 모두 되돌아가요  
reg import "%CL_REG_BAK%\!_file!" >nul 2>&1
if errorlevel 1 (
    echo   !CR!✗!C0! 되돌리지 못했어요 !CD!^(관리자 권한이 필요한 키가 있을 수 있어요^)!C0!
) else (
    echo   !CG!✓!C0! 되돌렸어요  !CD!!_file!!C0! 
)
goto :REG_AFTER

:ELEVATE
powershell -NoProfile -Command "Start-Process -FilePath $env:CL_SELF -Verb RunAs" >nul 2>&1
if errorlevel 1 (
    echo   !CR!✗!C0! 관리자 권한 요청이 취소됐어요 
    goto :%_back%
)
goto :QUIT_SILENT

:ENGINE_FAIL
echo   %CR%✗%C0% 청소 엔진을 실행하지 못했어요 %CD%(PowerShell)%C0%
goto :QUIT

:QUIT
echo.
echo   %CD%  "먼지 한 톨 없는 하루 되세요"%C0% !MS_HAPPY!
echo.
:QUIT_SILENT
endlocal
exit /b 0

:: ======================================================================
:: 서브루틴 
:: ======================================================================
:BANNER
set "_adm=%CY%일반 사용자%C0% %CD%(관리자 항목은 건너뜀)%C0%" 
if "!IS_ADMIN!"=="1" set "_adm=%CG%관리자%C0%" 
echo.
echo   %G1% ██████╗██╗     ███████╗ █████╗ ███╗   ██╗%C0% 
echo   %G2%██╔════╝██║     ██╔════╝██╔══██╗████╗  ██║%C0%     %CP%!MS_1!%C0%
echo   %G3%██║     ██║     █████╗  ███████║██╔██╗ ██║%C0%    %CP%!MS_2!%C0%   %CD%squeak squeak, sweeping~%C0%
echo   %G4%██║     ██║     ██╔══╝  ██╔══██║██║╚██╗██║%C0%     %CP%!MS_3!%C0%
echo   %G5%╚██████╗███████╗███████╗██║  ██║██║ ╚████║%C0% 
echo   %G6% ╚═════╝╚══════╝╚══════╝╚═╝  ╚═╝╚═╝  ╚═══╝%C0% 
echo   %CD%   생쥐 청소부  ·  임시 파일 · 캐시 · 휴지통 정리 🧹%C0%
echo.
echo   %CC%■%C0% %date% %time:~0,8%  %CD%·%C0%  !_adm! 
echo   %RULE%
goto :eof

:SHOW_TABLE
set /a "_mx=1"
for /l %%i in (1,1,%T_COUNT%) do if !B_%%i! gtr !_mx! set "_mx=!B_%%i!"
echo.
echo   %CC%╭─%C0% 🧹 %CW%청소할 거리%C0% %CC%%CARD%%C0%
set /a "_sum_mb=0"
for /l %%i in (1,1,%T_COUNT%) do call :TABLE_ROW %%i
echo   %CC%│%C0%
set "MSG=  !CC!│!C0!  !CW!지울 수 있는 합계!C0!  !CK!!_sum_mb! MB!C0!" & echo !MSG!
echo   %CC%╰%CARD_END%%C0%
goto :eof

:: 표 한 줄 (%1 = 번호)
:TABLE_ROW
set "_i=%~1"
set "_n= %_i%"
set "_box=%CD%[ ]%C0%"
if "!TS_%_i%!"=="1" set "_box=%CG%[✓]%C0%"
if "!TS_%_i%!"=="1" if not "!X_%_i%!"=="-1" set /a "_sum_mb+=B_%_i%"
set "_mbs=        !BS_%_i%!"
set "_mbs=!_mbs:~-8!"
call :BAR !B_%_i%! !_mx! 12 _g
set "_nm=!TP_%_i%!"
set "_note=%CD%!TD_%_i%!%C0%"
if "!X_%_i%!"=="-1" set "_note=%CY%관리자 권한 필요%C0%  %CD%!TD_%_i%!%C0%"
if "!X_%_i%!"=="0" if "!F_%_i%!"=="0" set "_note=%CD%비어 있음 · !TD_%_i%!%C0%"
set "MSG=  !CC!│!C0! !CY!!_n:~-2!!C0! !_box! !_nm! !_mbs!  !_g!  !_note!" 
echo !MSG!
goto :eof

:: 결과 한 줄 (%1 = 번호) 
:RESULT_ROW
set "_i=%~1"
set /a "_tot_mb+=RB_%_i%"
if !RE_%_i%! gtr 0 set /a "_tot_err+=RE_%_i%"
set "_mbs=        !RBS_%_i%!"
set "_mbs=!_mbs:~-8!"
set "_nm=!TP_%_i%!"
set "_st=%CG%✓%C0%" 
set "_note=%CD%파일 !RF_%_i%!개 삭제%C0%"
if not "!RE_%_i%!"=="0" set "_st=%CY%▲%C0%" & set "_note=%CD%파일 !RF_%_i%!개 삭제 · 사용 중 !RE_%_i%!개 건너뜀%C0%" 
if "!RE_%_i%!"=="-1" set "_st=%CY%–%C0%" & set "_note=%CY%관리자 권한이 없어 건너뜀%C0%" 
if "%_i%"=="3" if "!RE_%_i%!"=="0" set "_note=%CD%휴지통 비움%C0%" 
set "MSG=  !CC!│!C0!  !_st! !_nm! !CK!!_mbs!!C0!  !_note!"
echo !MSG!
goto :eof

:: 막대 (%1 = 값 MB, %2 = 최댓값 MB, %3 = 칸 수, %4 = 저장할 변수명)
:: 크기는 엔진이 MB 정수로 준다 (set /a 는 32비트라 바이트로는 2GB 에서 넘침) 
:BAR
set /a "_v=%~1, _m=%~2"
if !_m! lss 1 set "_m=1"
set /a "_on=(_v * %~3 + _m - 1) / _m"
if !_v! gtr 0 if !_on! lss 1 set "_on=1"
if !_on! gtr %~3 set "_on=%~3"
set /a "_off=%~3 - _on"
set "_full=▰▰▰▰▰▰▰▰▰▰▰▰"
set "_empty=▱▱▱▱▱▱▱▱▱▱▱▱" 
for /f "tokens=1,2" %%p in ("!_on! !_off!") do set "%~4=!CK!!_full:~0,%%p!!CD!!_empty:~0,%%q!!C0!"
goto :eof

:: 레지스트리 표 한 줄 (%1 = 범주 번호) 
:REG_ROW
set "_i=%~1"
set "_box=%CD%[ ]%C0%"
if "!RS_%_i%!"=="1" set "_box=%CG%[✓]%C0%" & set /a "_rtot+=RC_%_i%"
set "_cnt=     !RC_%_i%!건"
set "_note=%CD%!RD_%_i%!%C0%"
if "!IS_ADMIN!"=="0" if not "!RH_%_i%!"=="0" set "_note=%CY%관리자 필요 !RH_%_i%!건%C0%  %CD%!RD_%_i%!%C0%" 
set "MSG=  !CC!│!C0! !CY! %_i%!C0! !_box! !RP_%_i%! !_cnt:~-7!  !_note!"
echo !MSG!
goto :eof

:: 레지스트리 결과 한 줄 (%1 = 범주 번호)
:REG_RESULT_ROW
set "_i=%~1"
set /a "_rf+=RF_%_i%, _rx+=RX_%_i%"
set "_st=%CG%✓%C0%" 
set "_note=%CD%!RF_%_i%!건 정리%C0%" 
if not "!RX_%_i%!"=="0" set "_st=%CY%▲%C0%" & set "_note=%CD%!RF_%_i%!건 정리 · !RX_%_i%!건 실패%C0%"
if not "!RA_%_i%!"=="0" set "_note=!_note!  %CY%관리자 필요로 !RA_%_i%!건 건너뜀%C0%" 
set "MSG=  !CC!│!C0!  !_st! !RP_%_i%! !_note!"
echo !MSG!
goto :eof

:CHECK_BROWSERS
set "_run="
for %%p in (chrome msedge brave vivaldi firefox opera) do (
    tasklist /fi "imagename eq %%p.exe" /nh 2>nul | find /i "%%p.exe" >nul && set "_run=!_run! %%p"
)
if defined _run (
    set "MSG=    !CY!▲!C0! 실행 중인 브라우저  !CW!!_run:~1!!C0!  !CD!캐시 일부가 잠겨 있을 수 있어요. 닫고 청소하면 더 많이 비워져요!C0!"
    echo !MSG!
)
goto :eof

:LOAD_STATS
set "ST_TOTAL_MB=0"
set "ST_RUNS=0"
if exist "%CL_STAT_FILE%" for /f "usebackq tokens=1,* delims==" %%a in ("%CL_STAT_FILE%") do set "ST_%%a=%%b"
goto :eof

:SAVE_STATS
if not exist "%CL_STAT_DIR%" mkdir "%CL_STAT_DIR%" >nul 2>&1
> "%CL_STAT_FILE%" (
    echo TOTAL_MB=!ST_TOTAL_MB!
    echo RUNS=!ST_RUNS!
)
goto :eof

:RUN_ENGINE
if exist "%CL_SUM%" del /f /q "%CL_SUM%" >nul 2>&1
powershell -NoProfile -ExecutionPolicy Bypass -Command "$s = [IO.File]::ReadAllText($env:CL_SELF, [Text.Encoding]::UTF8); $m = [Environment]::NewLine + '::PS_ENGINE_BEGIN'; [IO.File]::WriteAllText($env:CL_ENGINE, $s.Substring($s.IndexOf($m) + $m.Length)); & $env:CL_ENGINE"
goto :eof

:: ======================================================================
:: PowerShell 청소 엔진 (cmd 는 여기까지 오지 않는다 - ASCII 전용 코드) 
::  CL_MODE=scan  : 대상별 크기 B_n(MB) BS_n(표시용) · 파일 수 F_n · 상태 X_n (0 가능, -1 관리자 필요)
::  CL_MODE=clean : CL_SEL 의 대상만 지우고 RB_n(비운 MB) RBS_n(표시용) RF_n(지운 파일) RE_n(실패, -1 권한 없음)
:: ======================================================================
::PS_ENGINE_BEGIN
$ErrorActionPreference = 'SilentlyContinue'
[Console]::OutputEncoding = New-Object System.Text.UTF8Encoding $false
$e = [char]27; $C0 = "$e[0m"; $CD = "$e[90m"; $CK = "$e[38;5;121m"
$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
$L = $env:LOCALAPPDATA; $R = $env:APPDATA; $U = $env:USERPROFILE; $W = $env:windir
$cutoff = (Get-Date).AddHours(-24)

function Chromium([string]$base) {
    $out = @()
    if (Test-Path -LiteralPath $base) {
        foreach ($p in Get-ChildItem -LiteralPath $base -Directory | Where-Object { $_.Name -eq 'Default' -or $_.Name -like 'Profile *' }) {
            $out += (Join-Path $p.FullName 'Cache'), (Join-Path $p.FullName 'Code Cache'), (Join-Path $p.FullName 'GPUCache')
        }
    }
    $out
}
function Firefox { $p = "$L\Mozilla\Firefox\Profiles"; if (Test-Path -LiteralPath $p) { @(Get-ChildItem -LiteralPath $p -Directory | ForEach-Object { Join-Path $_.FullName 'cache2' }) } }

# id -> @{ Paths; Filter; Age; Admin; Flat }  ($TG, not $T: PowerShell names ignore case and $t is used below)
$TG = @{}
$TG[1] = @{ Paths = @($env:TEMP); Age = $true }
$TG[2] = @{ Paths = @("$W\Temp"); Age = $true; Admin = $true }
$TG[3] = @{ Recycle = $true }
$TG[4] = @{ Paths = @("$L\Microsoft\Windows\Explorer"); Filter = '*.db'; Flat = $true }
$TG[5] = @{ Paths = @("$W\Minidump", "$L\CrashDumps") }
$TG[6] = @{ Paths = @(@(Chromium "$L\Google\Chrome\User Data") + @(Chromium "$L\Microsoft\Edge\User Data") + @(Chromium "$L\BraveSoftware\Brave-Browser\User Data") + @(Chromium "$L\Vivaldi\User Data") + @(Firefox) + @("$R\Opera Software\Opera Stable\Cache", "$L\Opera Software\Opera Stable\Cache")) }
$TG[7] = @{ Paths = @("$W\SoftwareDistribution\Download"); Admin = $true }
$TG[8] = @{ Paths = @("$W\Prefetch"); Filter = '*.pf'; Flat = $true; Admin = $true }
$TG[9] = @{ Paths = @("$R\Microsoft\Windows\Recent"); Filter = '*.lnk'; Flat = $true }
$TG[10] = @{ Paths = @("$R\npm-cache", "$U\.nuget\packages", "$L\pip\cache", "$U\.gradle\caches", "$U\.m2\repository") }

# files of a target (skips unreadable folders, applies filter / age)
function Get-TargetFiles($t) {
    foreach ($root in $t.Paths) {
        if (-not $root -or -not [IO.Directory]::Exists($root)) { continue }
        $pattern = if ($t.Filter) { $t.Filter } else { '*' }
        $stack = New-Object 'System.Collections.Generic.Stack[string]'; $stack.Push($root)
        while ($stack.Count -gt 0) {
            $dir = $stack.Pop()
            try { foreach ($f in [IO.Directory]::GetFiles($dir, $pattern)) {
                $fi = New-Object IO.FileInfo $f
                if ($t.Age -and $fi.LastWriteTime -gt $cutoff) { continue }
                $fi
            } } catch { }
            if (-not $t.Flat) {
                try { foreach ($s in [IO.Directory]::GetDirectories($dir)) {
                    if (([IO.File]::GetAttributes($s) -band [IO.FileAttributes]::ReparsePoint) -eq 0) { $stack.Push($s) }
                } } catch { }
            }
        }
    }
}

function Get-RecycleSize {
    $sid = [Security.Principal.WindowsIdentity]::GetCurrent().User.Value
    $b = [long]0; $n = 0
    foreach ($d in [IO.DriveInfo]::GetDrives() | Where-Object { $_.DriveType -eq 'Fixed' -and $_.IsReady }) {
        $p = Join-Path $d.RootDirectory.FullName ('$Recycle.Bin\' + $sid)
        if (-not [IO.Directory]::Exists($p)) { continue }
        foreach ($f in Get-ChildItem -LiteralPath $p -Recurse -Force -File) { if ($f.Name -notlike 'desktop.ini') { $b += $f.Length; $n++ } }
    }
    , @($b, $n)
}

function Fmt([long]$b) {
    $c = [Globalization.CultureInfo]::InvariantCulture
    if ($b -ge 1GB) { return ($b / 1GB).ToString('0.0', $c) + ' GB' }
    if ($b -ge 1MB) { return ($b / 1MB).ToString('0', $c) + ' MB' }
    if ($b -gt 0) { return [Math]::Max(1, [int]($b / 1KB)).ToString() + ' KB' }
    return '0'
}
function Show([int]$id, [string]$label) {
    [Console]::Write("`r$e[2K    $CD$label  $id / 10$C0")
}

# ---------------------------------------------------------------- registry (regscan / regfix)
# Categories: 1 App Paths  2 Uninstall entries  3 SharedDLLs  4 Run (startup)
# False-positive guards: quoted/unquoted paths + %env% expanded, bare names / UNC / unplugged
# drives are never judged, System32<->SysWOW64 and Program Files (x86) alternatives checked,
# Uninstall entries only when BOTH install folder and uninstaller are gone.
if ($env:CL_MODE -like 'reg*') {
    $CC = "$e[96m"
    $HK = @{ HKLM = [Microsoft.Win32.Registry]::LocalMachine; HKCU = [Microsoft.Win32.Registry]::CurrentUser }
    $CV = 'SOFTWARE\Microsoft\Windows\CurrentVersion'; $CVW = 'SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion'
    $utf8 = New-Object System.Text.UTF8Encoding $false
    function Get-ExePath([string]$cmd) {
        if (-not $cmd) { return $null }
        $s = [Environment]::ExpandEnvironmentVariables($cmd).Trim()
        if ($s.StartsWith('"')) { $q = $s.IndexOf('"', 1); if ($q -gt 1) { return $s.Substring(1, $q - 1) }; return $s.Trim('"') }
        $m = [regex]::Match($s, '^(.+?\.(exe|dll|com|bat|cmd|scr|cpl|msc|ocx|sys))(?=\s|,|$)', 'IgnoreCase')
        if ($m.Success) { return $m.Groups[1].Value }
        return $s
    }
    function Test-Exists([string]$p) {
        if (-not $p) { return $true }
        $p = $p.Trim().Trim('"').TrimEnd('\')
        if ($p.StartsWith('\??\')) { $p = $p.Substring(4) }
        if ($p -notmatch '^[A-Za-z]:\\') { return $true }
        if (-not [IO.Directory]::Exists([IO.Path]::GetPathRoot($p))) { return $true }
        if ([IO.File]::Exists($p) -or [IO.Directory]::Exists($p)) { return $true }
        foreach ($pair in @(@('\System32\', '\SysWOW64\'), @('\SysWOW64\', '\System32\'), @('\Program Files\', '\Program Files (x86)\'), @('\Program Files (x86)\', '\Program Files\'))) {
            $i = $p.IndexOf($pair[0], [StringComparison]::OrdinalIgnoreCase)
            if ($i -ge 0) {
                $alt = $p.Substring(0, $i) + $pair[1] + $p.Substring($i + $pair[0].Length)
                if ([IO.File]::Exists($alt) -or [IO.Directory]::Exists($alt)) { return $true }
            }
        }
        return $false
    }
    function Open-Key($hive, $path) { try { return $HK[$hive].OpenSubKey($path, $false) } catch { return $null } }
    function Clean([string]$s) { if ($null -eq $s) { return '' }; return ($s -replace "[`t`r`n]", ' ') }

    if ($env:CL_MODE -eq 'regscan') {
        $rows = New-Object 'System.Collections.Generic.List[string]'
        function Add-Issue($cat, $hive, $path, $kind, $name, $label, $missing) { $rows.Add((($cat, $hive, (Clean $path), $kind, (Clean $name), (Clean $label), (Clean $missing)) -join "`t")) }

        foreach ($loc in @(@('HKLM', "$CV\App Paths"), @('HKLM', "$CVW\App Paths"), @('HKCU', "$CV\App Paths"))) {
            $k = Open-Key $loc[0] $loc[1]; if (-not $k) { continue }
            foreach ($sub in $k.GetSubKeyNames()) {
                try { $sk = $k.OpenSubKey($sub); if (-not $sk) { continue }; $v = [string]$sk.GetValue(''); $sk.Close() } catch { continue }
                if (-not $v) { continue }
                $exe = Get-ExePath $v
                if (-not (Test-Exists $exe)) { Add-Issue 1 $loc[0] "$($loc[1])\$sub" 'KEY' '' $sub $exe }
            }
            $k.Close()
        }
        foreach ($loc in @(@('HKLM', "$CV\Uninstall"), @('HKLM', "$CVW\Uninstall"), @('HKCU', "$CV\Uninstall"))) {
            $k = Open-Key $loc[0] $loc[1]; if (-not $k) { continue }
            foreach ($sub in $k.GetSubKeyNames()) {
                try {
                    $sk = $k.OpenSubKey($sub); if (-not $sk) { continue }
                    $dn = [string]$sk.GetValue('DisplayName'); $il = [string]$sk.GetValue('InstallLocation'); $us = [string]$sk.GetValue('UninstallString'); $sk.Close()
                } catch { continue }
                if (-not $dn -or (-not $il -and -not $us)) { continue }
                $ilx = [Environment]::ExpandEnvironmentVariables($il).Trim('"')
                $ilMiss = [bool]$il -and -not (Test-Exists $ilx)
                $exe = Get-ExePath $us
                $usMiss = [bool]$us -and -not (Test-Exists $exe)
                if ((-not $il -or $ilMiss) -and (-not $us -or $usMiss)) { Add-Issue 2 $loc[0] "$($loc[1])\$sub" 'KEY' '' $dn $(if ($usMiss) { $exe } else { $ilx }) }
            }
            $k.Close()
        }
        foreach ($loc in @(@('HKLM', "$CV\SharedDLLs"), @('HKLM', "$CVW\SharedDLLs"))) {
            $k = Open-Key $loc[0] $loc[1]; if (-not $k) { continue }
            foreach ($name in $k.GetValueNames()) { if ($name -and -not (Test-Exists $name)) { Add-Issue 3 $loc[0] $loc[1] 'VALUE' $name ([IO.Path]::GetFileName($name)) $name } }
            $k.Close()
        }
        foreach ($loc in @(@('HKLM', "$CV\Run"), @('HKLM', "$CVW\Run"), @('HKCU', "$CV\Run"))) {
            $k = Open-Key $loc[0] $loc[1]; if (-not $k) { continue }
            foreach ($name in $k.GetValueNames()) {
                if (-not $name) { continue }
                $exe = Get-ExePath ([string]$k.GetValue($name))
                if (-not (Test-Exists $exe)) { Add-Issue 4 $loc[0] $loc[1] 'VALUE' $name $name $exe }
            }
            $k.Close()
        }
        [IO.File]::WriteAllLines($env:CL_REG, $rows, $utf8)

        $kv = New-Object 'System.Collections.Generic.List[string]'
        $view = New-Object 'System.Collections.Generic.List[string]'
        $all = @($rows | ForEach-Object { $c = $_ -split "`t"; New-Object PSObject -Property @{ Cat = [int]$c[0]; Hive = $c[1]; Path = $c[2]; Label = $c[5]; Missing = $c[6] } })
        foreach ($cat in 1..4) {
            $list = @($all | Where-Object { $_.Cat -eq $cat })
            $kv.Add("RC_$cat=$($list.Count)"); $kv.Add("RH_$cat=$(@($list | Where-Object { $_.Hive -eq 'HKLM' }).Count)")
            if ($list.Count -eq 0) { continue }
            $view.Add("  $CC$([char]0x25A0)$C0 $([Environment]::GetEnvironmentVariable("CL_RN_$cat"))  $CD$([char]0x00B7) $($list.Count)$C0")
            foreach ($it in ($list | Select-Object -First 6)) {
                $lab = $it.Label; if ($lab.Length -gt 34) { $lab = $lab.Substring(0, 33) + [char]0x2026 }
                $mis = $it.Missing; if ($mis.Length -gt 58) { $mis = [char]0x2026 + $mis.Substring($mis.Length - 57) }
                $view.Add("    $CD$($it.Hive)$C0  $lab  $CD$([char]0x2192) $mis$C0")
            }
            if ($list.Count -gt 6) { $view.Add("    $CD$([char]0x2026) +$($list.Count - 6)$C0") }
            $view.Add('')
        }
        [IO.File]::WriteAllLines($env:CL_REGVIEW, $view, $utf8)
        [IO.File]::WriteAllLines($env:CL_SUM, $kv, $utf8)
        exit 0
    }

    # ---- regfix: back up everything that will change, then delete
    $sel = @(([string]$env:CL_RSEL).Split(' ', [StringSplitOptions]::RemoveEmptyEntries) | ForEach-Object { [int]$_ })
    $rows = @([IO.File]::ReadAllLines($env:CL_REG, $utf8) | ForEach-Object { $c = $_ -split "`t"; New-Object PSObject -Property @{ Cat = [int]$c[0]; Hive = $c[1]; Path = $c[2]; Kind = $c[3]; Name = $c[4] } } | Where-Object { $sel -contains $_.Cat })
    $stat = @{}; foreach ($cat in 1..4) { $stat[$cat] = @{ F = 0; X = 0; A = 0 } }
    $work = New-Object 'System.Collections.Generic.List[object]'
    foreach ($r in $rows) { if ($r.Hive -eq 'HKLM' -and -not $isAdmin) { $stat[$r.Cat].A++ } else { $work.Add($r) } }

    $kv = New-Object 'System.Collections.Generic.List[string]'
    if ($work.Count -gt 0) {
        $keys = New-Object 'System.Collections.Generic.List[string]'
        foreach ($r in $work) {
            $full = "$($r.Hive)\$($r.Path)"; if (-not $keys.Contains($full)) { $keys.Add($full) }
            if ($r.Cat -eq 4) { foreach ($sa in 'Run', 'Run32') { $p = "$($r.Hive)\$CV\Explorer\StartupApproved\$sa"; if ((Open-Key $r.Hive "$CV\Explorer\StartupApproved\$sa") -and -not $keys.Contains($p)) { $keys.Add($p) } } }
        }
        [void][IO.Directory]::CreateDirectory($env:CL_REG_BAK)
        $bak = Join-Path $env:CL_REG_BAK ('registry_' + (Get-Date).ToString('yyyyMMdd_HHmmss') + '.reg')
        $sb = New-Object System.Text.StringBuilder
        [void]$sb.Append("Windows Registry Editor Version 5.00`r`n")
        $failed = $false
        foreach ($key in $keys) {
            $tmp = [IO.Path]::GetTempFileName()
            & reg.exe export $key $tmp /y 2>&1 | Out-Null
            if ($LASTEXITCODE -ne 0) { $failed = $true; break }
            $txt = [IO.File]::ReadAllText($tmp, [Text.Encoding]::Unicode); [IO.File]::Delete($tmp)
            $nl = $txt.IndexOf("`r`n"); if ($nl -ge 0) { $txt = $txt.Substring($nl + 2) }
            [void]$sb.Append($txt)
        }
        if ($failed) { [IO.File]::WriteAllLines($env:CL_SUM, @('BACKUP_FAIL=1'), $utf8); exit 0 }
        [IO.File]::WriteAllText($bak, $sb.ToString(), [Text.Encoding]::Unicode)
        if (-not (Test-Path -LiteralPath $bak) -or (New-Object IO.FileInfo $bak).Length -lt 64) { [IO.File]::WriteAllLines($env:CL_SUM, @('BACKUP_FAIL=1'), $utf8); exit 0 }
        $kv.Add("BACKUP=$bak")

        foreach ($r in $work) {
            try {
                if ($r.Kind -eq 'KEY') {
                    $HK[$r.Hive].DeleteSubKeyTree($r.Path, $false)
                } else {
                    $k = $HK[$r.Hive].OpenSubKey($r.Path, $true); $k.DeleteValue($r.Name, $false); $k.Close()
                    if ($r.Cat -eq 4) {
                        foreach ($sa in 'Run', 'Run32') { try { $k = $HK[$r.Hive].OpenSubKey("$CV\Explorer\StartupApproved\$sa", $true); if ($k) { $k.DeleteValue($r.Name, $false); $k.Close() } } catch { } }
                    }
                }
                $stat[$r.Cat].F++
            } catch { $stat[$r.Cat].X++ }
        }
    }
    foreach ($cat in 1..4) { $kv.Add("RF_$cat=$($stat[$cat].F)"); $kv.Add("RX_$cat=$($stat[$cat].X)"); $kv.Add("RA_$cat=$($stat[$cat].A)") }
    [IO.File]::WriteAllLines($env:CL_SUM, $kv, $utf8)
    exit 0
}

$kv = New-Object 'System.Collections.Generic.List[string]'
if ($env:CL_MODE -eq 'scan') {
    foreach ($id in 1..10) {
        Show $id $env:CL_L_SCAN
        $t = $TG[$id]
        if ($t.Admin -and -not $isAdmin) {
            $x = -1
            $b = [long]0; $n = 0
            foreach ($f in Get-TargetFiles $t) { $b += $f.Length; $n++ }
        } elseif ($t.Recycle) {
            $r = Get-RecycleSize; $b = $r[0]; $n = $r[1]; $x = 0
        } else {
            $b = [long]0; $n = 0; $x = 0
            foreach ($f in Get-TargetFiles $t) { $b += $f.Length; $n++ }
        }
        $kv.Add("B_$id=" + [long][Math]::Floor($b / 1MB)); $kv.Add("BS_$id=" + (Fmt $b)); $kv.Add("F_$id=$n"); $kv.Add("X_$id=$x")
    }
} else {
    $total = [long]0
    $sel = @(([string]$env:CL_SEL).Split(' ', [StringSplitOptions]::RemoveEmptyEntries) | ForEach-Object { [int]$_ })
    foreach ($id in $sel) {
        Show $id $env:CL_L_CLEAN
        $t = $TG[$id]; $freed = [long]0; $done = 0; $fail = 0
        if ($t.Admin -and -not $isAdmin) {
            $fail = -1
        } elseif ($t.Recycle) {
            $r = Get-RecycleSize
            try { Clear-RecycleBin -Force -ErrorAction Stop; $freed = $r[0]; $done = $r[1] } catch { if ($r[1] -gt 0) { $fail = 1 } }
        } else {
            foreach ($f in @(Get-TargetFiles $t)) {
                try {
                    if ($f.IsReadOnly) { $f.IsReadOnly = $false }
                    $len = $f.Length; $f.Delete(); $freed += $len; $done++
                } catch { $fail++ }
            }
            if (-not $t.Flat) {
                foreach ($root in $t.Paths) {
                    if (-not $root -or -not [IO.Directory]::Exists($root)) { continue }
                    $dirs = @(Get-ChildItem -LiteralPath $root -Recurse -Directory -Force | Sort-Object { $_.FullName.Length } -Descending)
                    foreach ($d in $dirs) { try { if (-not [IO.Directory]::EnumerateFileSystemEntries($d.FullName).GetEnumerator().MoveNext()) { $d.Delete() } } catch { } }
                }
            }
        }
        $total += $freed; $kv.Add("RB_$id=" + [long][Math]::Floor($freed / 1MB)); $kv.Add("RBS_$id=" + (Fmt $freed)); $kv.Add("RF_$id=$done"); $kv.Add("RE_$id=$fail")
    }
    $kv.Add("TOTS=" + (Fmt $total))
}
[Console]::Write("`r$e[2K")
[IO.File]::WriteAllLines($env:CL_SUM, $kv, (New-Object System.Text.UTF8Encoding $false))
