@echo off
chcp 65001 >nul
:: 느낌표 리터럴은 지연 확장을 켜기 전에 잡아 둔다 (echo 문구에서 !EX! 로 사용)
set "EX=!"
setlocal enabledelayedexpansion

:: ======================================================================
::  NUMBER BASEBALL - 숫자 야구 콘솔 게임 
::
::  부엉이 베어스가 숨긴 비밀 숫자를 맞혀 끝내기 홈런을 쳐라
::   · 정규 시즌   3자리 · 9이닝
::   · 포스트 시즌 4자리 · 10이닝
::   · 자리와 숫자 모두 맞으면 S, 숫자만 맞으면 B, 하나도 없으면 OUT 
::
::  콘솔 테마: .claude/docs/cmd-console-style.md (ANSI 256 색, Windows 10+)
::  주의: 멀티바이트 문자가 있는 줄은 공백을 붙여 짝수 바이트로 끝낸다
::  주의: 지연 확장 상태이므로 느낌표는 !EX! 로만 출력한다 
::  주의: 캐럿이 들어간 이모티콘은 느낌표가 없는 set 줄에서만 정의한다 
:: ======================================================================

title Number Baseball

:: ----------------------------------------------------------------------
:: 콘솔 테마 (G1~G6 배너 그라데이션: 연두 → 잔디 초록) 
:: ----------------------------------------------------------------------
for /f %%E in ('echo prompt $E^| cmd') do set "ESC=%%E"
set "C0=%ESC%[0m"
set "CW=%ESC%[97m"
set "CD=%ESC%[90m"
set "CC=%ESC%[96m"
set "CG=%ESC%[92m"
set "CY=%ESC%[93m"
set "CR=%ESC%[91m"
set "CP=%ESC%[38;5;218m"
set "CO=%ESC%[38;5;208m"
set "CM=%ESC%[38;5;213m"
set "CL_S=%ESC%[38;5;220m"
set "CL_B=%ESC%[38;5;46m"
set "G1=%ESC%[38;5;157m"
set "G2=%ESC%[38;5;120m"
set "G3=%ESC%[38;5;84m"
set "G4=%ESC%[38;5;48m"
set "G5=%ESC%[38;5;42m"
set "G6=%ESC%[38;5;36m"
set "HRC_0=%ESC%[38;5;196m"
set "HRC_1=%ESC%[38;5;208m"
set "HRC_2=%ESC%[38;5;226m"
set "HRC_3=%ESC%[38;5;46m"
set "HRC_4=%ESC%[38;5;51m"
set "HRC_5=%ESC%[38;5;201m"
set "HRC_6=%ESC%[38;5;213m"
set "CUR_OFF=%ESC%[?25l"
set "CUR_ON=%ESC%[?25h"
set "RULE=%CD%──────────────────────────────────────────────────────────────────%C0%" 
set "CARD=────────────────────────────────────────────" 
set "CARD_END=─────────────────────────────────────────────────────────────"  

:: 애니메이션 재료 
set "TRAIL=························"
set "SPC=                        "
set "BAR_ON=▰▰▰▰▰▰▰▰▰▰▰▰▰"
set "BAR_OFF=▱▱▱▱▱▱▱▱▱▱▱▱▱" 
set "FWE_0=%ESC%[38;5;196m✦%C0%"
set "FWE_1=🎆"
set "FWE_2=%ESC%[38;5;226m✧%C0%"
set "FWE_3=🎉"
set "FWE_4=✨" 

:: 이모티콘 (캐럿 포함 - 이 줄들에는 느낌표를 쓰지 않는다)
set "KAO_CHEER=\(^o^)/"
set "KAO_WOW=(ﾉ◕ヮ◕)ﾉ*:･ﾟ✧"
set "KAO_CRY=(╥﹏╥)" 
set "KAO_SHRUG=¯\_(ツ)_/¯" 
set "KAO_WAVE=(^_^)/~"

:: 구종 
set "PT_0=직구" 
set "PT_1=슬라이더" 
set "PT_2=커브" 
set "PT_3=체인지업" 
set "PT_4=포크볼"
set "PV_0=145"
set "PV_1=134"
set "PV_2=118"
set "PV_3=127"
set "PV_4=131"

:: 중계 멘트
set "CAST_OUT_1=헛스윙 삼진~ 방망이가 허공을 가릅니다"
set "CAST_OUT_2=공 하나 건드리지 못했어요. 그래도 지울 숫자가 생겼습니다" 
set "CAST_OUT_3=완벽하게 속았습니다. 부엉이 투수 오늘 공이 좋네요" 
set "CAST_FOUL_1=파울~ 숫자는 맞혔는데 자리가 틀렸어요" 
set "CAST_FOUL_2=타이밍이 살짝 어긋났습니다. 방향만 잡으면 됩니다" 
set "CAST_FOUL_3=관중석으로 날아가는 파울볼~ 관중이 잡았어요 🧤" 
set "CAST_HIT1_1=깔끔한 중전 안타~ 1루에 안착합니다"
set "CAST_HIT1_2=빗맞았지만 코스가 좋아요. 내야 안타"
set "CAST_HIT1_3=밀어친 타구가 1·2루 간을 빠져나갑니다" 
set "CAST_HIT2_1=쭉쭉 뻗어갑니다~ 좌중간을 가르는 2루타"
set "CAST_HIT2_2=라인 드라이브~ 펜스 앞 원바운드, 2루까지"
set "CAST_HIT2_3=거의 다 왔습니다. 담장까지 한 뼘 남았어요"
set "CAST_HIT3_1=펜스 직격~ 3루까지 전력 질주" 
set "CAST_HIT3_2=우중간 깊숙한 타구, 여유 있게 3루타" 
set "CAST_HIT3_3=담장 바로 앞에서 떨어집니다. 3루타"
set "CAST_HR_1=넘어갑니다~ 담장을 넘어갑니다!EX! 끝내기 홈런!EX!" 
set "CAST_HR_2=이 타구는~ 이 타구는~ 넘어갔습니다!EX! 경기 끝!EX!" 
set "CAST_HR_3=밤하늘을 가르는 대형 아치!EX! 숫자 구장이 뒤집어집니다!EX!"

:: 기록 파일
set "REC_DIR=%LOCALAPPDATA%\Playground\NumberBaseball"
set "REC_FILE=%REC_DIR%\record.ini"
if not exist "%REC_DIR%" mkdir "%REC_DIR%" >nul 2>&1

:: 애니메이션 지연 보정 (루프 속도 측정) 
call :CALIBRATE

:: ======================================================================
:: 타이틀 화면 
:: ======================================================================
:TITLE
call :LOAD_RECORD
cls
echo.
echo   %G1%██████╗  █████╗ ███████╗███████╗██████╗  █████╗ ██╗     ██╗     %C0% 
echo   %G2%██╔══██╗██╔══██╗██╔════╝██╔════╝██╔══██╗██╔══██╗██║     ██║     %C0% 
echo   %G3%██████╔╝███████║███████╗█████╗  ██████╔╝███████║██║     ██║     %C0% 
echo   %G4%██╔══██╗██╔══██║╚════██║██╔══╝  ██╔══██╗██╔══██║██║     ██║     %C0% 
echo   %G5%██████╔╝██║  ██║███████║███████╗██████╔╝██║  ██║███████╗███████╗%C0% 
echo   %G6%╚═════╝ ╚═╝  ╚═╝╚══════╝╚══════╝╚═════╝ ╚═╝  ╚═╝╚══════╝╚══════╝%C0% 
echo   %CD%   숫자 야구  ·  비밀 숫자를 맞혀 끝내기 홈런을 쳐라 ⚾%C0% 
echo.
echo   %CC%■%C0% %date% %time:~0,8% 
echo.
echo      %CP%,_,%C0%     🎤 %CD%부엉 심판%C0%  "오늘도 공정하게 판정하겠습니다"
echo     %CP%(O,O)%C0%    🏟  %CD%숫자 구장%C0%  ·  %CW%PLAYER%C0% %CD%vs%C0% %CP%OWL BEARS%C0%
echo     %CP%(   )%C0%    📊 %CD%통산 전적%C0%  %CW%!REC_WIN!%C0%승 %CW%!REC_LOSE!%C0%패  %CD%·%C0%  %CD%최고 기록%C0%  3자리 %CY%!SHOW_BEST3!%C0%  %CD%/%C0%  4자리 %CY%!SHOW_BEST4!%C0%
echo     %CP%-"-"-%C0%
echo.
echo   %RULE%
echo.
echo   %CC%■%C0% %CW%경기 선택%C0%
echo     %CD%└%C0% %CY%[1]%C0% 정규 시즌     %CD%숫자 3개 · 9이닝%C0% 
echo     %CD%└%C0% %CY%[2]%C0% 포스트 시즌   %CD%숫자 4개 · 10이닝%C0% 
echo     %CD%└%C0% %CY%[3]%C0% 경기 규칙
echo     %CD%└%C0% %CY%[Q]%C0% 퇴장 
echo.
choice /C 123Q /N /M "   선택 > " 
if errorlevel 4 goto :QUIT
if errorlevel 3 goto :RULES_FROM_TITLE
if errorlevel 2 goto :MODE_POST
goto :MODE_REGULAR

:MODE_REGULAR
set "LEN=3"
set "INNINGS=9"
set "MODE_NAME=정규 시즌" 
goto :PLAY

:MODE_POST
set "LEN=4"
set "INNINGS=10"
set "MODE_NAME=포스트 시즌"
goto :PLAY

:RULES_FROM_TITLE
call :RULES
goto :TITLE

:: ======================================================================
:: 경기 진행
:: ======================================================================
:PLAY
title Number Baseball - !MODE_NAME!
call :GEN_SECRET
set "INN=0"
set "LP_INN="
for /l %%n in (1,1,12) do (
    set "H_G_%%n="
    set "H_R_%%n="
    set "H_C_%%n="
)
call :INTRO

:AT_BAT
set /a INN+=1
if !INN! gtr !INNINGS! goto :GAME_OVER
call :DRAW_BOARD

:ASK
echo.
set "GUESS="
set /p "GUESS=!C0!   !CO!⚾!C0! !CW!!INN!회 타석!C0!  !CD!숫자 !LEN!개 입력  ·  H 규칙  ·  Q 기권!C0!  !CY!›!C0! " 
if not defined GUESS goto :ASK
set "GUESS=!GUESS: =!"
if not defined GUESS goto :ASK
if /i "!GUESS!"=="q" goto :FORFEIT
if /i "!GUESS!"=="h" (
    call :RULES
    call :DRAW_BOARD
    goto :ASK
)

call :VALIDATE
if not defined BAD goto :THROW
echo   !CY!▲!C0! !CY!보크 선언!C0!  !BAD! !CD!(이닝은 소모되지 않아요)!C0!
goto :ASK

:THROW
call :PITCH_ANIM
call :JUDGE

if !S! equ !LEN! goto :HOMERUN
if !S! equ 0 if !B! equ 0 goto :R_OUT
if !S! equ 0 goto :R_FOUL
goto :R_HIT

:: ---- 판정별 연출 ---- 
:R_OUT
set "LP_ICON=💨"
set "LP_LABEL=삼진 아웃"
set "LP_COLOR=!CR!"
set "LP_KEY=OUT"
set "LP_RES=OUT"
set "LP_CROWD=!KAO_CRY!  아이고~ 아까워라" 
set "LP_HINT=입력한 !GUESS! 의 숫자는 정답에 하나도 없어요" 
set "LP_DIST=-"
set "LP_BARN=0"
echo     !CD!휘익~!C0! 💨 
goto :R_DONE

:R_FOUL
set "LP_ICON=↗️"
set "LP_LABEL=파울" 
set "LP_COLOR=!CL_B!"
set "LP_KEY=FOUL"
set "LP_RES=!B!B"
set "LP_CROWD=!KAO_SHRUG!  아까비~"
set "LP_HINT="
set /a "LP_DIST=B*12 + !random! %% 10 + 8"
set /a "LP_BARN=LP_DIST/10"
echo     !CL_B!틱~!C0! ↗️
goto :R_DONE

:R_HIT
set "LP_ICON=💥"
set "LP_COLOR=!CY!"
set "LP_KEY=HIT!S!"
set "LP_RES=!S!S"
if !B! gtr 0 set "LP_RES=!S!S!B!B"
if !S! equ 1 set "LP_LABEL=안타"
if !S! equ 2 set "LP_LABEL=2루타" 
if !S! equ 3 set "LP_LABEL=3루타" 
set "LP_CROWD=!KAO_CHEER!  와아아~"
if !S! geq 2 set "LP_CROWD=!KAO_WOW!  모두 일어나세요~"
set "LP_HINT="
set /a "LP_DIST=25 + S*90/LEN + B*5 + !random! %% 10"
set /a "LP_BARN=LP_DIST/10"
echo     !CY!딱~!C0! 💥
goto :R_DONE

:R_DONE
set /a "_r=!random! %% 3 + 1"
for %%r in (!_r!) do set "LP_CAST=!CAST_%LP_KEY%_%%r!"
set "LP_INN=!INN!"
set "LP_GUESS=!GUESS!"
set "LP_S=!S!"
set "LP_B=!B!"
set "H_G_!INN!=!GUESS!"
set "H_R_!INN!=!LP_RES!"
set "H_C_!INN!=!LP_COLOR!"
call :SLEEP 650
goto :AT_BAT

:: ======================================================================
:: 끝내기 홈런 
:: ======================================================================
:HOMERUN
set "H_G_!INN!=!GUESS!"
set "H_R_!INN!=HR"
set "H_C_!INN!=!CM!"
echo     !CM!따아악~!C0! 💥💥💥
call :SLEEP 900
cls
echo.
<nul set /p "=%CUR_OFF%"
for /l %%f in (1,1,9) do (
    set /a "_k=%%f %% 7"
    for %%k in (!_k!) do set "HC=!HRC_%%k!"
    call :HR_BANNER
    call :SLEEP 110
    if %%f lss 9 <nul set /p "=%ESC%[6A"
)
<nul set /p "=%CUR_ON%"
echo.
call :FIREWORKS
call :FIREWORKS
echo.
set /a "_r=!random! %% 3 + 1"
for %%r in (!_r!) do set "TYPE_TEXT=!CAST_HR_%%r!"
<nul set /p "=!C0!   🎤 !CD!캐스터!C0!  "
call :TYPE TYPE_TEXT CW
echo.
echo   %CD%   관중석%C0%  !KAO_WOW!  !KAO_CHEER!  !KAO_WOW!  !KAO_CHEER! 
echo.

:: 기록 갱신
set "NEWREC="
set /a REC_WIN+=1
if not defined REC_BEST%LEN% set "REC_BEST%LEN%=99"
for %%L in (!LEN!) do if !INN! lss !REC_BEST%%L! (
    set "REC_BEST%%L=!INN!"
    set "NEWREC=!CM!🏆 신기록!C0!" 
)
call :SAVE_RECORD

echo   %CC%╭─%C0% %CW%경기 결과%C0% %CC%%CARD%─────%C0% 
echo   %CC%│%C0%  %CD%정답%C0%       %CW%!SECRET!%C0% 
echo   %CC%│%C0%  %CD%승리 이닝%C0%  %CM%!INN!회 끝내기 홈런%C0%  !NEWREC!
echo   %CC%│%C0%  %CD%통산 전적%C0%  !REC_WIN!승 !REC_LOSE!패 
echo   %CC%╰%CARD_END%%C0%  
echo.
echo      %CP%,_,%C0%
echo     %CP%(^^,^^)%C0%   %CM%✓ HOME RUN%C0% ✨   %CD%부엉 심판도 박수를 칩니다%C0%
echo     %CP%(   )%C0%
echo     %CP%-"-"-%C0%
goto :NEXT_GAME

:: ======================================================================
:: 경기 종료 (이닝 소진) / 기권 
:: ======================================================================
:GAME_OVER
set /a "INN=INNINGS"
call :DRAW_BOARD
set "END_REASON=!INNINGS!이닝 무득점 패배" 
goto :LOSE

:FORFEIT
echo.
choice /C YN /N /M "   정말 기권할까요? [Y=기권 / N=계속] "
if errorlevel 2 goto :ASK
set "END_REASON=!INN!회 기권"

:LOSE
set /a REC_LOSE+=1
call :SAVE_RECORD
echo.
echo   %RULE%
echo.
echo   %CC%╭─%C0% %CW%경기 종료%C0% %CC%%CARD%─────%C0% 
echo   %CC%│%C0%  %CD%정답%C0%       %CW%!SECRET!%C0% 
echo   %CC%│%C0%  %CD%결과%C0%       %CR%!END_REASON!%C0% 
echo   %CC%│%C0%  %CD%통산 전적%C0%  !REC_WIN!승 !REC_LOSE!패 
echo   %CC%╰%CARD_END%%C0%  
echo.
echo      %CP%,_,%C0%
echo     %CP%(;,;)%C0%   %CR%✗ GAME OVER%C0%   %CD%다음 경기에서 설욕해요%C0% 
echo     %CP%(   )%C0%
echo     %CP%-"-"-%C0%

:NEXT_GAME
echo.
echo   %RULE%
echo.
choice /C YN /N /M "   한 경기 더 할까요? [Y=타이틀로 / N=퇴장] "
if errorlevel 2 goto :QUIT
goto :TITLE

:QUIT
<nul set /p "=%CUR_ON%"
echo.
echo   %CD%  "오늘 경기는 여기까지, 숫자 구장에서 또 만나요"%C0% !KAO_WAVE!
echo.
endlocal
exit /b 0

:: ======================================================================
:: 서브루틴: 비밀 숫자 생성 (1~9, 중복 없음)
:: ======================================================================
:GEN_SECRET
set "SECRET=_"
:GEN_LOOP
set /a "_d=!random! %% 9 + 1"
for %%d in (!_d!) do if "!SECRET:%%d=!"=="!SECRET!" set "SECRET=!SECRET!%%d"
if "!SECRET:~%LEN%,1!"=="" goto :GEN_LOOP
set "SECRET=!SECRET:~1!"
goto :eof

:: ======================================================================
:: 서브루틴: 입력 검증 (BAD 가 비어 있으면 통과)
:: ======================================================================
:VALIDATE
set "BAD="
set /a "_last=LEN-1"
if "!GUESS:~%LEN%,1!" neq "" set "BAD=숫자는 딱 !LEN!개만 던질 수 있어요" & goto :eof 
if "!GUESS:~%_last%,1!"=="" set "BAD=숫자 !LEN!개를 모두 입력하세요" & goto :eof 
for /l %%i in (0,1,%_last%) do (
    set "_c=!GUESS:~%%i,1!"
    set "_ok=0"
    for %%d in (1 2 3 4 5 6 7 8 9) do if "!_c!"=="%%d" set "_ok=1"
    if "!_ok!"=="0" set "BAD=1~9 숫자만 쓸 수 있어요" 
)
if defined BAD goto :eof
for /l %%i in (0,1,%_last%) do for /l %%j in (0,1,%_last%) do if %%i lss %%j if "!GUESS:~%%i,1!"=="!GUESS:~%%j,1!" set "BAD=같은 숫자를 두 번 쓸 수 없어요" 
goto :eof

:: ======================================================================
:: 서브루틴: 판정 (S = 자리와 숫자 일치, B = 숫자만 일치) 
:: ======================================================================
:JUDGE
set /a "S=0, B=0, _last=LEN-1"
for /l %%i in (0,1,%_last%) do for /l %%j in (0,1,%_last%) do (
    if "!SECRET:~%%i,1!"=="!GUESS:~%%j,1!" (
        if %%i equ %%j (set /a S+=1) else (set /a B+=1)
    )
)
goto :eof

:: ======================================================================
:: 서브루틴: 경기 소개 (캐스터 타자기 연출) 
:: ======================================================================
:INTRO
cls
echo.
echo   %CO%⚾%C0% %CW%NUMBER BASEBALL%C0%  %CD%·%C0%  !MODE_NAME!  %CD%·%C0%  %CD%숫자 !LEN!개 · !INNINGS!이닝%C0% 
echo   %RULE%
echo.
set "TYPE_TEXT=숫자 구장에 관중이 가득 찼습니다. 오늘의 상대는 부엉이 베어스" 
<nul set /p "=!C0!   🎤 !CD!캐스터!C0!  "
call :TYPE TYPE_TEXT CW
set "TYPE_TEXT=비밀 숫자 !LEN!개를 숨겼습니다. 자리까지 맞히면 홈런입니다" 
<nul set /p "=!C0!   🎤 !CD!캐스터!C0!  "
call :TYPE TYPE_TEXT CW
echo.
echo      %CP%,_,%C0%
echo     %CP%(O,O)%C0%   🧢 %CD%부엉 심판%C0% 
<nul set /p "=!C0!    !CP!(   )!C0!   "
set "TYPE_TEXT=플레이 볼!EX!" 
call :TYPE TYPE_TEXT CY
echo     %CP%-"-"-%C0%
call :SLEEP 900
goto :eof

:: ======================================================================
:: 서브루틴: 전광판 + 직전 타석 결과 
:: ======================================================================
:DRAW_BOARD
cls
set "IDOTS="
for /l %%i in (1,1,!INNINGS!) do if %%i leq !INN! (set "IDOTS=!IDOTS!●") else (set "IDOTS=!IDOTS!○")
echo.
echo   %CO%⚾%C0% %CW%NUMBER BASEBALL%C0%  %CD%·%C0%  !MODE_NAME!  %CD%·%C0%  %CC%!IDOTS!%C0%  %CW%!INN!/!INNINGS!회%C0%
echo   %RULE%

set "ROW_N="
set "ROW_G="
set "ROW_R="
for /l %%n in (1,1,!INNINGS!) do (
    set "_c=%%n      "
    if %%n equ !INN! (set "ROW_N=!ROW_N!!CY!!_c:~0,6!!C0!") else (set "ROW_N=!ROW_N!!CD!!_c:~0,6!!C0!")
    if defined H_G_%%n (
        set "_c=!H_G_%%n!      "
        set "ROW_G=!ROW_G!!CW!!_c:~0,6!!C0!"
        set "_c=!H_R_%%n!      "
        set "ROW_R=!ROW_R!!H_C_%%n!!_c:~0,6!!C0!"
    ) else (
        set "ROW_G=!ROW_G!!CD!·     !C0!"
        set "ROW_R=!ROW_R!!CD!·     !C0!"
    )
)
echo.
echo   %CC%╭─%C0% 🏟  %CW%SCOREBOARD%C0% %CC%%CARD%%C0% 
echo   %CC%│%C0%  %CD%회%C0%      !ROW_N!
echo   %CC%│%C0%  %CD%타격%C0%    !ROW_G! 
echo   %CC%│%C0%  %CD%판정%C0%    !ROW_R! 
echo   %CC%╰%CARD_END%%C0%  
if not defined LP_INN goto :eof

:: ---- 직전 타석 결과 ---- 
set "LS="
set "LB="
for /l %%i in (1,1,!LEN!) do (
    if %%i leq !LP_S! (set "LS=!LS!!CL_S!●") else (set "LS=!LS!!CD!○")
    if %%i leq !LP_B! (set "LB=!LB!!CL_B!●") else (set "LB=!LB!!CD!○")
)
if !LP_BARN! gtr 13 set "LP_BARN=13"
for %%n in (!LP_BARN!) do set "BAR=!CO!!BAR_ON:~0,%%n!!CD!!BAR_OFF:~%%n!!C0!"
set "DIST_TXT=!LP_DIST!m"
if "!LP_DIST!"=="-" set "DIST_TXT=-"

echo.
echo   %CC%■%C0% %CW%!LP_INN!회 결과%C0%  %CD%·%C0%  %CW%!LP_GUESS!%C0% 
echo.
echo     !LP_ICON! !LP_COLOR!!LP_LABEL!!C0!    !CL_S!S!C0! !LS!!C0!   !CL_B!B!C0! !LB!!C0!   !CD!비거리!C0! !BAR! !DIST_TXT! 
echo     🎤 %CD%캐스터%C0%  !LP_CAST!
echo     👥 %CD%관중석%C0%  !LP_CROWD! 
if defined LP_HINT echo     💡 %CD%힌트%C0%    !LP_HINT!

:: 다이아몬드 (안타 수만큼 루 점등)
set "D1=!CD!◇!C0!"
set "D2=!CD!◇!C0!"
set "D3=!CD!◇!C0!"
set "DB=!CO!⚾!C0!"
if !LP_S! geq 1 set "D1=!CY!◆!C0!"
if !LP_S! geq 2 set "D2=!CY!◆!C0!"
if !LP_S! geq 3 set "D3=!CY!◆!C0!"
if "!LP_KEY!"=="OUT" set "DB=💨"
echo.
echo                     !D2! %CD%2루%C0%
echo                 %CD%╱       ╲%C0%
echo         %CD%3루%C0% !D3!      !DB!      !D1! %CD%1루%C0% 
echo                 %CD%╲       ╱%C0%
echo                     %CD%⌂ 홈%C0%
goto :eof

:: ======================================================================
:: 서브루틴: 투구 애니메이션 (구종·구속 랜덤, 구속이 빠를수록 빨리 날아감)
:: ======================================================================
:PITCH_ANIM
set /a "_p=!random! %% 5"
for %%p in (!_p!) do (
    set "P_NAME=!PT_%%p!"
    set /a "P_SPD=!PV_%%p! + !random! %% 9"
)
set /a "P_DELAY=(160 - P_SPD) + 12"
echo.
echo   %CD%   🧢 부엉이 투수, 와인드업...%C0%  %CW%!P_NAME!%C0% %CD%!P_SPD!km/h%C0%
call :SLEEP 350
<nul set /p "=%CUR_OFF%"
for /l %%i in (0,1,24) do (
    <nul set /p "=%ESC%[1G     %CP%(O,O)%C0%  %CD%!TRAIL:~0,%%i!%C0%%CO%⚾%C0%!SPC:~%%i!  🏏 %CW%!GUESS!%C0%   " 
    call :SLEEP !P_DELAY!
)
<nul set /p "=%CUR_ON%"
echo.
goto :eof

:: ======================================================================
:: 서브루틴: HOME RUN 배너 (HC 색으로 출력)
:: ======================================================================
:HR_BANNER
echo   !HC!██╗  ██╗ ██████╗ ███╗   ███╗███████╗    ██████╗ ██╗   ██╗███╗   ██╗!C0!
echo   !HC!██║  ██║██╔═══██╗████╗ ████║██╔════╝    ██╔══██╗██║   ██║████╗  ██║!C0!
echo   !HC!███████║██║   ██║██╔████╔██║█████╗      ██████╔╝██║   ██║██╔██╗ ██║!C0!
echo   !HC!██╔══██║██║   ██║██║╚██╔╝██║██╔══╝      ██╔══██╗██║   ██║██║╚██╗██║!C0!
echo   !HC!██║  ██║╚██████╔╝██║ ╚═╝ ██║███████╗    ██║  ██║╚██████╔╝██║ ╚████║!C0!
echo   !HC!╚═╝  ╚═╝ ╚═════╝ ╚═╝     ╚═╝╚══════╝    ╚═╝  ╚═╝ ╚═════╝ ╚═╝  ╚═══╝!C0!
goto :eof

:: ======================================================================
:: 서브루틴: 불꽃놀이 한 줄 (랜덤 간격 · 랜덤 불꽃)
:: ======================================================================
:FIREWORKS
set "FW="
for /l %%k in (1,1,7) do (
    set /a "_sp=!random! %% 8 + 2, _e=!random! %% 5"
    for %%s in (!_sp!) do set "FW=!FW!!SPC:~0,%%s!"
    for %%e in (!_e!) do set "FW=!FW!!FWE_%%e!"
)
echo  !FW!
call :SLEEP 250
goto :eof

:: ======================================================================
:: 서브루틴: 경기 규칙
:: ======================================================================
:RULES
cls
echo.
echo   %CO%⚾%C0% %CW%NUMBER BASEBALL%C0%  %CD%·%C0%  경기 규칙 
echo   %RULE%
echo.
echo   %CC%■%C0% %CW%기본 규칙%C0%
echo     %CD%└%C0% 부엉이 베어스가 1~9 중 서로 다른 숫자로 비밀 숫자를 숨깁니다 
echo     %CD%└%C0% 매 이닝 한 번 숫자를 던지면 부엉 심판이 판정합니다
echo     %CD%└%C0% 이닝이 끝나기 전에 모든 자리를 맞히면 끝내기 홈런 승리
echo.
echo   %CC%■%C0% %CW%판정%C0% 
echo     %CD%└%C0% %CL_S%S 스트라이크%C0%  숫자와 자리가 모두 맞음 
echo     %CD%└%C0% %CL_B%B 볼%C0%          숫자는 있지만 자리가 다름
echo     %CD%└%C0% %CR%OUT%C0%           숫자가 하나도 없음 %CD%(오히려 힌트)%C0%
echo.
echo   %CC%■%C0% %CW%타구 연출%C0%
echo     %CD%└%C0% 💨 %CR%삼진 아웃%C0%  0S 0B
echo     %CD%└%C0% ↗️ %CL_B%파울%C0%       0S · 볼만 있음
echo     %CD%└%C0% 💥 %CY%안타%C0%       1S  %CD%→%C0%  %CY%2루타%C0% 2S  %CD%→%C0%  %CY%3루타%C0% 3S 
echo     %CD%└%C0% 🎆 %CM%홈런%C0%       모든 자리 적중 
echo.
echo   %CC%■%C0% %CW%예시%C0%  %CD%정답이 472 일 때%C0%
echo     %CD%└%C0% 123  %CD%→%C0%  %CL_B%1B%C0%     %CD%2 는 있지만 자리가 다름%C0%
echo     %CD%└%C0% 479  %CD%→%C0%  %CY%2S%C0%     %CD%4 와 7 이 제자리%C0% 
echo     %CD%└%C0% 568  %CD%→%C0%  %CR%OUT%C0%    %CD%5 · 6 · 8 은 정답에 없음%C0%
echo.
echo   %RULE%
echo.
<nul set /p "=!C0!   !CD!아무 키나 누르면 돌아갑니다!C0! "
pause >nul
echo.
goto :eof

:: ======================================================================
:: 서브루틴: 타자기 출력 (%1 = 문구 변수명, %2 = 색 변수명) 
::  이모지는 서로게이트 쌍이라 한 글자씩 자르면 깨진다 - 문구에 이모지 금지 
:: ======================================================================
:TYPE
set "_T=!%~1!"
set "_TC=!%~2!"
for /l %%i in (0,1,120) do if "!_T:~%%i,1!" neq "" (
    <nul set /p "=%C0%!_TC!!_T:~%%i,1!"
    call :SLEEP 24
)
echo(%C0%
goto :eof

:: ======================================================================
:: 서브루틴: 지연 (%1 = 밀리초, CALIBRATE 로 측정한 루프 속도 사용)
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
:: 서브루틴: 기록 불러오기 / 저장 (%LOCALAPPDATA%\Playground\NumberBaseball)
:: ======================================================================
:LOAD_RECORD
set "REC_WIN=0"
set "REC_LOSE=0"
set "REC_BEST3="
set "REC_BEST4="
if exist "%REC_FILE%" for /f "usebackq tokens=1,* delims==" %%a in ("%REC_FILE%") do set "REC_%%a=%%b"
set "SHOW_BEST3=-"
set "SHOW_BEST4=-"
if defined REC_BEST3 set "SHOW_BEST3=!REC_BEST3!회"
if defined REC_BEST4 set "SHOW_BEST4=!REC_BEST4!회"
goto :eof

:SAVE_RECORD
> "%REC_FILE%" (
    echo WIN=!REC_WIN!
    echo LOSE=!REC_LOSE!
    if defined REC_BEST3 echo BEST3=!REC_BEST3!
    if defined REC_BEST4 echo BEST4=!REC_BEST4!
)
goto :eof
