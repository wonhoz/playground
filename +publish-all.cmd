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

:: ── Applications / AI ────────────────────────────────────────────
call :pub "AI.Clip"               "Applications\AI\AI.Clip"                                      "Ai.Clip.exe"               "Applications\AI"

:: ── Applications / Audio ─────────────────────────────────────────
call :pub "Music.Player"          "Applications\Audio\Music.Player"                              "Music.Player.exe"          "Applications\Audio"

:: ── Applications / Automation ────────────────────────────────────
call :pub "Stay.Awake"            "Applications\Automation\Stay.Awake"                           "Stay.Awake.exe"            "Applications\Automation\Stay.Awake"

:: ── Applications / Files / Inspector ─────────────────────────────
call :pub "Disk.Lens"             "Applications\Files\Inspector\Disk.Lens"                       "Disk.Lens.exe"             "Applications\Files\Inspector\Disk.Lens"
call :pub "Hash.Check"            "Applications\Files\Inspector\Hash.Check"                      "Hash.Check.exe"            "Applications\Files\Inspector\Hash.Check"
call :pub "PDF.Forge"             "Applications\Files\Inspector\PDF.Forge"                       "Pdf.Forge.exe"             "Applications\Files\Inspector\Pdf.Forge"
call :pub "Zip.Peek"              "Applications\Files\Inspector\Zip.Peek"                        "Zip.Peek.exe"              "Applications\Files\Inspector\Zip.Peek"

:: ── Applications / Files / Manager ───────────────────────────────
call :pub "Batch.Rename"          "Applications\Files\Manager\Batch.Rename"                      "Batch.Rename.exe"          "Applications\Files\Manager"
call :pub "File.Duplicates"       "Applications\Files\Manager\File.Duplicates"                   "File.Duplicates.exe"       "Applications\Files\Manager"
call :pub "File.Unlocker"         "Applications\Files\Manager\File.Unlocker"                     "File.Unlocker.exe"         "Applications\Files\Manager"
call :pub "Folder.Purge"          "Applications\Files\Manager\Folder.Purge"                      "Folder.Purge.exe"          "Applications\Files\Manager"

:: ── Applications / Media ─────────────────────────────────────────
call :pub "Mosaic.Forge"          "Applications\Media\Mosaic.Forge"                              "Mosaic.Forge.exe"          "Applications\Media\Mosaic.Forge"
call :pub "Photo.Video.Organizer" "Applications\Media\Photo.Video.Organizer"                     "Photo.Video.Organizer.exe" "Applications\Media"

:: ── Tools / Dev / Assets ─────────────────────────────────────────
call :pub "Glyph.Map"             "Applications\Tools\Dev\Assets\Glyph.Map"                      "Glyph.Map.exe"             "Applications\Tools\Dev\Assets\Glyph.Map"
call :pub "Icon.Hunt"             "Applications\Tools\Dev\Assets\Icon.Hunt"                      "Icon.Hunt.exe"             "Applications\Tools\Dev\Assets"
call :pub "Key.Map"               "Applications\Tools\Dev\Assets\Key.Map"                        "Key.Map.exe"               "Applications\Tools\Dev\Assets\Key.Map"
call :pub "Locale.Forge"          "Applications\Tools\Dev\Assets\Locale.Forge"                   "Locale.Forge.exe"          "Applications\Tools\Dev\Assets"

:: ── Tools / Dev / Data ───────────────────────────────────────────
call :pub "Boot.Map"              "Applications\Tools\Dev\Data\Boot.Map"                         "Boot.Map.exe"              "Applications\Tools\Dev\Data"
call :pub "Quick.Calc"            "Applications\Tools\Dev\Data\Quick.Calc"                       "Quick.Calc.exe"            "Applications\Tools\Dev\Data"
call :pub "Table.Craft"           "Applications\Tools\Dev\Data\Table.Craft"                      "Table.Craft.exe"           "Applications\Tools\Dev\Data"

:: ── Tools / Dev / Debug ──────────────────────────────────────────
call :pub "Hex.Peek"              "Applications\Tools\Dev\Debug\Hex.Peek"                        "Hex.Peek.exe"              "Applications\Tools\Dev\Debug"
call :pub "Log.Lens"              "Applications\Tools\Dev\Debug\Log.Lens"                        "Log.Lens.exe"              "Applications\Tools\Dev\Debug"
call :pub "Signal.Flow"           "Applications\Tools\Dev\Debug\Signal.Flow"                     "Signal.Flow.exe"           "Applications\Tools\Dev\Debug"

:: ── Tools / Dev / Network ────────────────────────────────────────
call :pub "Api.Probe"             "Applications\Tools\Dev\Network\Api.Probe"                     "Api.Probe.exe"             "Applications\Tools\Dev\Network\Api.Probe"
call :pub "Mock.Server"           "Applications\Tools\Dev\Network\Mock.Server"                   "Mock.Server.exe"           "Applications\Tools\Dev\Network"
call :pub "Serve.Cast"            "Applications\Tools\Dev\Network\Serve.Cast"                    "Serve.Cast.exe"            "Applications\Tools\Dev\Network"

:: ── Tools / Dev / System ─────────────────────────────────────────
call :pub "Layout.Forge"          "Applications\Tools\Dev\System\Layout.Forge"                   "Layout.Forge.exe"          "Applications\Tools\Dev\System\Layout.Forge"
call :pub "Sched.Cast"            "Applications\Tools\Dev\System\Sched.Cast"                     "Sched.Cast.exe"            "Applications\Tools\Dev\System\Sched.Cast"

:: ── Tools / Network ──────────────────────────────────────────────
call :pub "DNS.Flip"              "Applications\Tools\Network\DNS.Flip"                          "Dns.Flip.exe"              "Applications\Tools\Network"
call :pub "Net.Scan"              "Applications\Tools\Network\Net.Scan"                          "Net.Scan.exe"              "Applications\Tools\Network\Net.Scan"
call :pub "Port.Watch"            "Applications\Tools\Network\Port.Watch"                        "Port.Watch.exe"            "Applications\Tools\Network"

:: ── Tools / Productivity / Capture ───────────────────────────────
call :pub "Code.Snap"             "Applications\Tools\Productivity\Capture\Code.Snap"            "Code.Snap.exe"             "Applications\Tools\Productivity\Capture"
call :pub "Screen.Recorder"       "Applications\Tools\Productivity\Capture\Screen.Recorder"      "Screen.Recorder.exe"       "Applications\Tools\Productivity\Capture"

:: ── Tools / Productivity / Creative ─────────────────────────────
call :pub "Color.Grade"           "Applications\Tools\Productivity\Creative\Color.Grade"         "Color.Grade.exe"           "Applications\Tools\Productivity\Creative\Color.Grade"

:: ── Tools / Productivity / Media ────────────────────────────────
call :pub "Tag.Forge"             "Applications\Tools\Productivity\Media\Tag.Forge"              "Tag.Forge.exe"             "Applications\Tools\Productivity\Media\Tag.Forge"

:: ── Tools / Productivity / Text ──────────────────────────────────
call :pub "Echo.Text"             "Applications\Tools\Productivity\Text\Echo.Text"               "Echo.Text.exe"             "Applications\Tools\Productivity\Text\Echo.Text"
call :pub "Mark.View"             "Applications\Tools\Productivity\Text\Mark.View"               "Mark.View.exe"             "Applications\Tools\Productivity\Text\Mark.View"
call :pub "Text.Forge"            "Applications\Tools\Productivity\Text\Text.Forge"              "Text.Forge.exe"            "Applications\Tools\Productivity\Text"

:: ── Tools / Productivity / Utility ───────────────────────────────
call :pub "Clipboard.Stacker"     "Applications\Tools\Productivity\Utility\Clipboard.Stacker"    "Clipboard.Stacker.exe"     "Applications\Tools\Productivity\Utility"
call :pub "Dict.Cast"             "Applications\Tools\Productivity\Utility\Dict.Cast"            "Dict.Cast.exe"             "Applications\Tools\Productivity\Utility\Dict.Cast"
call :pub "Mouse.Flick"           "Applications\Tools\Productivity\Utility\Mouse.Flick"          "Mouse.Flick.exe"           "Applications\Tools\Productivity\Utility"
call :pub "Prompt.Forge"          "Applications\Tools\Productivity\Utility\Prompt.Forge"         "Prompt.Forge.exe"          "Applications\Tools\Productivity\Utility\Prompt.Forge"
call :pub "QR.Forge"              "Applications\Tools\Productivity\Utility\QR.Forge"             "QR.Forge.exe"              "Applications\Tools\Productivity\Utility"

:: ── Tools / Productivity / Visual ────────────────────────────────
call :pub "Char.Art"              "Applications\Tools\Productivity\Visual\Char.Art"              "Char.Art.exe"              "Applications\Tools\Productivity\Visual"
call :pub "Timeline.Craft"        "Applications\Tools\Productivity\Visual\Timeline.Craft"        "Timeline.Craft.exe"        "Applications\Tools\Productivity\Visual\Timeline.Craft"
call :pub "Word.Cloud"            "Applications\Tools\Productivity\Visual\Word.Cloud"            "Word.Cloud.exe"            "Applications\Tools\Productivity\Visual"

:: ── Tools / System ───────────────────────────────────────────────
call :pub "App.Temp"              "Applications\Tools\System\App.Temp"                           "App.Temp.exe"              "Applications\Tools\System"
call :pub "Burn.Rate"             "Applications\Tools\System\Burn.Rate"                          "Burn.Rate.exe"             "Applications\Tools\System"
call :pub "Env.Guard"             "Applications\Tools\System\Env.Guard"                          "Env.Guard.exe"             "Applications\Tools\System"
call :pub "Spec.Report"           "Applications\Tools\System\Spec.Report"                        "Spec.Report.exe"           "Applications\Tools\System"
call :pub "Sys.Clean"             "Applications\Tools\System\Sys.Clean"                          "Sys.Clean.exe"             "Applications\Tools\System\Sys.Clean"
call :pub "Tray.Stats"            "Applications\Tools\System\Tray.Stats"                         "Tray.Stats.exe"            "Applications\Tools\System\Tray.Stats"

:: ── Games / Action ───────────────────────────────────────────────
call :pub "Dungeon.Dash"          "Games\Action\Dungeon.Dash"                                    "Dungeon.Dash.exe"          "Games\Action"

:: ── Games / Casual ───────────────────────────────────────────────
call :pub "Hook.Cast"             "Games\Casual\Hook.Cast"                                       "Hook.Cast.exe"             "Games\Casual"
call :pub "Wave.Surf"             "Games\Casual\Wave.Surf"                                       "Wave.Surf.exe"             "Games\Casual"

:: ── Games / Arcade ───────────────────────────────────────────────
call :pub "Brick.Blitz"           "Games\Arcade\Brick.Blitz"                                     "Brick.Blitz.exe"           "Games\Arcade"
call :pub "Dash.City"             "Games\Arcade\Dash.City"                                       "Dash.City.exe"             "Games\Arcade"
call :pub "Neon.Run"              "Games\Arcade\Neon.Run"                                        "Neon.Run.exe"              "Games\Arcade"
call :pub "Neon.Slice"            "Games\Arcade\Neon.Slice"                                      "Neon.Slice.exe"            "Games\Arcade\Neon.Slice"

:: ── Games / Puzzle ───────────────────────────────────────────────
call :pub "Gravity.Flip"          "Games\Puzzle\Gravity.Flip"                                    "Gravity.Flip.exe"          "Games\Puzzle"
call :pub "Hue.Flow"              "Games\Puzzle\Hue.Flow"                                        "Hue.Flow.exe"              "Games\Puzzle"
call :pub "Orbit.Craft"           "Games\Puzzle\Orbit.Craft"                                     "Orbit.Craft.exe"           "Games\Puzzle\Orbit.Craft"

:: ── Games / Racing ───────────────────────────────────────────────
call :pub "Nitro.Drift"           "Games\Racing\Nitro.Drift"                                     "Nitro.Drift.exe"           "Games\Racing"

:: ── Games / Rhythm ───────────────────────────────────────────────
call :pub "Beat.Drop"             "Games\Rhythm\Beat.Drop"                                       "Beat.Drop.exe"             "Games\Rhythm"
call :pub "Chord.Strike"          "Games\Rhythm\Chord.Strike"                                    "ChordStrike.exe"           "Games\Rhythm"

:: ── Games / Simulation ────────────────────────────────────────────
call :pub "Leaf.Grow"             "Games\Simulation\Leaf.Grow"                                   "Leaf.Grow.exe"             "Games\Simulation"

:: ── Games / Sandbox ──────────────────────────────────────────────
call :pub "Sand.Fall"             "Games\Sandbox\Sand.Fall"                                      "Sand.Fall.exe"             "Games\Sandbox"

:: ── Games / Shooter ──────────────────────────────────────────────
call :pub "Dodge.Blitz"           "Games\Shooter\Dodge.Blitz"                                    "Dodge.Blitz.exe"           "Games\Shooter"
call :pub "Star.Strike"           "Games\Shooter\Star.Strike"                                    "Star.Strike.exe"           "Games\Shooter"

:: ── Games / Strategy ─────────────────────────────────────────────
call :pub "Tower.Guard"           "Games\Strategy\Tower.Guard"                                   "Tower.Guard.exe"           "Games\Strategy"

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
