@echo off
chcp 65001 >nul
setlocal enabledelayedexpansion

:: ======================================================================
::  LOTTO 6/45 - 복돼지 로또 추첨기 
::
::  1~45 중 6개 + 보너스 1개를 추첨기로 뽑는다  
::   · [1] 행운 번호 1게임   추첨기 회전 연출 + 번호 분석 
::   · [2] 자동 5게임        로또 용지 한 장 (저장)
::   · [3] 반자동 5게임      고정 번호 + 자동 
::   · [4] 모의 추첨         저장한 용지로 1~5등 당첨 확인  
::  공 색은 실제 로또와 같다: 1~10 노랑 · 11~20 파랑 · 21~30 빨강 · 31~40 회색 · 41~45 초록 
::
::  콘솔 테마: .claude/docs/cmd-console-style.md (마스코트: 복돼지)
::  주의: 멀티바이트가 든 줄은 멀티바이트로 끝나지 않게, 짝수 바이트로 끝낸다  
::  주의: 지연 확장 상태이므로 문구에 느낌표를 쓰지 않는다 
::  주의: 캐럿이 든 이모티콘은 느낌표가 없는 set 줄에서만 정의한다 
:: ======================================================================

title Lotto 6/45

:: ----------------------------------------------------------------------
:: 콘솔 테마 (G1~G6 배너: 로또 공 색 노랑 → 파랑 → 빨강 → 회색 → 초록)
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
set "CM=%ESC%[38;5;213m"
set "G1=%ESC%[38;5;227m"
set "G2=%ESC%[38;5;221m"
set "G3=%ESC%[38;5;75m"
set "G4=%ESC%[38;5;203m"
set "G5=%ESC%[38;5;250m"
set "G6=%ESC%[38;5;114m"
set "D_Y=%ESC%[38;5;220m●%C0%"
set "D_B=%ESC%[38;5;33m●%C0%" 
set "D_R=%ESC%[38;5;203m●%C0%"
set "D_G=%ESC%[38;5;245m●%C0%"
set "D_E=%ESC%[38;5;71m●%C0%" 
set "CUR_OFF=%ESC%[?25l"
set "CUR_ON=%ESC%[?25h"
set "RULE=%CD%──────────────────────────────────────────────────────────────────%C0%" 
set "CARD=──────────────────────────────────────────────" 
set "CARD_END=──────────────────────────────────────────────────────────────" 

:: 복돼지 (캐럿 포함 - 이 줄들에는 느낌표를 쓰지 않는다) 
set "PIG_1=  ^   ^"
set "PIG_2= (・(oo)・)" 
set "PIG_3=  U   U"
set "PIG_HAPPY=(≧(oo)≦)"
set "PIG_WOW=(ﾟ(oo)ﾟ)"
set "PIG_SAD=(╥(oo)╥)"
set "PIG_WINK=(・(oo)-)" 

:: 복돼지 한마디  
set "TALK_1=꿀꿀, 이 번호 느낌 좋은데요" 
set "TALK_2=어젯밤 돼지꿈 꾸셨죠? 딱 그 번호예요"
set "TALK_3=코끝이 찡긋, 행운 냄새가 납니다" 
set "TALK_4=일단 사면 반은 된 거래요, 꿀꿀" 

:: 기록 파일  
set "REC_DIR=%LOCALAPPDATA%\Playground\Lotto"
set "TICKET_FILE=%REC_DIR%\ticket.txt"
set "LAST_FILE=%REC_DIR%\last.txt"
set "STAT_FILE=%REC_DIR%\stats.ini"
if not exist "%REC_DIR%" mkdir "%REC_DIR%" >nul 2>&1

call :CALIBRATE

:: ======================================================================
:: 타이틀  
:: ======================================================================
:TITLE
call :LOAD_STATS
set "TICKET_TXT=없음" 
if exist "%TICKET_FILE%" set "TICKET_TXT=저장됨" 
cls
echo.
echo   %G1%██╗      ██████╗ ████████╗████████╗ ██████╗ %C0% 
echo   %G2%██║     ██╔═══██╗╚══██╔══╝╚══██╔══╝██╔═══██╗%C0% 
echo   %G3%██║     ██║   ██║   ██║      ██║   ██║   ██║%C0% 
echo   %G4%██║     ██║   ██║   ██║      ██║   ██║   ██║%C0% 
echo   %G5%███████╗╚██████╔╝   ██║      ██║   ╚██████╔╝%C0% 
echo   %G6%╚══════╝ ╚═════╝    ╚═╝      ╚═╝    ╚═════╝ %C0% 
echo   %CD%   로또 6/45 추첨기  ·  복돼지가 골라 주는 행운 번호 🍀%C0%
echo.
echo   %CC%■%C0% %date% %time:~0,8% 
echo.
echo    %CP%!PIG_1!%C0%      🐷 %CD%복돼지%C0%     "꿀꿀, 오늘 돼지꿈 꾸셨어요?"
echo    %CP%!PIG_2!%C0%    🎱 %CD%누적 추첨%C0%  %CW%!ST_DRAWS!%C0%회  %CD%·%C0%  %CD%모의 추첨 최고%C0%  %CY%!BEST_TXT!%C0%
echo    %CP%!PIG_3!%C0%      🎫 %CD%내 용지%C0%    !TICKET_TXT!
echo.
echo   %RULE%
echo.
echo   %CC%■%C0% %CW%메뉴%C0% 
echo     %CD%└%C0% %CY%[1]%C0% 행운 번호 1게임     %CD%추첨기 연출 + 번호 분석%C0% 
echo     %CD%└%C0% %CY%[2]%C0% 자동 5게임          %CD%로또 용지 한 장%C0%
echo     %CD%└%C0% %CY%[3]%C0% 반자동 5게임        %CD%고정 번호 + 자동%C0%
echo     %CD%└%C0% %CY%[4]%C0% 모의 추첨           %CD%내 용지로 당첨 확인%C0%
echo     %CD%└%C0% %CY%[0]%C0% 종료 
echo.
choice /C 12340 /N /M "   선택 > "
if errorlevel 5 goto :QUIT
if errorlevel 4 goto :MOCK
if errorlevel 3 goto :SEMI5
if errorlevel 2 goto :AUTO5
goto :ONE_GAME

:: ======================================================================
:: [1] 행운 번호 1게임  
:: ======================================================================
:ONE_GAME
call :MINI_HEADER "행운 번호 1게임" 
call :DRAW_ANIM
call :SORT6
call :CHIPS_SORTED LINE
call :BALL !D7! _bc
echo.
echo   %CC%■%C0% %CW%오늘의 행운 번호%C0%  %CD%작은 수부터%C0%
echo.
echo       !LINE!  %CD%+%C0%  !_bc!
echo.
call :ANALYZE
call :SAVE_LAST
set /a ST_DRAWS+=1
call :SAVE_STATS

set /a "_r=!random! %% 4 + 1"
for %%r in (!_r!) do set "TALK=!TALK_%%r!"
echo   %CC%╭─%C0% %CW%번호 분석%C0% %CC%%CARD%%C0%
echo   %CC%│%C0%  %CD%합계%C0%        %CW%!A_SUM!%C0%   %CD%(자주 나오는 구간 100~175)%C0% 
echo   %CC%│%C0%  %CD%홀 : 짝%C0%     !A_ODD! : !A_EVEN!
echo   %CC%│%C0%  %CD%저 : 고%C0%     !A_LOW! : !A_HIGH!   %CD%(1~22 : 23~45)%C0% 
echo   %CC%│%C0%  %CD%번호대%C0%      !A_BANDS!
echo   %CC%│%C0%  %CD%지난 추첨%C0%   !A_OVERLAP! 
echo   %CC%╰%CARD_END%%C0%
echo.
echo    %CP%!PIG_1!%C0%
echo    %CP%!PIG_HAPPY!%C0%   %CG%✓ LUCKY%C0% 🍀   %CD%!TALK!%C0% 
echo    %CP%!PIG_3!%C0%
goto :AFTER

:: ======================================================================
:: [2] 자동 5게임 / [3] 반자동 5게임 
:: ======================================================================
:AUTO5
call :CLEAR_FIX
call :MINI_HEADER "자동 5게임"
goto :MAKE_TICKET

:SEMI5
call :MINI_HEADER "반자동 5게임" 
:SEMI_ASK
echo.
echo   %CC%■%C0% %CW%고정 번호%C0%  %CD%1~45 중 최대 5개, 공백이나 쉼표로 구분 (예: 7 17 27)%C0%
set "FIXIN="
set /p "FIXIN=!C0!    !CD!└!C0! 고정할 번호  !CY!›!C0! " 
if not defined FIXIN goto :SEMI_ASK
call :PARSE_FIXED
if not defined BAD goto :MAKE_TICKET
echo   !CY!▲!C0! !BAD!
goto :SEMI_ASK

:MAKE_TICKET
echo.
echo   %CC%╭─%C0% 🎫 %CW%로또 6/45 용지%C0%  %CD%·  %date%%C0% %CC%────────────────────────────%C0%
set "ROWNAME=ABCDE"
if exist "%TICKET_FILE%" del /f /q "%TICKET_FILE%" >nul 2>&1
for /l %%r in (1,1,5) do (
    set /a "_ri=%%r-1"
    for %%i in (!_ri!) do set "_rn=!ROWNAME:~%%i,1!"
    set "_kind=자동  "
    if !FIX_CNT! gtr 0 set "_kind=반자동"
    call :GEN_ROW
    call :SORT6
    <nul set /p "=%C0%  %CC%│%C0%  %CW%!_rn!%C0%  %CD%!_kind!%C0%   " 
    for /l %%k in (1,1,6) do (
        set "_u="
        for %%n in (!S%%k!) do if not "!FIX_LIST: %%n =!"=="!FIX_LIST!" set "_u=U"
        call :BALL !S%%k! _c !_u!
        <nul set /p "=%C0%!_c! "
        call :SLEEP 70
    )
    echo.
    >> "%TICKET_FILE%" echo !S1! !S2! !S3! !S4! !S5! !S6!
)
echo   %CC%╰%CARD_END%%C0%
if !FIX_CNT! gtr 0 echo   %CD%   밑줄 = 고정 번호%C0% 
set /a ST_DRAWS+=5
call :SAVE_STATS
echo.
echo    %CP%!PIG_1!%C0%
echo    %CP%!PIG_WINK!%C0%   %CG%✓ SAVED%C0% 🎫   %CD%용지를 저장했어요. [4] 모의 추첨으로 맞춰 보세요%C0% 
echo    %CP%!PIG_3!%C0%
goto :AFTER

:: ======================================================================
:: [4] 모의 추첨 - 저장한 용지로 당첨 확인  
:: ======================================================================
:MOCK
call :MINI_HEADER "모의 추첨" 
if exist "%TICKET_FILE%" goto :MOCK_LOAD
echo.
echo   %CY%▲%C0% 저장한 용지가 없어 자동 5게임 용지를 먼저 만듭니다  
call :CLEAR_FIX
for /l %%r in (1,1,5) do (
    call :GEN_ROW
    call :SORT6
    >> "%TICKET_FILE%" echo !S1! !S2! !S3! !S4! !S5! !S6!
)

:MOCK_LOAD
set "_r=0"
for /f "usebackq tokens=1-6" %%a in ("%TICKET_FILE%") do (
    set /a _r+=1
    set "T!_r!= %%a %%b %%c %%d %%e %%f "
)
call :DRAW_ANIM
call :SORT6
call :CHIPS_SORTED LINE
call :BALL !D7! _bc
set "WIN= !S1! !S2! !S3! !S4! !S5! !S6! "
set "BONUS=!D7!"
call :SAVE_LAST
set /a ST_DRAWS+=1
echo.
echo   %CC%■%C0% %CW%당첨 번호%C0%
echo.
echo       !LINE!  %CD%+%C0%  !_bc!
echo.
echo   %CC%╭─%C0% 🎫 %CW%내 용지 확인%C0% %CC%%CARD%%C0% 
set "ROWNAME=ABCDE"
set "BEST_NOW=9"
for /l %%r in (1,1,5) do (
    set /a "_ri=%%r-1"
    for %%i in (!_ri!) do set "_rn=!ROWNAME:~%%i,1!"
    set "_hit=0"
    set "_bon=0"
    set "_row=!T%%r!"
    set "_chips="
    for %%n in (!_row!) do (
        if not "!WIN: %%n =!"=="!WIN!" (
            set /a _hit+=1
            call :BALL %%n _c
        ) else if "%%n"=="!BONUS!" (
            set "_bon=1"
            call :BALL %%n _c U
        ) else (
            call :DIMBALL %%n _c
        )
        set "_chips=!_chips!!_c! "
    )
    call :RANK
    <nul set /p "=%C0%  %CC%│%C0%  %CW%!_rn!%C0%   !_chips!  "
    echo !RANK_TXT!
    call :SLEEP 350
)
echo   %CC%╰%CARD_END%%C0%
echo   %CD%   밑줄 = 보너스 번호 일치  ·  1등 확률 1/8,145,060%C0% 
echo.
if !BEST_NOW! lss !ST_BEST! set "ST_BEST=!BEST_NOW!"
call :SAVE_STATS
if !BEST_NOW! leq 3 goto :MOCK_BIG
if !BEST_NOW! leq 5 goto :MOCK_SMALL
echo    %CP%!PIG_1!%C0%
echo    %CP%!PIG_SAD!%C0%   %CY%▲ 낙첨%C0%   %CD%괜찮아요, 다음 주에는 꼭 꿀꿀%C0%
echo    %CP%!PIG_3!%C0%
goto :AFTER

:MOCK_SMALL
echo    %CP%!PIG_1!%C0%
echo    %CP%!PIG_HAPPY!%C0%   %CG%✓ 당첨%C0% 🍀   %CD%!BEST_NOW!등 당첨, 복돼지 코가 씰룩씰룩%C0% 
echo    %CP%!PIG_3!%C0%
goto :AFTER

:MOCK_BIG
call :FLASH_BANNER
call :FIREWORKS
call :FIREWORKS
echo.
echo    %CP%!PIG_1!%C0%
echo    %CP%!PIG_WOW!%C0%   %CM%✓ JACKPOT%C0% 🎉   %CD%!BEST_NOW!등 당첨, 복돼지가 기절했습니다%C0%
echo    %CP%!PIG_3!%C0%
goto :AFTER

:: ======================================================================
:: 공통 마무리 
:: ======================================================================
:AFTER
echo.
echo   %RULE%
echo.
choice /C 10 /N /M "   [1] 메뉴로   [0] 종료 > " 
if errorlevel 2 goto :QUIT
goto :TITLE

:QUIT
<nul set /p "=%CUR_ON%"
echo.
echo   %CD%  "행운은 준비된 사람에게 온대요, 꿀꿀"%C0% !PIG_WINK!
echo.
endlocal
exit /b 0

:: ======================================================================
:: 서브루틴: 작은 머리글 (%1 = 화면 이름)
:: ======================================================================
:MINI_HEADER
cls
echo.
echo   🎱 %CW%LOTTO 6/45%C0%  %CD%·%C0%  %~1  %CD%·%C0%  %CD%%date%%C0% 
echo   %RULE%
goto :eof

:: ======================================================================
:: 서브루틴: 추첨기 애니메이션 (D1~D6 + 보너스 D7) 
::  윗줄 = 뽑힌 공 트레이, 아랫줄 = 회전하는 추첨기 
:: ======================================================================
:DRAW_ANIM
for /l %%n in (1,1,45) do set "USED_%%n="
set "TRAY="
echo.
echo   🎱 %CW%추첨기 가동%C0%  %CD%45개의 공이 힘차게 돌아갑니다%C0% 
echo.
echo     %CD%· · · · · ·  +  ·%C0% 
<nul set /p "=%CUR_OFF%    "
for /l %%i in (1,1,7) do (
    for /l %%f in (1,1,9) do (
        set "TUMB="
        for /l %%k in (1,1,5) do (
            set /a "_r=!random! %% 45 + 1"
            call :BALL !_r! _c
            set "TUMB=!TUMB!!_c! "
        )
        <nul set /p "=%ESC%[1G%ESC%[2K    🌀 !TUMB!"
        set /a "_ms=25 + %%f * 9"
        call :SLEEP !_ms!
    )
    call :PICK_UNIQUE D%%i
    call :BALL !D%%i! _c
    if %%i equ 7 (set "TRAY=!TRAY! %CD%+%C0%  !_c!") else (set "TRAY=!TRAY!!_c! ")
    <nul set /p "=%ESC%[1A%ESC%[1G%ESC%[2K    !TRAY!%ESC%[1B%ESC%[1G%ESC%[2K"
    if %%i equ 7 (
        <nul set /p "=    🎉 %CY%보너스 공%C0%  !_c!" 
    ) else (
        <nul set /p "=    🎉 %CW%%%i번째 공%C0%  !_c!" 
    )
    call :SLEEP 520
)
<nul set /p "=%ESC%[1G%ESC%[2K    %CG%✓%C0% 추첨 완료%CUR_ON%"
echo.
goto :eof

:: ======================================================================
:: 서브루틴: 겹치지 않는 번호 하나 (%1 = 저장할 변수명)
:: ======================================================================
:PICK_UNIQUE
set /a "_p=!random! %% 45 + 1"
if defined USED_%_p% goto :PICK_UNIQUE
set "USED_%_p%=1"
set "%~1=%_p%"
goto :eof

:: ======================================================================
:: 서브루틴: 용지 한 줄 (고정 번호 FIX_1..n + 나머지 자동 → D1~D6) 
:: ======================================================================
:GEN_ROW
for /l %%n in (1,1,45) do set "USED_%%n="
set "_g=0"
for /l %%f in (1,1,5) do if defined FIX_%%f (
    set /a _g+=1
    for %%g in (!_g!) do set "D%%g=!FIX_%%f!"
    for %%v in (!FIX_%%f!) do set "USED_%%v=1"
)
:GEN_ROW_LOOP
if !_g! geq 6 goto :eof
set /a _g+=1
call :PICK_UNIQUE D!_g!
goto :GEN_ROW_LOOP

:: ======================================================================
:: 서브루틴: 고정 번호 해석 (FIXIN → FIX_1..FIX_n, FIX_CNT, FIX_LIST / 오류는 BAD) 
:: ======================================================================
:PARSE_FIXED
set "BAD="
call :CLEAR_FIX
set "FIXIN=!FIXIN:,= !"
for %%x in (!FIXIN!) do if not defined BAD (
    set "_x=%%x"
    for /f "delims=0123456789" %%z in ("%%x") do set "BAD=숫자만 입력하세요: %%x" 
    if not defined BAD if not "!_x:~2,1!"=="" set "BAD=1~45 사이만 가능해요: %%x"
    if not defined BAD (
        set /a "_v=100!_x! %% 100"
        if !_v! lss 1 set "BAD=1~45 사이만 가능해요: %%x"
        if !_v! gtr 45 set "BAD=1~45 사이만 가능해요: %%x" 
    )
    if not defined BAD for %%v in (!_v!) do if not "!FIX_LIST: %%v =!"=="!FIX_LIST!" set "BAD=같은 번호가 두 번 있어요: %%x"
    if not defined BAD (
        set /a FIX_CNT+=1
        if !FIX_CNT! gtr 5 set "BAD=고정 번호는 최대 5개예요" 
        for %%c in (!FIX_CNT!) do set "FIX_%%c=!_v!"
        set "FIX_LIST=!FIX_LIST!!_v! "
    )
)
goto :eof


:CLEAR_FIX
set "FIX_CNT=0"
set "FIX_LIST= "
for /l %%f in (1,1,6) do set "FIX_%%f="
goto :eof

:: ======================================================================
:: 서브루틴: 정렬 (D1~D6 → S1~S6) 
:: ======================================================================
:SORT6
for /l %%i in (1,1,6) do set "S%%i=!D%%i!"
for /l %%a in (1,1,5) do for /l %%i in (1,1,5) do (
    set /a "_j=%%i+1"
    for %%j in (!_j!) do if !S%%i! gtr !S%%j! (
        set "_t=!S%%i!"
        set "S%%i=!S%%j!"
        set "S%%j=!_t!"
    )
)
goto :eof

:: ======================================================================
:: 서브루틴: 공 칩 (%1 = 번호, %2 = 저장할 변수명, %3 = U 면 밑줄) 
::  1~10 노랑 · 11~20 파랑 · 21~30 빨강 · 31~40 회색 · 41~45 초록 
:: ======================================================================
:BALL
set /a "_n=%~1"
set "_bg=220"
set "_fg=30"
if %_n% gtr 10 set "_bg=33" & set "_fg=97"
if %_n% gtr 20 set "_bg=203" & set "_fg=97"
if %_n% gtr 30 set "_bg=245" & set "_fg=30"
if %_n% gtr 40 set "_bg=71" & set "_fg=97"
set "_t=0%_n%"
set "_sg=1;%_fg%"
if /i "%~3"=="U" set "_sg=1;4;%_fg%"
set "%~2=%ESC%[%_sg%;48;5;%_bg%m %_t:~-2% %C0%"
goto :eof

:DIMBALL
set "_t=0%~1"
set "%~2=%ESC%[38;5;244;48;5;236m %_t:~-2% %C0%"
goto :eof

:CHIPS_SORTED
set "_line="
for /l %%k in (1,1,6) do (
    call :BALL !S%%k! _c
    set "_line=!_line!!_c! "
)
set "%~1=!_line!"
goto :eof

:: ======================================================================
:: 서브루틴: 번호 분석 (S1~S6 → A_SUM A_ODD A_EVEN A_LOW A_HIGH A_BANDS A_OVERLAP)
:: ======================================================================
:ANALYZE
set /a "A_SUM=0, A_ODD=0, A_LOW=0, _b1=0, _b2=0, _b3=0, _b4=0, _b5=0"
for /l %%k in (1,1,6) do (
    set /a "A_SUM+=S%%k, _m=S%%k %% 2"
    if !_m! equ 1 set /a A_ODD+=1
    if !S%%k! leq 22 set /a A_LOW+=1
    set /a "_bi=(S%%k - 1) / 10 + 1"
    set /a "_b!_bi!+=1"
)
set /a "A_EVEN=6-A_ODD, A_HIGH=6-A_LOW"
set "A_BANDS="
call :BAND_DOTS !_b1! D_Y
call :BAND_DOTS !_b2! D_B
call :BAND_DOTS !_b3! D_R
call :BAND_DOTS !_b4! D_G
call :BAND_DOTS !_b5! D_E
set "A_OVERLAP=%CD%기록 없음%C0%" 
if not exist "%LAST_FILE%" goto :eof
set "PREV= "
for /f "usebackq tokens=1-6" %%a in ("%LAST_FILE%") do set "PREV= %%a %%b %%c %%d %%e %%f "
set "_ov="
set "_ovn=0"
for /l %%k in (1,1,6) do for %%n in (!S%%k!) do if not "!PREV: %%n =!"=="!PREV!" (
    set /a _ovn+=1
    set "_ov=!_ov! %%n"
)
if !_ovn! equ 0 (set "A_OVERLAP=%CD%겹친 번호 없음%C0%") else (set "A_OVERLAP=%CY%!_ov:~1!%C0%  %CD%(!_ovn!개 겹침)%C0%")
goto :eof

:BAND_DOTS
set "_s="
for /l %%i in (1,1,6) do if %%i leq %~1 set "_s=!_s!!%~2!"
if %~1 equ 0 set "_s=%CD%·%C0%"
set "A_BANDS=!A_BANDS!!_s!  "
goto :eof

:: ======================================================================
:: 서브루틴: 등수 (_hit, _bon → RANK_TXT, BEST_NOW 갱신)
:: ======================================================================
:RANK
set "_rk=9"
if !_hit! equ 3 set "_rk=5"
if !_hit! equ 4 set "_rk=4"
if !_hit! equ 5 set "_rk=3"
if !_hit! equ 5 if !_bon! equ 1 set "_rk=2"
if !_hit! equ 6 set "_rk=1"
if !_rk! lss !BEST_NOW! set "BEST_NOW=!_rk!"
set "RANK_TXT=%CD%낙첨  !_hit!개 일치%C0%" 
if !_rk! equ 5 set "RANK_TXT=%CG%5등%C0%  %CD%5,000원%C0%"
if !_rk! equ 4 set "RANK_TXT=%CG%4등%C0%  %CD%50,000원%C0%" 
if !_rk! equ 3 set "RANK_TXT=%CY%3등%C0%  %CD%약 150만 원%C0% 🎉" 
if !_rk! equ 2 set "RANK_TXT=%CM%2등%C0%  %CD%약 5천만 원%C0% 🎉🎉"
if !_rk! equ 1 set "RANK_TXT=%CM%1등%C0%  %CW%약 20억 원%C0% 🎆🎆🎆"
goto :eof

:: ======================================================================
:: 서브루틴: 당첨 축하 (배너 색 순환 + 불꽃) 
:: ======================================================================
:FLASH_BANNER
echo.
<nul set /p "=%CUR_OFF%"
for /l %%f in (1,1,8) do (
    set /a "_k=%%f %% 6 + 1"
    for %%k in (!_k!) do set "HC=!G%%k!"
    echo   !HC!██╗      ██████╗ ████████╗████████╗ ██████╗ !C0! 
    echo   !HC!██║     ██╔═══██╗╚══██╔══╝╚══██╔══╝██╔═══██╗!C0! 
    echo   !HC!██║     ██║   ██║   ██║      ██║   ██║   ██║!C0! 
    echo   !HC!██║     ██║   ██║   ██║      ██║   ██║   ██║!C0! 
    echo   !HC!███████╗╚██████╔╝   ██║      ██║   ╚██████╔╝!C0! 
    echo   !HC!╚══════╝ ╚═════╝    ╚═╝      ╚═╝    ╚═════╝ !C0! 
    call :SLEEP 120
    if %%f lss 8 <nul set /p "=%ESC%[6A"
)
<nul set /p "=%CUR_ON%"
goto :eof

:FIREWORKS
set "FW="
for /l %%k in (1,1,7) do (
    set /a "_sp=!random! %% 7 + 2, _e=!random! %% 4"
    for %%s in (!_sp!) do set "FW=!FW!!SPC:~0,%%s!"
    if !_e! equ 0 set "FW=!FW!🎆" 
    if !_e! equ 1 set "FW=!FW!✨"
    if !_e! equ 2 set "FW=!FW!🎉" 
    if !_e! equ 3 set "FW=!FW!🍀" 
)
echo  !FW!
call :SLEEP 220
goto :eof

:: ======================================================================
:: 서브루틴: 기록 (%LOCALAPPDATA%\Playground\Lotto) 
:: ======================================================================
:SAVE_LAST
> "%LAST_FILE%" echo !S1! !S2! !S3! !S4! !S5! !S6! !D7!
goto :eof

:LOAD_STATS
set "ST_DRAWS=0"
set "ST_BEST=9"
if exist "%STAT_FILE%" for /f "usebackq tokens=1,* delims==" %%a in ("%STAT_FILE%") do set "ST_%%a=%%b"
set "BEST_TXT=-"
if !ST_BEST! lss 9 set "BEST_TXT=!ST_BEST!등"
goto :eof

:SAVE_STATS
> "%STAT_FILE%" (
    echo DRAWS=!ST_DRAWS!
    echo BEST=!ST_BEST!
)
goto :eof

:: ======================================================================
:: 서브루틴: 지연 (baseball.cmd 와 동일 - 루프 속도 측정 보정) 
:: ======================================================================
:SLEEP
set /a "_n=%~1 * LOOP_PER_MS"
for /l %%i in (1,1,%_n%) do rem
goto :eof

:CALIBRATE
set "SPC=                    "
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
