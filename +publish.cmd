@echo off
chcp 65001 >nul
setlocal enabledelayedexpansion

set "ROOT=%~dp0"
set "BIN=%ROOT%bin"

for /f %%a in ('echo prompt $E ^| cmd /q') do set "ESC=%%a"
set "CY=!ESC![96m"
set "GR=!ESC![92m"
set "RE=!ESC![91m"
set "DG=!ESC![90m"
set "YL=!ESC![93m"
set "RS=!ESC![0m"
set "BD=!ESC![1m"

if not exist "%BIN%" mkdir "%BIN%"

:: ── 메뉴 출력 ─────────────────────────────────────────────────────
:MENU
cls
echo.
echo !BD!!CY!Playground ^| Publish!RS!
echo !DG!--------------------------------------------------!RS!
echo.
echo !BD!!DG!  Applications!RS!
echo !CY!   1!RS! AI.Clip               !DG!Applications/AI!RS!
echo !CY!   2!RS! Music.Player          !DG!Applications/Audio!RS!
echo !CY!   3!RS! Stay.Awake            !DG!Applications/Automation!RS!
echo !CY!   4!RS! Batch.Rename          !DG!Applications/Files!RS!
echo !CY!   5!RS! File.Duplicates       !DG!Applications/Files!RS!
echo !CY!   6!RS! Toast.Cast            !DG!Applications/Health!RS!
echo !CY!   7!RS! Photo.Video.Organizer !DG!Applications/Media!RS!
echo.
echo !BD!!DG!  Tools / Dev!RS!
echo !CY!   8!RS! Api.Probe             !DG!Tools/Dev!RS!
echo !CY!   9!RS! Hash.Forge            !DG!Tools/Dev!RS!
echo !CY!  10!RS! Log.Lens              !DG!Tools/Dev!RS!
echo !CY!  11!RS! Mock.Desk             !DG!Tools/Dev!RS!
echo.
echo !BD!!DG!  Tools / Network!RS!
echo !CY!  12!RS! DNS.Flip              !DG!Tools/Network!RS!
echo !CY!  13!RS! Port.Watch            !DG!Tools/Network!RS!
echo.
echo !BD!!DG!  Tools / Productivity!RS!
echo !CY!  14!RS! Clipboard.Stacker     !DG!Tools/Productivity!RS!
echo !CY!  15!RS! Screen.Recorder       !DG!Tools/Productivity!RS!
echo !CY!  16!RS! Text.Forge            !DG!Tools/Productivity!RS!
echo.
echo !BD!!DG!  Tools / System!RS!
echo !CY!  17!RS! Env.Guard             !DG!Tools/System!RS!
echo.
echo !BD!!DG!  Games!RS!
echo !CY!  18!RS! Dungeon.Dash          !DG!Games/Action!RS!
echo !CY!  19!RS! Brick.Blitz           !DG!Games/Arcade!RS!
echo !CY!  20!RS! Dash.City             !DG!Games/Arcade!RS!
echo !CY!  21!RS! Neon.Run              !DG!Games/Arcade!RS!
echo !CY!  22!RS! Gravity.Flip          !DG!Games/Puzzle!RS!
echo !CY!  23!RS! Hue.Flow              !DG!Games/Puzzle!RS!
echo !CY!  24!RS! Nitro.Drift           !DG!Games/Racing!RS!
echo !CY!  25!RS! Beat.Drop             !DG!Games/Rhythm!RS!
echo !CY!  26!RS! Dodge.Blitz           !DG!Games/Shooter!RS!
echo !CY!  27!RS! Star.Strike           !DG!Games/Shooter!RS!
echo !CY!  28!RS! Tower.Guard           !DG!Games/Strategy!RS!
echo.
echo !DG!--------------------------------------------------!RS!
echo !DG!  번호 입력 (공백/쉼표로 구분)   예: 1 3 5  또는  1,3,5!RS!
echo !DG!  전체 배포: ALL   ^|   취소: Q!RS!
echo.
set /p "SEL=  선택 > "

:: ── 입력 검증 ─────────────────────────────────────────────────────
if /i "!SEL!"=="Q"   goto :EOF
if /i "!SEL!"=="ALL" goto :PUBALL
if "!SEL!"==""       goto :MENU

:: 쉼표를 공백으로 변환
set "SEL=!SEL:,= !"

:: ── 선택 배포 ─────────────────────────────────────────────────────
set /a TOTAL=0
set /a PASS=0
set /a FAIL=0
set "FAILED="

echo.
echo !BD!!CY!Playground ^| Publish (선택)!RS!
echo !DG!Output: %BIN%!RS!
echo !DG!--------------------------------------------------!RS!
echo.

for %%n in (!SEL!) do (
    if "%%n"=="1"  call :pub "AI.Clip"               "Applications\AI\AI.Clip"                           "AiClip.exe"           "Applications\AI"
    if "%%n"=="2"  call :pub "Music.Player"           "Applications\Audio\Music.Player"                   "MusicPlayer.exe"      "Applications\Audio"
    if "%%n"=="3"  call :pub "Stay.Awake"             "Applications\Automation\Stay.Awake"                "StayAwake.exe"        "Applications\Automation\StayAwake"
    if "%%n"=="4"  call :pub "Batch.Rename"           "Applications\Files\Batch.Rename"                   "BatchRename.exe"      "Applications\Files"
    if "%%n"=="5"  call :pub "File.Duplicates"        "Applications\Files\File.Duplicates"                "FileDuplicates.exe"   "Applications\Files"
    if "%%n"=="6"  call :pub "Toast.Cast"             "Applications\Health\Toast.Cast"                    "ToastCast.exe"        "Applications\Health"
    if "%%n"=="7"  call :pub "Photo.Video.Organizer"  "Applications\Media\Photo.Video.Organizer"          "PhotoVideoOrganizer.exe" "Applications\Media"
    if "%%n"=="8"  call :pub "Api.Probe"              "Applications\Tools\Dev\Api.Probe"                  "Api.Probe.exe"        "Applications\Tools\Dev\Api.Probe"
    if "%%n"=="9"  call :pub "Hash.Forge"             "Applications\Tools\Dev\Hash.Forge"                 "HashForge.exe"        "Applications\Tools\Dev"
    if "%%n"=="10" call :pub "Log.Lens"               "Applications\Tools\Dev\Log.Lens"                   "LogLens.exe"          "Applications\Tools\Dev"
    if "%%n"=="11" call :pub "Mock.Desk"              "Applications\Tools\Dev\Mock.Desk"                  "Mock.Desk.exe"        "Applications\Tools\Dev\Mock.Desk"
    if "%%n"=="12" call :pub "DNS.Flip"               "Applications\Tools\Network\DNS.Flip"               "DnsFlip.exe"          "Applications\Tools\Network"
    if "%%n"=="13" call :pub "Port.Watch"             "Applications\Tools\Network\Port.Watch"             "PortWatch.exe"        "Applications\Tools\Network"
    if "%%n"=="14" call :pub "Clipboard.Stacker"      "Applications\Tools\Productivity\Clipboard.Stacker" "ClipboardStacker.exe" "Applications\Tools\Productivity"
    if "%%n"=="15" call :pub "Screen.Recorder"        "Applications\Tools\Productivity\Screen.Recorder"   "ScreenRecorder.exe"   "Applications\Tools\Productivity"
    if "%%n"=="16" call :pub "Text.Forge"             "Applications\Tools\Productivity\Text.Forge"        "TextForge.exe"        "Applications\Tools\Productivity"
    if "%%n"=="17" call :pub "Env.Guard"              "Applications\Tools\System\Env.Guard"               "EnvGuard.exe"         "Applications\Tools\System"
    if "%%n"=="18" call :pub "Dungeon.Dash"           "Games\Action\Dungeon.Dash"                         "DungeonDash.exe"      "Games\Action"
    if "%%n"=="19" call :pub "Brick.Blitz"            "Games\Arcade\Brick.Blitz"                          "BrickBlitz.exe"       "Games\Arcade"
    if "%%n"=="20" call :pub "Dash.City"              "Games\Arcade\Dash.City"                            "DashCity.exe"         "Games\Arcade"
    if "%%n"=="21" call :pub "Neon.Run"               "Games\Arcade\Neon.Run"                             "NeonRun.exe"          "Games\Arcade"
    if "%%n"=="22" call :pub "Gravity.Flip"           "Games\Puzzle\Gravity.Flip"                         "GravityFlip.exe"      "Games\Puzzle"
    if "%%n"=="23" call :pub "Hue.Flow"               "Games\Puzzle\Hue.Flow"                             "HueFlow.exe"          "Games\Puzzle"
    if "%%n"=="24" call :pub "Nitro.Drift"            "Games\Racing\Nitro.Drift"                          "NitroDrift.exe"       "Games\Racing"
    if "%%n"=="25" call :pub "Beat.Drop"              "Games\Rhythm\Beat.Drop"                            "BeatDrop.exe"         "Games\Rhythm"
    if "%%n"=="26" call :pub "Dodge.Blitz"            "Games\Shooter\Dodge.Blitz"                         "DodgeBlitz.exe"       "Games\Shooter"
    if "%%n"=="27" call :pub "Star.Strike"            "Games\Shooter\Star.Strike"                         "StarStrike.exe"       "Games\Shooter"
    if "%%n"=="28" call :pub "Tower.Guard"            "Games\Strategy\Tower.Guard"                        "TowerGuard.exe"       "Games\Strategy"
)
goto :DONE

:: ── 전체 배포 ─────────────────────────────────────────────────────
:PUBALL
set /a TOTAL=0
set /a PASS=0
set /a FAIL=0
set "FAILED="

echo.
echo !BD!!CY!Playground ^| Publish-All!RS!
echo !DG!Output: %BIN%!RS!
echo !DG!--------------------------------------------------!RS!
echo.

call :pub "AI.Clip"               "Applications\AI\AI.Clip"                           "AiClip.exe"           "Applications\AI"
call :pub "Music.Player"          "Applications\Audio\Music.Player"                   "MusicPlayer.exe"      "Applications\Audio"
call :pub "Stay.Awake"            "Applications\Automation\Stay.Awake"                "StayAwake.exe"        "Applications\Automation\StayAwake"
call :pub "Batch.Rename"          "Applications\Files\Batch.Rename"                   "BatchRename.exe"      "Applications\Files"
call :pub "File.Duplicates"       "Applications\Files\File.Duplicates"                "FileDuplicates.exe"   "Applications\Files"
call :pub "Toast.Cast"            "Applications\Health\Toast.Cast"                    "ToastCast.exe"        "Applications\Health"
call :pub "Photo.Video.Organizer" "Applications\Media\Photo.Video.Organizer"          "PhotoVideoOrganizer.exe" "Applications\Media"
call :pub "Api.Probe"             "Applications\Tools\Dev\Api.Probe"                  "Api.Probe.exe"        "Applications\Tools\Dev\Api.Probe"
call :pub "Hash.Forge"            "Applications\Tools\Dev\Hash.Forge"                 "HashForge.exe"        "Applications\Tools\Dev"
call :pub "Log.Lens"              "Applications\Tools\Dev\Log.Lens"                   "LogLens.exe"          "Applications\Tools\Dev"
call :pub "Mock.Desk"             "Applications\Tools\Dev\Mock.Desk"                  "Mock.Desk.exe"        "Applications\Tools\Dev\Mock.Desk"
call :pub "DNS.Flip"              "Applications\Tools\Network\DNS.Flip"               "DnsFlip.exe"          "Applications\Tools\Network"
call :pub "Port.Watch"            "Applications\Tools\Network\Port.Watch"             "PortWatch.exe"        "Applications\Tools\Network"
call :pub "Clipboard.Stacker"     "Applications\Tools\Productivity\Clipboard.Stacker" "ClipboardStacker.exe" "Applications\Tools\Productivity"
call :pub "Screen.Recorder"       "Applications\Tools\Productivity\Screen.Recorder"   "ScreenRecorder.exe"   "Applications\Tools\Productivity"
call :pub "Text.Forge"            "Applications\Tools\Productivity\Text.Forge"        "TextForge.exe"        "Applications\Tools\Productivity"
call :pub "Env.Guard"             "Applications\Tools\System\Env.Guard"               "EnvGuard.exe"         "Applications\Tools\System"
call :pub "Dungeon.Dash"          "Games\Action\Dungeon.Dash"                         "DungeonDash.exe"      "Games\Action"
call :pub "Brick.Blitz"           "Games\Arcade\Brick.Blitz"                          "BrickBlitz.exe"       "Games\Arcade"
call :pub "Dash.City"             "Games\Arcade\Dash.City"                            "DashCity.exe"         "Games\Arcade"
call :pub "Neon.Run"              "Games\Arcade\Neon.Run"                             "NeonRun.exe"          "Games\Arcade"
call :pub "Gravity.Flip"          "Games\Puzzle\Gravity.Flip"                         "GravityFlip.exe"      "Games\Puzzle"
call :pub "Hue.Flow"              "Games\Puzzle\Hue.Flow"                             "HueFlow.exe"          "Games\Puzzle"
call :pub "Nitro.Drift"           "Games\Racing\Nitro.Drift"                          "NitroDrift.exe"       "Games\Racing"
call :pub "Beat.Drop"             "Games\Rhythm\Beat.Drop"                            "BeatDrop.exe"         "Games\Rhythm"
call :pub "Dodge.Blitz"           "Games\Shooter\Dodge.Blitz"                         "DodgeBlitz.exe"       "Games\Shooter"
call :pub "Star.Strike"           "Games\Shooter\Star.Strike"                         "StarStrike.exe"       "Games\Shooter"
call :pub "Tower.Guard"           "Games\Strategy\Tower.Guard"                        "TowerGuard.exe"       "Games\Strategy"

:: ── 완료 ──────────────────────────────────────────────────────────
:DONE
echo !DG!Cleaning .pdb files...!RS!
del /s /q "%BIN%\*.pdb" 2>nul
echo !DG!--------------------------------------------------!RS!
echo.
if !FAIL! gtr 0 echo !BD!!RE!Result: !PASS!/!TOTAL! OK  ^|  Failed:!FAILED!!RS!
if !FAIL! equ 0 echo !BD!!GR!Result: !PASS!/!TOTAL! All succeeded!RS!
echo.

set "NOTIFY=%ROOT%.claude\Scripts\notify\notify.ps1"
if !FAIL! equ 0 (
    set "MSG=Publish 성공: !PASS!/!TOTAL! 프로젝트 배포 완료"
    set "LV=Info"
) else (
    set "MSG=Publish 실패: !PASS!/!TOTAL! 성공, 실패:!FAILED!"
    set "LV=Error"
)
powershell -ExecutionPolicy Bypass -File "%NOTIFY%" -Message "!MSG!" -Level !LV! -Title "Playground Publish"

pause
goto :eof

:: --------------------------------------------------------------------------
:pub
set /a TOTAL+=1
set "NM=%~1"
set "DIR=%ROOT%%~2"
set "EX=%~3"
set "OUT=%BIN%\%~4"
if not exist "!OUT!" mkdir "!OUT!"
echo !CY!  ^> %NM%!RS!
echo !DG!    %~4\%EX%!RS!
dotnet publish "%DIR%" -c Release -o "!OUT!" > "%TEMP%\pub_%NM%.log" 2>&1
set "RC=!errorlevel!"
if !RC! equ 0 set /a PASS+=1
if !RC! equ 0 echo !GR!    [OK]!RS!
if !RC! neq 0 set /a FAIL+=1
if !RC! neq 0 set "FAILED=!FAILED! %NM%"
if !RC! neq 0 echo !RE!    [!!]  Failed - log: %TEMP%\pub_%NM%.log!RS!
echo.
goto :eof
