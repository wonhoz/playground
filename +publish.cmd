@echo off
chcp 65001 >nul
setlocal enabledelayedexpansion

set "ROOT=%~dp0"
set "BIN=%ROOT%bin"

:: ANSI color vars - must be set before any call (prevents [90m misparse in subroutine)
for /f %%a in ('echo prompt $E ^| cmd /q') do set "ESC=%%a"
set "CY=!ESC![96m"
set "GR=!ESC![92m"
set "RE=!ESC![91m"
set "DG=!ESC![90m"
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
echo !CY!   5!RS! Disk.Lens             !DG!Applications/Files!RS!
echo !CY!   6!RS! File.Duplicates       !DG!Applications/Files!RS!
echo !CY!   7!RS! File.Unlocker         !DG!Applications/Files!RS!
echo !CY!   8!RS! Folder.Purge          !DG!Applications/Files!RS!
echo !CY!   9!RS! Toast.Cast            !DG!Applications/Health!RS!
echo !CY!  10!RS! Photo.Video.Organizer !DG!Applications/Media!RS!
echo.
echo !BD!!DG!  Tools / Dev!RS!
echo !CY!  11!RS! Api.Probe             !DG!Tools/Dev!RS!
echo !CY!  12!RS! Glyph.Map             !DG!Tools/Dev!RS!
echo !CY!  13!RS! Hex.Peek              !DG!Tools/Dev!RS!
echo !CY!  14!RS! Log.Lens              !DG!Tools/Dev!RS!
echo !CY!  15!RS! Mock.Server           !DG!Tools/Dev!RS!
echo !CY!  16!RS! Signal.Flow           !DG!Tools/Dev!RS!
echo !CY!  17!RS! Serve.Cast            !DG!Tools/Dev!RS!
echo !CY!  18!RS! Table.Craft           !DG!Tools/Dev!RS!
echo.
echo !BD!!DG!  Tools / Network!RS!
echo !CY!  19!RS! DNS.Flip              !DG!Tools/Network!RS!
echo !CY!  20!RS! Port.Watch            !DG!Tools/Network!RS!
echo.
echo !BD!!DG!  Tools / Productivity!RS!
echo !CY!  21!RS! Clipboard.Stacker     !DG!Tools/Productivity!RS!
echo !CY!  22!RS! Code.Snap             !DG!Tools/Productivity!RS!
echo !CY!  23!RS! QR.Forge              !DG!Tools/Productivity!RS!
echo !CY!  24!RS! Screen.Recorder       !DG!Tools/Productivity!RS!
echo !CY!  25!RS! Text.Forge            !DG!Tools/Productivity!RS!
echo !CY!  26!RS! Word.Cloud            !DG!Tools/Productivity!RS!
echo !CY!  27!RS! Char.Art              !DG!Tools/Productivity!RS!
echo !CY!  28!RS! Mark.View             !DG!Tools/Productivity!RS!
echo.
echo !BD!!DG!  Tools / System!RS!
echo !CY!  29!RS! Env.Guard             !DG!Tools/System!RS!
echo.
echo !BD!!DG!  Games!RS!
echo !CY!  30!RS! Dungeon.Dash          !DG!Games/Action!RS!
echo !CY!  31!RS! Brick.Blitz           !DG!Games/Arcade!RS!
echo !CY!  32!RS! Dash.City             !DG!Games/Arcade!RS!
echo !CY!  33!RS! Neon.Run              !DG!Games/Arcade!RS!
echo !CY!  34!RS! Neon.Slice            !DG!Games/Arcade!RS!
echo !CY!  35!RS! Gravity.Flip          !DG!Games/Puzzle!RS!
echo !CY!  36!RS! Hue.Flow              !DG!Games/Puzzle!RS!
echo !CY!  37!RS! Orbit.Craft           !DG!Games/Puzzle!RS!
echo !CY!  38!RS! Nitro.Drift           !DG!Games/Racing!RS!
echo !CY!  39!RS! Beat.Drop             !DG!Games/Rhythm!RS!
echo !CY!  40!RS! Sand.Fall             !DG!Games/Sandbox!RS!
echo !CY!  41!RS! Dodge.Blitz           !DG!Games/Shooter!RS!
echo !CY!  42!RS! Star.Strike           !DG!Games/Shooter!RS!
echo !CY!  43!RS! Tower.Guard           !DG!Games/Strategy!RS!
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
    if "%%n"=="1"  call :pub "AI.Clip"               "Applications\AI\AI.Clip"                           "Ai.Clip.exe"               "Applications\AI"
    if "%%n"=="2"  call :pub "Music.Player"           "Applications\Audio\Music.Player"                   "Music.Player.exe"          "Applications\Audio"
    if "%%n"=="3"  call :pub "Stay.Awake"             "Applications\Automation\Stay.Awake"                "Stay.Awake.exe"            "Applications\Automation\Stay.Awake"
    if "%%n"=="4"  call :pub "Batch.Rename"           "Applications\Files\Batch.Rename"                   "Batch.Rename.exe"          "Applications\Files"
    if "%%n"=="5"  call :pub "Disk.Lens"              "Applications\Files\Disk.Lens"                      "DiskLens.exe"              "Applications\Files"
    if "%%n"=="6"  call :pub "File.Duplicates"        "Applications\Files\File.Duplicates"                "File.Duplicates.exe"       "Applications\Files"
    if "%%n"=="7"  call :pub "File.Unlocker"          "Applications\Files\File.Unlocker"                  "File.Unlocker.exe"         "Applications\Files"
    if "%%n"=="8"  call :pub "Folder.Purge"           "Applications\Files\Folder.Purge"                   "Folder.Purge.exe"          "Applications\Files"
    if "%%n"=="9"  call :pub "Toast.Cast"             "Applications\Health\Toast.Cast"                    "Toast.Cast.exe"            "Applications\Health"
    if "%%n"=="10" call :pub "Photo.Video.Organizer"  "Applications\Media\Photo.Video.Organizer"          "Photo.Video.Organizer.exe" "Applications\Media"
    if "%%n"=="11" call :pub "Api.Probe"              "Applications\Tools\Dev\Api.Probe"                  "Api.Probe.exe"             "Applications\Tools\Dev\Api.Probe"
    if "%%n"=="12" call :pub "Glyph.Map"              "Applications\Tools\Dev\Glyph.Map"                  "Glyph.Map.exe"             "Applications\Tools\Dev\Glyph.Map"
    if "%%n"=="13" call :pub "Hex.Peek"               "Applications\Tools\Dev\Hex.Peek"                   "Hex.Peek.exe"              "Applications\Tools\Dev"
    if "%%n"=="14" call :pub "Log.Lens"               "Applications\Tools\Dev\Log.Lens"                   "Log.Lens.exe"              "Applications\Tools\Dev"
    if "%%n"=="15" call :pub "Mock.Server"            "Applications\Tools\Dev\Mock.Server"                "Mock.Server.exe"           "Applications\Tools\Dev"
    if "%%n"=="16" call :pub "Signal.Flow"            "Applications\Tools\Dev\Signal.Flow"                "Signal.Flow.exe"           "Applications\Tools\Dev"
    if "%%n"=="17" call :pub "Serve.Cast"             "Applications\Tools\Dev\Serve.Cast"                 "ServeCast.exe"             "Applications\Tools\Dev"
    if "%%n"=="18" call :pub "Table.Craft"            "Applications\Tools\Dev\Table.Craft"                "TableCraft.exe"            "Applications\Tools\Dev"
    if "%%n"=="19" call :pub "DNS.Flip"               "Applications\Tools\Network\DNS.Flip"               "Dns.Flip.exe"              "Applications\Tools\Network"
    if "%%n"=="20" call :pub "Port.Watch"             "Applications\Tools\Network\Port.Watch"             "Port.Watch.exe"            "Applications\Tools\Network"
    if "%%n"=="21" call :pub "Clipboard.Stacker"      "Applications\Tools\Productivity\Clipboard.Stacker" "Clipboard.Stacker.exe"     "Applications\Tools\Productivity"
    if "%%n"=="22" call :pub "Code.Snap"              "Applications\Tools\Productivity\Code.Snap"         "Code.Snap.exe"             "Applications\Tools\Productivity"
    if "%%n"=="23" call :pub "QR.Forge"               "Applications\Tools\Productivity\QR.Forge"          "QR.Forge.exe"              "Applications\Tools\Productivity"
    if "%%n"=="24" call :pub "Screen.Recorder"        "Applications\Tools\Productivity\Screen.Recorder"   "Screen.Recorder.exe"       "Applications\Tools\Productivity"
    if "%%n"=="25" call :pub "Text.Forge"             "Applications\Tools\Productivity\Text.Forge"        "Text.Forge.exe"            "Applications\Tools\Productivity"
    if "%%n"=="26" call :pub "Word.Cloud"             "Applications\Tools\Productivity\Word.Cloud"        "Word.Cloud.exe"            "Applications\Tools\Productivity"
    if "%%n"=="27" call :pub "Char.Art"               "Applications\Tools\Productivity\Char.Art"          "Char.Art.exe"              "Applications\Tools\Productivity"
    if "%%n"=="28" call :pub "Mark.View"              "Applications\Tools\Productivity\Mark.View"         "Mark.View.exe"             "Applications\Tools\Productivity"
    if "%%n"=="29" call :pub "Env.Guard"              "Applications\Tools\System\Env.Guard"               "Env.Guard.exe"             "Applications\Tools\System"
    if "%%n"=="30" call :pub "Dungeon.Dash"           "Games\Action\Dungeon.Dash"                         "Dungeon.Dash.exe"          "Games\Action"
    if "%%n"=="31" call :pub "Brick.Blitz"            "Games\Arcade\Brick.Blitz"                          "Brick.Blitz.exe"           "Games\Arcade"
    if "%%n"=="32" call :pub "Dash.City"              "Games\Arcade\Dash.City"                            "Dash.City.exe"             "Games\Arcade"
    if "%%n"=="33" call :pub "Neon.Run"               "Games\Arcade\Neon.Run"                             "Neon.Run.exe"              "Games\Arcade"
    if "%%n"=="34" call :pub "Neon.Slice"             "Games\Arcade\Neon.Slice"                           "Neon.Slice.exe"            "Games\Arcade\Neon.Slice"
    if "%%n"=="35" call :pub "Gravity.Flip"           "Games\Puzzle\Gravity.Flip"                         "Gravity.Flip.exe"          "Games\Puzzle"
    if "%%n"=="36" call :pub "Hue.Flow"               "Games\Puzzle\Hue.Flow"                             "Hue.Flow.exe"              "Games\Puzzle"
    if "%%n"=="37" call :pub "Orbit.Craft"            "Games\Puzzle\Orbit.Craft"                          "OrbitCraft.exe"            "Games\Puzzle"
    if "%%n"=="38" call :pub "Nitro.Drift"            "Games\Racing\Nitro.Drift"                          "Nitro.Drift.exe"           "Games\Racing"
    if "%%n"=="39" call :pub "Beat.Drop"              "Games\Rhythm\Beat.Drop"                            "Beat.Drop.exe"             "Games\Rhythm"
    if "%%n"=="40" call :pub "Sand.Fall"              "Games\Sandbox\Sand.Fall"                           "SandFall.exe"              "Games\Sandbox"
    if "%%n"=="41" call :pub "Dodge.Blitz"            "Games\Shooter\Dodge.Blitz"                         "Dodge.Blitz.exe"           "Games\Shooter"
    if "%%n"=="42" call :pub "Star.Strike"            "Games\Shooter\Star.Strike"                         "Star.Strike.exe"           "Games\Shooter"
    if "%%n"=="43" call :pub "Tower.Guard"            "Games\Strategy\Tower.Guard"                        "Tower.Guard.exe"           "Games\Strategy"
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

call :pub "AI.Clip"               "Applications\AI\AI.Clip"                           "Ai.Clip.exe"               "Applications\AI"
call :pub "Music.Player"          "Applications\Audio\Music.Player"                   "Music.Player.exe"          "Applications\Audio"
call :pub "Stay.Awake"            "Applications\Automation\Stay.Awake"                "Stay.Awake.exe"            "Applications\Automation\Stay.Awake"
call :pub "Batch.Rename"          "Applications\Files\Batch.Rename"                   "Batch.Rename.exe"          "Applications\Files"
call :pub "Disk.Lens"             "Applications\Files\Disk.Lens"                      "DiskLens.exe"              "Applications\Files"
call :pub "File.Duplicates"       "Applications\Files\File.Duplicates"                "File.Duplicates.exe"       "Applications\Files"
call :pub "File.Unlocker"         "Applications\Files\File.Unlocker"                  "File.Unlocker.exe"         "Applications\Files"
call :pub "Folder.Purge"          "Applications\Files\Folder.Purge"                   "Folder.Purge.exe"          "Applications\Files"
call :pub "Toast.Cast"            "Applications\Health\Toast.Cast"                    "Toast.Cast.exe"            "Applications\Health"
call :pub "Photo.Video.Organizer" "Applications\Media\Photo.Video.Organizer"          "Photo.Video.Organizer.exe" "Applications\Media"
call :pub "Api.Probe"             "Applications\Tools\Dev\Api.Probe"                  "Api.Probe.exe"             "Applications\Tools\Dev\Api.Probe"
call :pub "Glyph.Map"             "Applications\Tools\Dev\Glyph.Map"                  "Glyph.Map.exe"             "Applications\Tools\Dev\Glyph.Map"
call :pub "Hex.Peek"              "Applications\Tools\Dev\Hex.Peek"                   "Hex.Peek.exe"              "Applications\Tools\Dev"
call :pub "Log.Lens"              "Applications\Tools\Dev\Log.Lens"                   "Log.Lens.exe"              "Applications\Tools\Dev"
call :pub "Mock.Server"           "Applications\Tools\Dev\Mock.Server"                "Mock.Server.exe"           "Applications\Tools\Dev"
call :pub "Signal.Flow"           "Applications\Tools\Dev\Signal.Flow"                "Signal.Flow.exe"           "Applications\Tools\Dev"
call :pub "Serve.Cast"            "Applications\Tools\Dev\Serve.Cast"                 "ServeCast.exe"             "Applications\Tools\Dev"
call :pub "Table.Craft"           "Applications\Tools\Dev\Table.Craft"                "TableCraft.exe"            "Applications\Tools\Dev"
call :pub "DNS.Flip"              "Applications\Tools\Network\DNS.Flip"               "Dns.Flip.exe"              "Applications\Tools\Network"
call :pub "Port.Watch"            "Applications\Tools\Network\Port.Watch"             "Port.Watch.exe"            "Applications\Tools\Network"
call :pub "Clipboard.Stacker"     "Applications\Tools\Productivity\Clipboard.Stacker" "Clipboard.Stacker.exe"     "Applications\Tools\Productivity"
call :pub "Code.Snap"             "Applications\Tools\Productivity\Code.Snap"         "Code.Snap.exe"             "Applications\Tools\Productivity"
call :pub "QR.Forge"              "Applications\Tools\Productivity\QR.Forge"          "QR.Forge.exe"              "Applications\Tools\Productivity"
call :pub "Screen.Recorder"       "Applications\Tools\Productivity\Screen.Recorder"   "Screen.Recorder.exe"       "Applications\Tools\Productivity"
call :pub "Text.Forge"            "Applications\Tools\Productivity\Text.Forge"        "Text.Forge.exe"            "Applications\Tools\Productivity"
call :pub "Word.Cloud"            "Applications\Tools\Productivity\Word.Cloud"        "Word.Cloud.exe"            "Applications\Tools\Productivity"
call :pub "Char.Art"              "Applications\Tools\Productivity\Char.Art"          "Char.Art.exe"              "Applications\Tools\Productivity"
call :pub "Mark.View"             "Applications\Tools\Productivity\Mark.View"         "Mark.View.exe"             "Applications\Tools\Productivity"
call :pub "Env.Guard"             "Applications\Tools\System\Env.Guard"               "Env.Guard.exe"             "Applications\Tools\System"
call :pub "Dungeon.Dash"          "Games\Action\Dungeon.Dash"                         "Dungeon.Dash.exe"          "Games\Action"
call :pub "Brick.Blitz"           "Games\Arcade\Brick.Blitz"                          "Brick.Blitz.exe"           "Games\Arcade"
call :pub "Dash.City"             "Games\Arcade\Dash.City"                            "Dash.City.exe"             "Games\Arcade"
call :pub "Neon.Run"              "Games\Arcade\Neon.Run"                             "Neon.Run.exe"              "Games\Arcade"
call :pub "Neon.Slice"            "Games\Arcade\Neon.Slice"                           "Neon.Slice.exe"            "Games\Arcade\Neon.Slice"
call :pub "Gravity.Flip"          "Games\Puzzle\Gravity.Flip"                         "Gravity.Flip.exe"          "Games\Puzzle"
call :pub "Hue.Flow"              "Games\Puzzle\Hue.Flow"                             "Hue.Flow.exe"              "Games\Puzzle"
call :pub "Orbit.Craft"           "Games\Puzzle\Orbit.Craft"                          "OrbitCraft.exe"            "Games\Puzzle"
call :pub "Nitro.Drift"           "Games\Racing\Nitro.Drift"                          "Nitro.Drift.exe"           "Games\Racing"
call :pub "Beat.Drop"             "Games\Rhythm\Beat.Drop"                            "Beat.Drop.exe"             "Games\Rhythm"
call :pub "Sand.Fall"             "Games\Sandbox\Sand.Fall"                           "SandFall.exe"              "Games\Sandbox"
call :pub "Dodge.Blitz"           "Games\Shooter\Dodge.Blitz"                         "Dodge.Blitz.exe"           "Games\Shooter"
call :pub "Star.Strike"           "Games\Shooter\Star.Strike"                         "Star.Strike.exe"           "Games\Shooter"
call :pub "Tower.Guard"           "Games\Strategy\Tower.Guard"                        "Tower.Guard.exe"           "Games\Strategy"

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
