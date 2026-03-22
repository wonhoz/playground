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

:: ── Checkbox Dialog ─────────────────────────────────────────────────
set "TEMP_SEL=%TEMP%\playground_pub_sel.txt"
if exist "%TEMP_SEL%" del "%TEMP_SEL%"
powershell -NoProfile -ExecutionPolicy Bypass -File "%ROOT%+publish-selector.ps1" "%TEMP_SEL%"
if not exist "%TEMP_SEL%" goto :eof
set /p SEL=<"%TEMP_SEL%"
del "%TEMP_SEL%"
if "!SEL!"=="" goto :eof

:: ── Publish selected ────────────────────────────────────────────────
set /a TOTAL=0
set /a PASS=0
set /a FAIL=0
set "FAILED="

echo.
echo !BD!!CY!Playground ^| Publish!RS!
echo !DG!Output: %BIN%!RS!
echo !DG!--------------------------------------------------!RS!
echo.

for %%n in (!SEL!) do (
    if "%%n"=="1"   call :pub "AI.Clip"               "Applications\AI\AI.Clip"                                 "Ai.Clip.exe"               "Applications\AI"
    if "%%n"=="2"   call :pub "ANSI.Forge"             "Applications\Text\ANSI.Forge"                           "ANSI.Forge.exe"            "Applications\Text\ANSI.Forge"
    if "%%n"=="3"   call :pub "Api.Probe"              "Applications\Network\Server\Api.Probe"                   "Api.Probe.exe"             "Applications\Network\Server\Api.Probe"
    if "%%n"=="4"   call :pub "App.Temp"               "Applications\Development\Inspector\App.Temp"             "App.Temp.exe"              "Applications\Development\Inspector"
    if "%%n"=="5"   call :pub "Badge.Forge"            "Applications\Tools.Utility\Badge.Forge"                  "Badge.Forge.exe"           "Applications\Tools.Utility\Badge.Forge"
    if "%%n"=="6"   call :pub "Batch.Rename"           "Applications\Files\Manager\Batch.Rename"                 "Batch.Rename.exe"          "Applications\Files\Manager"
    if "%%n"=="7"   call :pub "Beat.Drop"              "Games\Rhythm\Beat.Drop"                                  "Beat.Drop.exe"             "Games\Rhythm"
    if "%%n"=="8"   call :pub "Boot.Map"               "Applications\System\Monitor\Boot.Map"                    "Boot.Map.exe"              "Applications\System\Monitor"
    if "%%n"=="9"   call :pub "Brick.Blitz"            "Games\Arcade\Brick.Blitz"                                "Brick.Blitz.exe"           "Games\Arcade"
    if "%%n"=="10"  call :pub "Bug.Hunt"               "Games\Puzzle\Bug.Hunt"                                   "Bug.Hunt.exe"              "Games\Puzzle"
    if "%%n"=="11"  call :pub "Burn.Rate"              "Applications\System\Monitor\Burn.Rate"                   "Burn.Rate.exe"             "Applications\System\Monitor"
    if "%%n"=="12"  call :pub "Char.Art"               "Applications\Text\Char.Art"                              "Char.Art.exe"              "Applications\Text"
    if "%%n"=="13"  call :pub "Cipher.Quest"           "Games\Puzzle\Cipher.Quest"                               "Cipher.Quest.exe"          "Games\Puzzle"
    if "%%n"=="14"  call :pub "Circuit.Break"          "Games\Puzzle\Circuit.Break"                              "Circuit.Break.exe"         "Games\Puzzle"
    if "%%n"=="15"  call :pub "Clipboard.Stacker"      "Applications\Tools.Utility\Clipboard.Stacker"            "Clipboard.Stacker.exe"     "Applications\Tools.Utility"
    if "%%n"=="16"  call :pub "Code.Idle"              "Games\Idle\Code.Idle"                                    "Code.Idle.exe"             "Games\Idle\Code.Idle"
    if "%%n"=="17"  call :pub "Color.Grade"            "Applications\Photo.Picture\Color.Grade"                  "Color.Grade.exe"           "Applications\Photo.Picture\Color.Grade"
    if "%%n"=="18"  call :pub "Comic.Cast"             "Applications\Photo.Picture\Comic.Cast"                   "Comic.Cast.exe"            "Applications\Photo.Picture\Comic.Cast"
    if "%%n"=="19"  call :pub "Crossword.Cast"         "Games\Puzzle\Crossword.Cast"                             "Crossword.Cast.exe"        "Games\Puzzle"
    if "%%n"=="20"  call :pub "Ctx.Menu"               "Applications\System\Manager\Ctx.Menu"                    "Ctx.Menu.exe"              "Applications\System\Manager\Ctx.Menu"
    if "%%n"=="21"  call :pub "Dash.City"              "Games\Arcade\Dash.City"                                  "Dash.City.exe"             "Games\Arcade"
    if "%%n"=="22"  call :pub "Dep.Graph"              "Applications\Development\Analyzer\Dep.Graph"             "Dep.Graph.exe"             "Applications\Development\Analyzer\Dep.Graph"
    if "%%n"=="23"  call :pub "Dict.Cast"              "Applications\Tools.Utility\Dict.Cast"                    "Dict.Cast.exe"             "Applications\Tools.Utility\Dict.Cast"
    if "%%n"=="24"  call :pub "Disk.Lens"              "Applications\Files\Inspector\Disk.Lens"                  "Disk.Lens.exe"             "Applications\Files\Inspector\Disk.Lens"
    if "%%n"=="25"  call :pub "DNS.Flip"               "Applications\Network\Monitor\DNS.Flip"                   "Dns.Flip.exe"              "Applications\Network\Monitor"
    if "%%n"=="26"  call :pub "Dodge.Blitz"            "Games\Shooter\Dodge.Blitz"                               "Dodge.Blitz.exe"           "Games\Shooter"
    if "%%n"=="27"  call :pub "Dodge.Craft"            "Games\Shooter\Dodge.Craft"                               "DodgeCraft.exe"            "Games\Shooter"
    if "%%n"=="28"  call :pub "Drive.Bench"            "Applications\System\Monitor\Drive.Bench"                 "Drive.Bench.exe"           "Applications\System\Monitor"
    if "%%n"=="29"  call :pub "Dungeon.Dash"           "Games\Action\Dungeon.Dash"                               "Dungeon.Dash.exe"          "Games\Action"
    if "%%n"=="30"  call :pub "Ear.Train"              "Games\Casual\Ear.Train"                                  "Ear.Train.exe"             "Games\Casual"
    if "%%n"=="31"  call :pub "Echo.Text"              "Applications\Text\Echo.Text"                             "Echo.Text.exe"             "Applications\Text\Echo.Text"
    if "%%n"=="32"  call :pub "Env.Guard"              "Applications\System\Manager\Env.Guard"                   "Env.Guard.exe"             "Applications\System\Manager"
    if "%%n"=="33"  call :pub "Escape.Key"             "Games\Puzzle\Escape.Key"                                 "Escape.Key.exe"            "Games\Puzzle"
    if "%%n"=="34"  call :pub "Ext.Boss"               "Applications\System\Manager\Ext.Boss"                    "Ext.Boss.exe"              "Applications\System\Manager\Ext.Boss"
    if "%%n"=="35"  call :pub "File.Duplicates"        "Applications\Files\Manager\File.Duplicates"              "File.Duplicates.exe"       "Applications\Files\Manager"
    if "%%n"=="36"  call :pub "File.Unlocker"          "Applications\Files\Manager\File.Unlocker"                "File.Unlocker.exe"         "Applications\Files\Manager"
    if "%%n"=="37"  call :pub "Folder.Purge"           "Applications\Files\Manager\Folder.Purge"                 "Folder.Purge.exe"          "Applications\Files\Manager"
    if "%%n"=="38"  call :pub "Geo.Quiz"               "Games\Casual\Geo.Quiz"                                   "Geo.Quiz.exe"              "Games\Casual\Geo.Quiz"
    if "%%n"=="39"  call :pub "Git.Stats"              "Applications\Development\Analyzer\Git.Stats"             "Git.Stats.exe"             "Applications\Development\Analyzer\Git.Stats"
    if "%%n"=="40"  call :pub "Glyph.Map"              "Applications\Emoji.Icon\Glyph.Map"                       "Glyph.Map.exe"             "Applications\Emoji.Icon\Glyph.Map"
    if "%%n"=="41"  call :pub "Golf.Cast"              "Games\Sports\Golf.Cast"                                  "Golf.Cast.exe"             "Games\Sports\Golf.Cast"
    if "%%n"=="42"  call :pub "Gravity.Flip"           "Games\Puzzle\Gravity.Flip"                               "Gravity.Flip.exe"          "Games\Puzzle"
    if "%%n"=="43"  call :pub "Hash.Check"             "Applications\Files\Inspector\Hash.Check"                 "Hash.Check.exe"            "Applications\Files\Inspector\Hash.Check"
    if "%%n"=="44"  call :pub "Hex.Peek"               "Applications\Development\Inspector\Hex.Peek"             "Hex.Peek.exe"              "Applications\Development\Inspector"
    if "%%n"=="45"  call :pub "Hotkey.Map"             "Applications\System\Key\Hotkey.Map"                      "Hotkey.Map.exe"            "Applications\System\Key\Hotkey.Map"
    if "%%n"=="46"  call :pub "Hue.Flow"               "Games\Puzzle\Hue.Flow"                                   "Hue.Flow.exe"              "Games\Puzzle"
    if "%%n"=="47"  call :pub "Icon.Hunt"              "Applications\Emoji.Icon\Icon.Hunt"                       "Icon.Hunt.exe"             "Applications\Emoji.Icon"
    if "%%n"=="48"  call :pub "Icon.Maker"             "Applications\Tools.Utility\Icon.Maker"                   "Icon.Maker.exe"            "Applications\Tools.Utility\Icon.Maker"
    if "%%n"=="49"  call :pub "Img.Cast"             "Applications\Emoji.Icon\Img.Cast"                      "Img.Cast.exe"              "Applications\Emoji.Icon\Img.Cast"
    if "%%n"=="50"  call :pub "Img.Compare"            "Applications\Photo.Picture\Img.Compare"                  "Img.Compare.exe"           "Applications\Photo.Picture\Img.Compare"
    if "%%n"=="51"  call :pub "JSON.Fmt"               "Applications\Tools.Utility\JSON.Fmt"                     "JSON.Fmt.exe"              "Applications\Tools.Utility\JSON.Fmt"
    if "%%n"=="52"  call :pub "JSON.Tree"              "Applications\Development\Inspector\JSON.Tree"             "JSON.Tree.exe"             "Applications\Development\Inspector\JSON.Tree"
    if "%%n"=="53"  call :pub "Key.Map"                "Applications\System\Key\Key.Map"                         "Key.Map.exe"               "Applications\System\Key\Key.Map"
    if "%%n"=="54"  call :pub "Key.Test"               "Applications\System\Key\Key.Test"                        "KeyTest.exe"               "Applications\System\Key"
    if "%%n"=="55"  call :pub "Leaf.Grow"              "Games\Simulation\Leaf.Grow"                              "Leaf.Grow.exe"             "Games\Simulation"
    if "%%n"=="56"  call :pub "Locale.View"            "Applications\Development\Inspector\Locale.View"          "Locale.View.exe"           "Applications\Development\Inspector\Locale.View"
    if "%%n"=="57"  call :pub "Log.Lens"               "Applications\Development\Analyzer\Log.Lens"              "Log.Lens.exe"              "Applications\Development\Analyzer"
    if "%%n"=="58"  call :pub "Log.Merge"              "Applications\Development\Analyzer\Log.Merge"             "Log.Merge.exe"             "Applications\Development\Analyzer"
    if "%%n"=="59"  call :pub "Manga.View"             "Applications\Files\Inspector\Manga.View"                 "Manga.View.exe"            "Applications\Files\Inspector\Manga.View"
    if "%%n"=="60"  call :pub "Mark.View"              "Applications\Text\Mark.View"                             "Mark.View.exe"             "Applications\Text\Mark.View"
    if "%%n"=="61"  call :pub "Mem.Lens"               "Applications\System\Monitor\Mem.Lens"                    "MemLens.exe"               "Applications\System\Monitor"
    if "%%n"=="62"  call :pub "Mock.Server"            "Applications\Network\Server\Mock.Server"                 "Mock.Server.exe"           "Applications\Network\Server"
    if "%%n"=="63"  call :pub "Morse.Run"              "Games\Casual\Morse.Run"                                  "Morse.Run.exe"             "Games\Casual\Morse.Run"
    if "%%n"=="64"  call :pub "Mosaic.Forge"           "Applications\Photo.Picture\Mosaic.Forge"                 "Mosaic.Forge.exe"          "Applications\Photo.Picture\Mosaic.Forge"
    if "%%n"=="65"  call :pub "Mouse.Flick"            "Applications\Tools.Utility\Mouse.Flick"                  "Mouse.Flick.exe"           "Applications\Tools.Utility"
    if "%%n"=="66"  call :pub "Music.Player"           "Applications\Audio\Music.Player"                         "Music.Player.exe"          "Applications\Audio"
    if "%%n"=="67"  call :pub "Neon.Run"               "Games\Arcade\Neon.Run"                                   "Neon.Run.exe"              "Games\Arcade"
    if "%%n"=="68"  call :pub "Neon.Slice"             "Games\Arcade\Neon.Slice"                                 "Neon.Slice.exe"            "Games\Arcade\Neon.Slice"
    if "%%n"=="69"  call :pub "Net.Scan"               "Applications\Network\Monitor\Net.Scan"                   "Net.Scan.exe"              "Applications\Network\Monitor\Net.Scan"
    if "%%n"=="70"  call :pub "Nitro.Drift"            "Games\Racing\Nitro.Drift"                                "Nitro.Drift.exe"           "Games\Racing"
    if "%%n"=="71"  call :pub "Orbit.Craft"            "Games\Puzzle\Orbit.Craft"                                "Orbit.Craft.exe"           "Games\Puzzle\Orbit.Craft"
    if "%%n"=="72"  call :pub "Orbit.Raid"             "Games\Puzzle\Orbit.Raid"                                 "Orbit.Raid.exe"            "Games\Puzzle"
    if "%%n"=="73"  call :pub "Pad.Forge"              "Applications\System\Manager\Pad.Forge"                   "Pad.Forge.exe"             "Applications\System\Manager\Pad.Forge"
    if "%%n"=="74"  call :pub "Pane.Cast"              "Applications\Automation\Pane.Cast"                       "Pane.Cast.exe"             "Applications\Automation"
    if "%%n"=="75"  call :pub "Path.Guard"             "Applications\System\Manager\Path.Guard"                  "PathGuard.exe"             "Applications\System\Manager\Path.Guard"
    if "%%n"=="76"  call :pub "PDF.Forge"              "Applications\Files\Inspector\PDF.Forge"                  "Pdf.Forge.exe"             "Applications\Files\Inspector\Pdf.Forge"
    if "%%n"=="77"  call :pub "Persp.Shift"            "Games\Puzzle\Persp.Shift"                                "Persp.Shift.exe"           "Games\Puzzle"
    if "%%n"=="78"  call :pub "Photo.Video.Organizer"  "Applications\Photo.Picture\Photo.Video.Organizer"        "Photo.Video.Organizer.exe" "Applications\Photo.Picture"
    if "%%n"=="79"  call :pub "Port.Watch"             "Applications\Network\Monitor\Port.Watch"                 "Port.Watch.exe"            "Applications\Network\Monitor"
    if "%%n"=="80"  call :pub "Proc.Bench"             "Applications\System\Monitor\Proc.Bench"                  "Proc.Bench.exe"            "Applications\System\Monitor\Proc.Bench"
    if "%%n"=="81"  call :pub "Prompt.Forge"           "Applications\AI\Prompt.Forge"                            "Prompt.Forge.exe"          "Applications\AI\Prompt.Forge"
    if "%%n"=="82"  call :pub "QR.Forge"               "Applications\Tools.Utility\QR.Forge"                     "QR.Forge.exe"              "Applications\Tools.Utility"
    if "%%n"=="83"  call :pub "Quick.Calc"             "Applications\Development\Inspector\Quick.Calc"           "Quick.Calc.exe"            "Applications\Development\Inspector"
    if "%%n"=="84"  call :pub "Reg.Vault"              "Applications\System\Manager\Reg.Vault"                   "Reg.Vault.exe"             "Applications\System\Manager\Reg.Vault"
    if "%%n"=="85"  call :pub "Sand.Fall"              "Games\Sandbox\Sand.Fall"                                 "Sand.Fall.exe"             "Games\Sandbox"
    if "%%n"=="86"  call :pub "Sched.Cast"             "Applications\System\Manager\Sched.Cast"                  "Sched.Cast.exe"            "Applications\System\Manager\Sched.Cast"
    if "%%n"=="87"  call :pub "Screen.Recorder"        "Applications\Video\Screen.Recorder"                      "Screen.Recorder.exe"       "Applications\Video"
    if "%%n"=="88"  call :pub "Serve.Cast"             "Applications\Network\Server\Serve.Cast"                  "Serve.Cast.exe"            "Applications\Network\Server"
    if "%%n"=="89"  call :pub "Shortcut.Forge"         "Applications\Files\Manager\Shortcut.Forge"               "Shortcut.Forge.exe"        "Applications\Files\Manager\Shortcut.Forge"
    if "%%n"=="90"  call :pub "Signal.Flow"            "Applications\Development\Inspector\Signal.Flow"          "Signal.Flow.exe"           "Applications\Development\Inspector"
    if "%%n"=="91"  call :pub "Skill.Cast"             "Applications\Development\Inspector\Skill.Cast"           "Skill.Cast.exe"            "Applications\Development\Inspector"
    if "%%n"=="92"  call :pub "Sky.Drift"              "Games\Arcade\Sky.Drift"                                  "SkyDrift.exe"              "Games\Arcade"
    if "%%n"=="93"  call :pub "Snap.Duel"              "Games\Casual\Snap.Duel"                                  "Snap.Duel.exe"             "Games\Casual"
    if "%%n"=="94"  call :pub "Spec.Report"            "Applications\System\Monitor\Spec.Report"                 "Spec.Report.exe"           "Applications\System\Monitor"
    if "%%n"=="95"  call :pub "Spec.View"              "Applications\System\Monitor\Spec.View"                   "Spec.View.exe"             "Applications\System\Monitor"
    if "%%n"=="96"  call :pub "Star.Strike"            "Games\Shooter\Star.Strike"                               "Star.Strike.exe"           "Games\Shooter"
    if "%%n"=="97"  call :pub "Stay.Awake"             "Applications\Automation\Stay.Awake"                      "Stay.Awake.exe"            "Applications\Automation\Stay.Awake"
    if "%%n"=="98"  call :pub "Svc.Guard"              "Applications\System\Manager\Svc.Guard"                   "Svc.Guard.exe"             "Applications\System\Manager\Svc.Guard"
    if "%%n"=="99"  call :pub "SVG.Forge"              "Applications\Photo.Picture\SVG.Forge"                    "SVG.Forge.exe"             "Applications\Photo.Picture\SVG.Forge"
    if "%%n"=="100" call :pub "Sys.Clean"              "Applications\System\Manager\Sys.Clean"                   "Sys.Clean.exe"             "Applications\System\Manager\Sys.Clean"
    if "%%n"=="101" call :pub "Tag.Forge"              "Applications\Audio\Tag.Forge"                            "Tag.Forge.exe"             "Applications\Audio\Tag.Forge"
    if "%%n"=="102" call :pub "Text.Forge"             "Applications\Text\Text.Forge"                            "Text.Forge.exe"            "Applications\Text"
    if "%%n"=="103" call :pub "Tower.Guard"            "Games\Strategy\Tower.Guard"                              "Tower.Guard.exe"           "Games\Strategy"
    if "%%n"=="104" call :pub "Tray.Stats"             "Applications\System\Monitor\Tray.Stats"                  "Tray.Stats.exe"            "Applications\System\Monitor\Tray.Stats"
    if "%%n"=="105" call :pub "Web.Shot"               "Applications\Photo.Picture\Web.Shot"                     "Web.Shot.exe"              "Applications\Photo.Picture\Web.Shot"
    if "%%n"=="106" call :pub "Win.Event"              "Applications\Development\Analyzer\Win.Event"             "Win.Event.exe"             "Applications\Development\Analyzer"
    if "%%n"=="107" call :pub "Win.Scope"              "Applications\System\Manager\Win.Scope"                   "Win.Scope.exe"             "Applications\System\Manager\Win.Scope"
    if "%%n"=="108" call :pub "Word.Cloud"             "Applications\Text\Word.Cloud"                            "Word.Cloud.exe"            "Applications\Text"
    if "%%n"=="109" call :pub "Zip.Peek"               "Applications\Files\Inspector\Zip.Peek"                   "Zip.Peek.exe"              "Applications\Files\Inspector\Zip.Peek"
)
goto :DONE
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
