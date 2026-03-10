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

set /a TOTAL=0
set /a PASS=0
set /a FAIL=0
set "FAILED="

echo.
echo !BD!!CY!Playground ^| Publish-All!RS!
echo !DG!Output: %BIN%!RS!
echo !DG!--------------------------------------------------!RS!
echo.

:: ── Applications / AI ─────────────────────────────────────────────
call :pub "AI.Clip"               "Applications\AI\AI.Clip"                                "Ai.Clip.exe"               "Applications\AI"
call :pub "Prompt.Forge"          "Applications\AI\Prompt.Forge"                           "Prompt.Forge.exe"          "Applications\AI\Prompt.Forge"

:: ── Applications / Audio ──────────────────────────────────────────
call :pub "Music.Player"          "Applications\Audio\Music.Player"                        "Music.Player.exe"          "Applications\Audio"
call :pub "Tag.Forge"             "Applications\Audio\Tag.Forge"                           "Tag.Forge.exe"             "Applications\Audio\Tag.Forge"

:: ── Applications / Automation ─────────────────────────────────────
call :pub "Stay.Awake"            "Applications\Automation\Stay.Awake"                     "Stay.Awake.exe"            "Applications\Automation\Stay.Awake"

:: ── Applications / Data ───────────────────────────────────────────
call :pub "Table.Craft"           "Applications\Data\Table.Craft"                          "Table.Craft.exe"           "Applications\Data"
call :pub "Timeline.Craft"        "Applications\Data\Timeline.Craft"                       "Timeline.Craft.exe"        "Applications\Data\Timeline.Craft"

:: ── Applications / Development ────────────────────────────────────
call :pub "App.Temp"              "Applications\Development\App.Temp"                      "App.Temp.exe"              "Applications\Development"
call :pub "Hex.Peek"              "Applications\Development\Hex.Peek"                      "Hex.Peek.exe"              "Applications\Development"
call :pub "Log.Lens"              "Applications\Development\Log.Lens"                      "Log.Lens.exe"              "Applications\Development"
call :pub "Quick.Calc"            "Applications\Development\Quick.Calc"                    "Quick.Calc.exe"            "Applications\Development"
call :pub "Signal.Flow"           "Applications\Development\Signal.Flow"                   "Signal.Flow.exe"           "Applications\Development"
call :pub "Win.Event"             "Applications\Development\Win.Event"                     "WinEvent.exe"              "Applications\Development"

:: ── Applications / Emoji.Icon ─────────────────────────────────────
call :pub "Glyph.Map"             "Applications\Emoji.Icon\Glyph.Map"                      "Glyph.Map.exe"             "Applications\Emoji.Icon\Glyph.Map"
call :pub "Icon.Hunt"             "Applications\Emoji.Icon\Icon.Hunt"                      "Icon.Hunt.exe"             "Applications\Emoji.Icon"

:: ── Applications / Files / Inspector ─────────────────────────────
call :pub "Disk.Lens"             "Applications\Files\Inspector\Disk.Lens"                 "Disk.Lens.exe"             "Applications\Files\Inspector\Disk.Lens"
call :pub "Hash.Check"            "Applications\Files\Inspector\Hash.Check"                "Hash.Check.exe"            "Applications\Files\Inspector\Hash.Check"
call :pub "PDF.Forge"             "Applications\Files\Inspector\PDF.Forge"                 "Pdf.Forge.exe"             "Applications\Files\Inspector\Pdf.Forge"
call :pub "Zip.Peek"              "Applications\Files\Inspector\Zip.Peek"                  "Zip.Peek.exe"              "Applications\Files\Inspector\Zip.Peek"

:: ── Applications / Files / Manager ───────────────────────────────
call :pub "Batch.Rename"          "Applications\Files\Manager\Batch.Rename"                "Batch.Rename.exe"          "Applications\Files\Manager"
call :pub "File.Duplicates"       "Applications\Files\Manager\File.Duplicates"             "File.Duplicates.exe"       "Applications\Files\Manager"
call :pub "File.Unlocker"         "Applications\Files\Manager\File.Unlocker"               "File.Unlocker.exe"         "Applications\Files\Manager"
call :pub "Folder.Purge"          "Applications\Files\Manager\Folder.Purge"                "Folder.Purge.exe"          "Applications\Files\Manager"

:: ── Applications / Network ────────────────────────────────────────
call :pub "Api.Probe"             "Applications\Network\Api.Probe"                         "Api.Probe.exe"             "Applications\Network\Api.Probe"
call :pub "DNS.Flip"              "Applications\Network\DNS.Flip"                          "Dns.Flip.exe"              "Applications\Network"
call :pub "Mock.Server"           "Applications\Network\Mock.Server"                       "Mock.Server.exe"           "Applications\Network"
call :pub "Net.Scan"              "Applications\Network\Net.Scan"                          "Net.Scan.exe"              "Applications\Network\Net.Scan"
call :pub "Port.Watch"            "Applications\Network\Port.Watch"                        "Port.Watch.exe"            "Applications\Network"
call :pub "Serve.Cast"            "Applications\Network\Serve.Cast"                        "Serve.Cast.exe"            "Applications\Network"

:: ── Applications / Photo.Picture ─────────────────────────────────
call :pub "Color.Grade"           "Applications\Photo.Picture\Color.Grade"                 "Color.Grade.exe"           "Applications\Photo.Picture\Color.Grade"
call :pub "Mosaic.Forge"          "Applications\Photo.Picture\Mosaic.Forge"                "Mosaic.Forge.exe"          "Applications\Photo.Picture\Mosaic.Forge"
call :pub "Photo.Video.Organizer" "Applications\Photo.Picture\Photo.Video.Organizer"       "Photo.Video.Organizer.exe" "Applications\Photo.Picture"
call :pub "Web.Shot"              "Applications\Photo.Picture\Web.Shot"                    "Web.Shot.exe"              "Applications\Photo.Picture\Web.Shot"

:: ── Applications / System ─────────────────────────────────────────
call :pub "Boot.Map"              "Applications\System\Boot.Map"                           "Boot.Map.exe"              "Applications\System"
call :pub "Drive.Bench"          "Applications\System\Drive.Bench"                        "DriveBench.exe"            "Applications\System"
call :pub "Burn.Rate"             "Applications\System\Burn.Rate"                          "Burn.Rate.exe"             "Applications\System"
call :pub "Env.Guard"             "Applications\System\Env.Guard"                          "Env.Guard.exe"             "Applications\System"
call :pub "Ext.Boss"              "Applications\System\Ext.Boss"                           "Ext.Boss.exe"              "Applications\System"
call :pub "Key.Map"               "Applications\System\Key.Map"                            "Key.Map.exe"               "Applications\System\Key.Map"
call :pub "Sched.Cast"            "Applications\System\Sched.Cast"                         "Sched.Cast.exe"            "Applications\System\Sched.Cast"
call :pub "Spec.Report"           "Applications\System\Spec.Report"                        "Spec.Report.exe"           "Applications\System"
call :pub "Spec.View"             "Applications\System\Spec.View"                          "Spec.View.exe"             "Applications\System"
call :pub "Sys.Clean"             "Applications\System\Sys.Clean"                          "Sys.Clean.exe"             "Applications\System\Sys.Clean"
call :pub "Tray.Stats"            "Applications\System\Tray.Stats"                         "Tray.Stats.exe"            "Applications\System\Tray.Stats"

:: ── Applications / Text ───────────────────────────────────────────
call :pub "Char.Art"              "Applications\Text\Char.Art"                             "Char.Art.exe"              "Applications\Text"
call :pub "Echo.Text"             "Applications\Text\Echo.Text"                            "Echo.Text.exe"             "Applications\Text\Echo.Text"
call :pub "Mark.View"             "Applications\Text\Mark.View"                            "Mark.View.exe"             "Applications\Text\Mark.View"
call :pub "Text.Forge"            "Applications\Text\Text.Forge"                           "Text.Forge.exe"            "Applications\Text"
call :pub "Word.Cloud"            "Applications\Text\Word.Cloud"                           "Word.Cloud.exe"            "Applications\Text"

:: ── Applications / Tools.Utility ─────────────────────────────────
call :pub "Clipboard.Stacker"     "Applications\Tools.Utility\Clipboard.Stacker"           "Clipboard.Stacker.exe"     "Applications\Tools.Utility"
call :pub "Dict.Cast"             "Applications\Tools.Utility\Dict.Cast"                   "Dict.Cast.exe"             "Applications\Tools.Utility\Dict.Cast"
call :pub "Mouse.Flick"           "Applications\Tools.Utility\Mouse.Flick"                 "Mouse.Flick.exe"           "Applications\Tools.Utility"
call :pub "QR.Forge"              "Applications\Tools.Utility\QR.Forge"                    "QR.Forge.exe"              "Applications\Tools.Utility"

:: ── Applications / Video ──────────────────────────────────────────
call :pub "Screen.Recorder"       "Applications\Video\Screen.Recorder"                     "Screen.Recorder.exe"       "Applications\Video"

:: ── Games / Action ───────────────────────────────────────────────
call :pub "Dungeon.Dash"          "Games\Action\Dungeon.Dash"                              "Dungeon.Dash.exe"          "Games\Action"

:: ── Games / Arcade ───────────────────────────────────────────────
call :pub "Brick.Blitz"           "Games\Arcade\Brick.Blitz"                               "Brick.Blitz.exe"           "Games\Arcade"
call :pub "Dash.City"             "Games\Arcade\Dash.City"                                 "Dash.City.exe"             "Games\Arcade"
call :pub "Neon.Run"              "Games\Arcade\Neon.Run"                                  "Neon.Run.exe"              "Games\Arcade"
call :pub "Neon.Slice"            "Games\Arcade\Neon.Slice"                                "Neon.Slice.exe"            "Games\Arcade\Neon.Slice"

:: ── Games / Casual ───────────────────────────────────────────────
call :pub "Hook.Cast"             "Games\Casual\Hook.Cast"                                 "Hook.Cast.exe"             "Games\Casual"
call :pub "Wave.Surf"             "Games\Casual\Wave.Surf"                                 "Wave.Surf.exe"             "Games\Casual"

:: ── Games / Puzzle ───────────────────────────────────────────────
call :pub "Auto.Build"            "Games\Puzzle\Auto.Build"                                "AutoBuild.exe"             "Games\Puzzle"
call :pub "Gravity.Flip"          "Games\Puzzle\Gravity.Flip"                              "Gravity.Flip.exe"          "Games\Puzzle"
call :pub "Hue.Flow"              "Games\Puzzle\Hue.Flow"                                  "Hue.Flow.exe"              "Games\Puzzle"
call :pub "Orbit.Craft"           "Games\Puzzle\Orbit.Craft"                               "Orbit.Craft.exe"           "Games\Puzzle\Orbit.Craft"

:: ── Games / Racing ───────────────────────────────────────────────
call :pub "Nitro.Drift"           "Games\Racing\Nitro.Drift"                               "Nitro.Drift.exe"           "Games\Racing"

:: ── Games / Rhythm ───────────────────────────────────────────────
call :pub "Beat.Drop"             "Games\Rhythm\Beat.Drop"                                 "Beat.Drop.exe"             "Games\Rhythm"
call :pub "Chord.Strike"          "Games\Rhythm\Chord.Strike"                              "ChordStrike.exe"           "Games\Rhythm"

:: ── Games / Sandbox ──────────────────────────────────────────────
call :pub "Sand.Fall"             "Games\Sandbox\Sand.Fall"                                "Sand.Fall.exe"             "Games\Sandbox"

:: ── Games / Shooter ──────────────────────────────────────────────
call :pub "Dodge.Blitz"           "Games\Shooter\Dodge.Blitz"                              "Dodge.Blitz.exe"           "Games\Shooter"
call :pub "Star.Strike"           "Games\Shooter\Star.Strike"                              "Star.Strike.exe"           "Games\Shooter"

:: ── Games / Simulation ────────────────────────────────────────────
call :pub "Leaf.Grow"             "Games\Simulation\Leaf.Grow"                             "Leaf.Grow.exe"             "Games\Simulation"

:: ── Games / Strategy ─────────────────────────────────────────────
call :pub "Tower.Guard"           "Games\Strategy\Tower.Guard"                             "Tower.Guard.exe"           "Games\Strategy"

:: Remove .pdb files (recursive)
echo !DG!Cleaning .pdb files...!RS!
del /s /q "%BIN%\*.pdb" 2>nul
echo !DG!--------------------------------------------------!RS!
echo.
if !FAIL! gtr 0 echo !BD!!RE!Result: !PASS!/!TOTAL! OK  ^|  Failed:!FAILED!!RS!
if !FAIL! equ 0 echo !BD!!GR!Result: !PASS!/!TOTAL! All succeeded!RS!
echo.

:: ── 알림 전송 ──────────────────────────────────────────────────────
set "NOTIFY=%ROOT%.claude\Scripts\notify\notify.ps1"

if !FAIL! equ 0 (
    set "MSG=Publish-All 성공: !PASS!/!TOTAL! 프로젝트 배포 완료"
    set "LV=Info"
) else (
    set "MSG=Publish-All 실패: !PASS!/!TOTAL! 성공, 실패:!FAILED!"
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
