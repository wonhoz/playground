@echo off
chcp 65001 >nul
setlocal enabledelayedexpansion

:: ======================================================================
::  LUCKY CAT SLOT - 복고양이 슬롯머신  
::
::  3릴 슬롯머신. 가운데 줄(▶ ◀)에 같은 그림이 서면 당첨 
::   · [1] 1코인  [2] 5코인  [3] 10코인  [4] 자동 10회(1코인)  [5] 배당표  
::   · 릴은 왼쪽부터 차례로 멈추고, 앞의 두 릴이 같으면 "리치" 로 마지막 릴이 더 천천히 돈다 
::   · 코인이 떨어지면 복고양이가 20코인을 빌려준다 (이자는 츄르)
::   · 코인·최고 당첨·잭팟 횟수 저장 (%LOCALAPPDATA%\Playground\Slot)
::
::  콘솔 테마: .claude/docs/cmd-console-style.md (마스코트: 복고양이) 
::  주의: 멀티바이트가 든 줄은 멀티바이트로 끝나지 않게, 짝수 바이트로 끝낸다  
::  주의: 지연 확장 상태이므로 문구에 느낌표를 쓰지 않는다 
::  주의: 캐럿이 든 이모티콘은 느낌표가 없는 set 줄에서만 정의한다 
:: ======================================================================

title Lucky Cat Slot

:: ----------------------------------------------------------------------
:: 콘솔 테마 (G1~G6 배너: 금색 → 주황 → 자홍, 카지노 네온) 
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
set "CK=%ESC%[38;5;220m"
set "BG=%ESC%[48;5;237m"
set "G1=%ESC%[38;5;228m"
set "G2=%ESC%[38;5;221m"
set "G3=%ESC%[38;5;214m"
set "G4=%ESC%[38;5;208m"
set "G5=%ESC%[38;5;205m"
set "G6=%ESC%[38;5;199m"
set "HRC_0=%ESC%[38;5;196m"
set "HRC_1=%ESC%[38;5;208m"
set "HRC_2=%ESC%[38;5;226m"
set "HRC_3=%ESC%[38;5;46m"
set "HRC_4=%ESC%[38;5;51m"
set "HRC_5=%ESC%[38;5;201m"
set "CUR_OFF=%ESC%[?25l"
set "CUR_ON=%ESC%[?25h"
set "RULE=%CD%──────────────────────────────────────────────────────────────────%C0%" 
set "CARD=──────────────────────────────────────────────" 
set "CARD_END=──────────────────────────────────────────────────────────────" 
set "FR_TOP=%CK%╔══════════════════════╗%C0%" 
set "FR_BOT=%CK%╚══════════════════════╝%C0%" 
set "FR_L=%CK%║%C0%"
set "SEP=%CD%│%C0%" 

:: 릴 그림 (가중치 %: 🍒30 🍋25 🔔18 🍀12 ⭐8 💎5 🐱2) 
set "SYM_0=🍒"
set "SYM_1=🍋"
set "SYM_2=🔔"
set "SYM_3=🍀"
set "SYM_4=⭐" 
set "SYM_5=💎"
set "SYM_6=🐱"
set "MULT_0=5"
set "MULT_1=8"
set "MULT_2=12"
set "MULT_3=25"
set "MULT_4=50"
set "MULT_5=100"
set "MULT_6=777"

:: 복고양이 (캐럿 포함 - 이 줄들에는 느낌표를 쓰지 않는다)
set "CAT_1=  /\_/\"
set "CAT_2= (=^･ω･^=)ﾉ"
set "CAT_3=  (_)(_)"
set "CAT_HAPPY=(=^▽^=)ﾉ"
set "CAT_WOW=(=ↀωↀ=)"
set "CAT_SAD=(=；ω；=)"
set "CAT_SLEEP=(=-ω-=)"

:: 복고양이 한마디 
set "MISS_1=다음 판은 느낌이 온다냥"
set "MISS_2=릴이 살짝 삐졌다냥, 달래 보자냥" 
set "MISS_3=츄르 한 개만 주면 대박 나게 해 주겠다냥"
set "WIN_1=딩동~ 앞발을 흔든 보람이 있다냥"
set "WIN_2=코인 냄새가 난다냥, 킁킁"
set "WIN_3=역시 복고양이다냥" 

:: 기록 파일  
set "REC_DIR=%LOCALAPPDATA%\Playground\Slot"
set "STAT_FILE=%REC_DIR%\stats.ini"
if not exist "%REC_DIR%" mkdir "%REC_DIR%" >nul 2>&1

call :CALIBRATE
call :LOAD_STATS
for %%i in (1 2 3) do (
    call :RANDSYM _s
    set "T%%i=!_s!"
    call :RANDSYM _s
    set "M%%i=!_s!"
    call :RANDSYM _s
    set "B%%i=!_s!"
)

:: ======================================================================
:: 로비 
:: ======================================================================
:LOBBY
cls
echo.
echo   %G1%███████╗██╗      ██████╗ ████████╗%C0% 
echo   %G2%██╔════╝██║     ██╔═══██╗╚══██╔══╝%C0%     %CP%!CAT_1!%C0% 
echo   %G3%███████╗██║     ██║   ██║   ██║   %C0%    %CP%!CAT_2!%C0%   %CD%nyan nyan, feeling lucky~%C0%
echo   %G4%╚════██║██║     ██║   ██║   ██║   %C0%     %CP%!CAT_3!%C0% 
echo   %G5%███████║███████╗╚██████╔╝   ██║   %C0% 
echo   %G6%╚══════╝╚══════╝ ╚═════╝    ╚═╝   %C0% 
echo   %CD%   복고양이 슬롯머신  ·  가운데 줄에 같은 그림 세 개 🎰%C0%
echo.
echo   %CC%■%C0% %date% %time:~0,8% 
echo.
echo   %CC%╭─%C0% %CW%내 지갑%C0% %CC%%CARD%%C0% 
echo   %CC%│%C0%  %CD%코인%C0%        🪙 %CK%!ST_COINS!%C0% 
echo   %CC%│%C0%  %CD%최고 당첨%C0%   %CY%!ST_BEST!%C0% 코인   %CD%·%C0%  %CD%잭팟%C0% %CM%!ST_JACKPOT!%C0%회  %CD%·%C0%  %CD%누적%C0% !ST_SPINS!판 
echo   %CC%╰%CARD_END%%C0%
echo.
echo   %CC%■%C0% %CW%베팅%C0% 
echo     %CD%└%C0% %CY%[1]%C0% 1코인  
echo     %CD%└%C0% %CY%[2]%C0% 5코인  
echo     %CD%└%C0% %CY%[3]%C0% 10코인 
echo     %CD%└%C0% %CY%[4]%C0% 자동 10회        %CD%1코인씩 빠르게%C0% 
echo     %CD%└%C0% %CY%[5]%C0% 배당표  
echo     %CD%└%C0% %CY%[0]%C0% 종료 
echo.
choice /C 123450 /N /M "   선택 > " 
if errorlevel 6 goto :QUIT
if errorlevel 5 goto :PAYTABLE
if errorlevel 4 goto :AUTO10
if errorlevel 3 (set "BET=10") else if errorlevel 2 (set "BET=5") else (set "BET=1")
goto :PLAY

:: ======================================================================
:: 한 판  
:: ======================================================================
:PLAY
call :CHECK_WALLET
if defined BROKE goto :LOBBY
cls
echo.
echo   🎰 %CW%LUCKY CAT SLOT%C0%  %CD%·%C0%  🪙 %CK%!ST_COINS!%C0%  %CD%·%C0%  %CD%베팅%C0% %CW%!BET!%C0% 
echo   %RULE%
echo.
set "FAST="
call :SPIN
call :JUDGE
echo.
call :SHOW_RESULT
echo.
echo   %RULE%
echo.
choice /C 120 /N /M "   [1] 한 번 더   [2] 로비   [0] 종료 > " 
if errorlevel 3 goto :QUIT
if errorlevel 2 goto :LOBBY
goto :PLAY

:: ======================================================================
:: 자동 10회 (1코인) 
:: ======================================================================
:AUTO10
set "BET=1"
cls
echo.
echo   🎰 %CW%LUCKY CAT SLOT%C0%  %CD%·%C0%  %CW%자동 10회%C0%  %CD%·%C0%  %CD%베팅%C0% %CW%1%C0%
echo   %RULE%
echo.
set /a "_in=0, _out=0"
for /l %%n in (1,1,10) do set "LOG_%%n="
for /l %%n in (1,1,10) do (
    call :CHECK_WALLET
    if not defined BROKE (
        set "FAST=1"
        call :SPIN
        call :JUDGE
        set /a "_in+=BET, _out+=WIN"
        call :LOG_LINE %%n
    )
)
echo.
echo   %CC%╭─%C0% %CW%자동 10회 결과%C0% %CC%%CARD%%C0%
for /l %%n in (1,1,10) do if defined LOG_%%n echo   %CC%│%C0%  !LOG_%%n!
set /a "_net=_out-_in"
set "_nt=%CR%!_net!%C0%"
if !_net! geq 0 set "_nt=%CG%+!_net!%C0%"
echo   %CC%│%C0%
echo   %CC%│%C0%  %CD%넣은 코인%C0%  !_in!   %CD%·%C0%   %CD%받은 코인%C0%  !_out!   %CD%·%C0%   %CD%손익%C0%  !_nt!
echo   %CC%╰%CARD_END%%C0%
echo.
echo   %RULE%
echo.
choice /C 120 /N /M "   [1] 자동 10회 더   [2] 로비   [0] 종료 > "
if errorlevel 3 goto :QUIT
if errorlevel 2 goto :LOBBY
goto :AUTO10

:: ======================================================================
:: 배당표  
:: ======================================================================
:PAYTABLE
cls
echo.
echo   🎰 %CW%LUCKY CAT SLOT%C0%  %CD%·%C0%  배당표 
echo   %RULE%
echo.
echo   %CC%■%C0% %CW%가운데 줄 기준%C0%  %CD%받는 코인 = 베팅 × 배수%C0% 
echo     %CD%└%C0% 🐱🐱🐱   %CM%×777%C0%   %CM%JACKPOT%C0% 
echo     %CD%└%C0% 💎💎💎   %CY%×100%C0% 
echo     %CD%└%C0% ⭐⭐⭐   %CY%×50%C0% 
echo     %CD%└%C0% 🍀🍀🍀   %CG%×25%C0%
echo     %CD%└%C0% 🔔🔔🔔   %CG%×12%C0%
echo     %CD%└%C0% 🍋🍋🍋   %CG%×8%C0% 
echo     %CD%└%C0% 🍒🍒🍒   %CG%×5%C0% 
echo     %CD%└%C0% 🐱🐱     %CG%×5%C0%    %CD%아무 칸이나 두 개%C0% 
echo     %CD%└%C0% 🍒🍒     %CG%×2%C0%    %CD%아무 칸이나 두 개%C0% 
echo.
echo   %CC%■%C0% %CW%나올 확률%C0%  %CD%릴 한 칸 기준%C0%
echo     %CD%└%C0% 🍒 30%%  🍋 25%%  🔔 18%%  🍀 12%%  ⭐ 8%%  💎 5%%  🐱 2%%
echo.
echo     %CP%!CAT_1!%C0%
echo    %CP%!CAT_HAPPY!%C0%   %CD%잭팟 확률은 12만 5천 분의 1이다냥%C0% 
echo     %CP%!CAT_3!%C0%
echo.
echo   %RULE%
echo.
<nul set /p "=!C0!   !CD!아무 키나 누르면 로비로 돌아갑니다!C0! "
pause >nul
goto :LOBBY

:QUIT
<nul set /p "=%CUR_ON%"
call :SAVE_STATS
echo.
echo   %CD%  "오늘의 행운은 여기까지다냥, 또 오라냥"%C0% !CAT_SLEEP!
echo.
endlocal
exit /b 0

:: ======================================================================
:: 서브루틴: 지갑 확인 (코인 부족 → BROKE, 0 코인이면 복고양이가 20코인 대출)
:: ======================================================================
:CHECK_WALLET
set "BROKE="
if !ST_COINS! geq !BET! goto :eof
set "BROKE=1"
echo.
if !ST_COINS! gtr 0 goto :CW_SHORT
set /a ST_COINS+=20
call :SAVE_STATS
echo     %CP%!CAT_1!%C0%
echo    %CP%!CAT_SAD!%C0%   %CY%▲ 코인이 떨어졌다냥%C0%   %CD%복고양이가 20코인을 빌려줬어요 (이자는 츄르)%C0%
echo     %CP%!CAT_3!%C0%
goto :CW_WAIT
:CW_SHORT
echo   %CY%▲%C0% 코인이 모자라요  %CD%(보유 !ST_COINS! · 베팅 !BET!)%C0%
:CW_WAIT
echo.
<nul set /p "=!C0!   !CD!아무 키나 누르면 로비로 돌아갑니다!C0! "
pause >nul
goto :eof

:: ======================================================================
:: 서브루틴: 릴 회전 (M1~M3 = 가운데 줄 결과, FAST=1 이면 짧게)
::  릴 i 는 STOP_i 프레임에서 멈추고, 직전 프레임에 결과 그림을 위 칸에 넣어 굴러 내려오게 한다 
:: ======================================================================
:SPIN
set /a "ST_COINS-=BET, ST_SPINS+=1"
for %%i in (1 2 3) do (
    call :RANDSYM _s
    set "R%%i=!_s!"
)
set "REACH="
if "!R1!"=="!R2!" set "REACH=1"
if defined FAST (set /a "STOP_1=4, STOP_2=6, STOP_3=8") else (set /a "STOP_1=9, STOP_2=14, STOP_3=19")
if defined REACH if not defined FAST set /a STOP_3+=7
echo       %FR_TOP%
call :DRAW_ROWS
echo       %FR_BOT%
<nul set /p "=%CUR_OFF%      "
for /l %%f in (1,1,!STOP_3!) do (
    for %%i in (1 2 3) do if %%f leq !STOP_%%i! (
        set "B%%i=!M%%i!"
        set "M%%i=!T%%i!"
        set /a "_pre=STOP_%%i - 1"
        if %%f equ !_pre! (set "T%%i=!R%%i!") else (
            call :RANDSYM _s
            set "T%%i=!_s!"
        )
    )
    <nul set /p "=%ESC%[1G%ESC%[4A"
    call :DRAW_ROWS
    <nul set /p "=%ESC%[1B%ESC%[1G%ESC%[2K"
    set "_ms=35"
    if defined FAST set "_ms=18"
    if %%f gtr !STOP_2! if defined REACH if not defined FAST (
        set /a "_ms=45 + (%%f - STOP_2) * 14"
        set /a "_blink=%%f %% 2"
        if !_blink! equ 0 (<nul set /p "=      %CM%두근두근... 리치%C0% 💓") else (<nul set /p "=      %CM%두근두근... 리치%C0%") 
    )
    call :SLEEP !_ms!
)
<nul set /p "=%ESC%[1G%ESC%[2K%CUR_ON%"
goto :eof

:: 자동 모드 한 줄 기록 (인수 = 회차) - 블록 밖에서 풀어야 M1~M3 가 최신 값이다 
:LOG_LINE
set "_nn= %~1"
set "_w=%CD%꽝%C0%"
if !WIN! gtr 0 set "_w=%CG%+!WIN!%C0%"
set "LOG_%~1=!_nn:~-2!회   !SYM_%M1%! !SYM_%M2%! !SYM_%M3%!   !_w!"
goto :eof

:DRAW_ROWS
set "_t1=!SYM_%T1%!"
set "_t2=!SYM_%T2%!"
set "_t3=!SYM_%T3%!"
set "_m1=!SYM_%M1%!"
set "_m2=!SYM_%M2%!"
set "_m3=!SYM_%M3%!"
set "_b1=!SYM_%B1%!"
set "_b2=!SYM_%B2%!"
set "_b3=!SYM_%B3%!"
echo       %FR_L%   !_t1!  %SEP%  !_t2!  %SEP%  !_t3!   %FR_L%
echo    %CY%▶%C0%  %FR_L%%BG%   !_m1!  │  !_m2!  │  !_m3!   %C0%%FR_L%  %CY%◀%C0% 
echo       %FR_L%   !_b1!  %SEP%  !_b2!  %SEP%  !_b3!   %FR_L%
goto :eof

:: ======================================================================
:: 서브루틴: 판정 (M1~M3 → MULT, WIN, 코인 반영)
:: ======================================================================
:JUDGE
set "MULT=0"
set /a "_cherry=0, _cat=0"
for %%i in (1 2 3) do (
    if !M%%i! equ 0 set /a _cherry+=1
    if !M%%i! equ 6 set /a _cat+=1
)
if !M1! equ !M2! if !M2! equ !M3! (for %%m in (!M1!) do set "MULT=!MULT_%%m!") & goto :JUDGE_PAY
if !_cat! equ 2 set "MULT=5"
if !_cherry! equ 2 set "MULT=2"
:JUDGE_PAY
set /a "WIN=BET * MULT, ST_COINS+=WIN"
if !WIN! gtr !ST_BEST! set "ST_BEST=!WIN!"
set "JACKPOT="
if !MULT! geq 100 set "JACKPOT=1"
if defined JACKPOT set /a ST_JACKPOT+=1
call :SAVE_STATS
goto :eof

:: ======================================================================
:: 서브루틴: 결과 연출  
:: ======================================================================
:SHOW_RESULT
set /a "_r=!random! %% 3 + 1"
if !MULT! equ 0 goto :SR_MISS
if defined JACKPOT goto :SR_JACKPOT
set "_rain="
set /a "_nc=MULT / 2 + 2"
if !_nc! gtr 14 set "_nc=14"
for /l %%c in (1,1,!_nc!) do set "_rain=!_rain!🪙"
for %%r in (!_r!) do set "TALK=!WIN_%%r!"
echo     !_rain!
echo.
echo     %CP%!CAT_1!%C0%
echo    %CP%!CAT_HAPPY!%C0%   %CG%✓ WIN ×!MULT!%C0%  %CK%+!WIN!%C0% 코인   %CD%!TALK!%C0%
echo     %CP%!CAT_3!%C0%
goto :eof

:SR_MISS
for %%r in (!_r!) do set "TALK=!MISS_%%r!"
echo     %CP%!CAT_1!%C0%
echo    %CP%!CAT_SLEEP!%C0%   %CD%▲ 꽝%C0%   %CD%!TALK!%C0%
echo     %CP%!CAT_3!%C0%
goto :eof

:SR_JACKPOT
<nul set /p "=%CUR_OFF%"
for /l %%f in (1,1,9) do (
    set /a "_k=%%f %% 6"
    for %%k in (!_k!) do set "HC=!HRC_%%k!"
    echo      !HC!██╗ █████╗  ██████╗██╗  ██╗██████╗  ██████╗ ████████╗!C0! 
    echo      !HC!██║██╔══██╗██╔════╝██║ ██╔╝██╔══██╗██╔═══██╗╚══██╔══╝!C0! 
    echo      !HC!██║███████║██║     █████╔╝ ██████╔╝██║   ██║   ██║   !C0! 
    echo   !HC!██   ██║██╔══██║██║     ██╔═██╗ ██╔═══╝ ██║   ██║   ██║   !C0! 
    echo   !HC!╚█████╔╝██║  ██║╚██████╗██║  ██╗██║     ╚██████╔╝   ██║   !C0! 
    echo    !HC!╚════╝ ╚═╝  ╚═╝ ╚═════╝╚═╝  ╚═╝╚═╝      ╚═════╝    ╚═╝   !C0! 
    call :SLEEP 110
    if %%f lss 9 <nul set /p "=%ESC%[6A"
)
<nul set /p "=%CUR_ON%"
echo.
echo     🪙🪙🪙🪙🪙🪙🪙🪙🪙🪙🪙🪙🪙🪙🪙🪙🪙🪙🪙🪙 
echo     ✨🎆✨🎉✨🎆✨🎉✨🎆✨🎉✨🎆✨🎉✨🎆✨🎉 
echo.
echo     %CP%!CAT_1!%C0%
echo    %CP%!CAT_WOW!%C0%   %CM%✓ JACKPOT ×!MULT!%C0%  %CK%+!WIN!%C0% 코인   %CD%앞발이 멈추지 않는다냥%C0%
echo     %CP%!CAT_3!%C0%
goto :eof

:: ======================================================================
:: 서브루틴: 가중치 랜덤 그림 (%1 = 저장할 변수명, 값 0~6)
:: ======================================================================
:RANDSYM
set /a "_w=!random! %% 100"
set "_v=6"
if %_w% lss 98 set "_v=5"
if %_w% lss 93 set "_v=4"
if %_w% lss 85 set "_v=3"
if %_w% lss 73 set "_v=2"
if %_w% lss 55 set "_v=1"
if %_w% lss 30 set "_v=0"
set "%~1=%_v%"
goto :eof

:: ======================================================================
:: 서브루틴: 기록 (%LOCALAPPDATA%\Playground\Slot)
:: ======================================================================
:LOAD_STATS
set "ST_COINS=100"
set "ST_BEST=0"
set "ST_JACKPOT=0"
set "ST_SPINS=0"
if exist "%STAT_FILE%" for /f "usebackq tokens=1,* delims==" %%a in ("%STAT_FILE%") do set "ST_%%a=%%b"
goto :eof

:SAVE_STATS
> "%STAT_FILE%" (
    echo COINS=!ST_COINS!
    echo BEST=!ST_BEST!
    echo JACKPOT=!ST_JACKPOT!
    echo SPINS=!ST_SPINS!
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
