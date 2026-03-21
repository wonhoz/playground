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

:: 배포 전 버전 확인
for /f "delims=" %%v in ('powershell -NoProfile -Command "(Select-String -Path \"%ROOT%Directory.Build.props\" -Pattern \"<AppVersion>(.+)</AppVersion>\").Matches[0].Groups[1].Value" 2^>nul') do set "APP_VER=%%v"
if not defined APP_VER set "APP_VER=(알 수 없음)"
echo !BD!현재 버전: !GR!!APP_VER!!RS!
echo !DG!Directory.Build.props 기준 — 배포 전 버전을 확인하세요.!RS!
echo.
set /p "CONFIRM=계속 배포하시겠습니까? [Y/N] "
if /i not "!CONFIRM!"=="Y" (
    echo !RE!배포가 취소되었습니다.!RS!
    exit /b 0
)
echo.

:: ── Applications / AI ──────────────────────────────────────────────
call :pub "AI.Clip"               "Applications\AI\AI.Clip"                                 "Ai.Clip.exe"               "Applications\AI"
call :pub "Prompt.Forge"          "Applications\AI\Prompt.Forge"                            "Prompt.Forge.exe"          "Applications\AI\Prompt.Forge"

:: ── Applications / Audio ───────────────────────────────────────────
call :pub "Music.Player"          "Applications\Audio\Music.Player"                         "Music.Player.exe"          "Applications\Audio"
call :pub "Tag.Forge"             "Applications\Audio\Tag.Forge"                            "Tag.Forge.exe"             "Applications\Audio\Tag.Forge"

:: ── Applications / Automation ──────────────────────────────────────
call :pub "Pane.Cast"             "Applications\Automation\Pane.Cast"                       "Pane.Cast.exe"             "Applications\Automation"
call :pub "Stay.Awake"            "Applications\Automation\Stay.Awake"                      "Stay.Awake.exe"            "Applications\Automation\Stay.Awake"

:: ── Applications / Data ────────────────────────────────────────────
call :pub "Data.Map"              "Applications\Data\Data.Map"                              "Data.Map.exe"              "Applications\Data"

:: ── Applications / Development / Analyzer ─────────────────────────
call :pub "Dep.Graph"             "Applications\Development\Analyzer\Dep.Graph"             "Dep.Graph.exe"             "Applications\Development\Analyzer"
call :pub "Git.Stats"             "Applications\Development\Analyzer\Git.Stats"             "Git.Stats.exe"             "Applications\Development\Analyzer"
call :pub "Log.Lens"              "Applications\Development\Analyzer\Log.Lens"              "Log.Lens.exe"              "Applications\Development\Analyzer"
call :pub "Log.Merge"             "Applications\Development\Analyzer\Log.Merge"             "Log.Merge.exe"             "Applications\Development\Analyzer"
call :pub "Win.Event"             "Applications\Development\Analyzer\Win.Event"             "WinEvent.exe"              "Applications\Development\Analyzer"

:: ── Applications / Development / Inspector ─────────────────────────
call :pub "App.Temp"              "Applications\Development\Inspector\App.Temp"             "App.Temp.exe"              "Applications\Development\Inspector"
call :pub "Hex.Peek"              "Applications\Development\Inspector\Hex.Peek"             "Hex.Peek.exe"              "Applications\Development\Inspector"
call :pub "JSON.Tree"             "Applications\Development\Inspector\JSON.Tree"             "JSON.Tree.exe"             "Applications\Development\Inspector"
call :pub "Locale.View"           "Applications\Development\Inspector\Locale.View"          "Locale.View.exe"           "Applications\Development\Inspector"
call :pub "Quick.Calc"            "Applications\Development\Inspector\Quick.Calc"           "Quick.Calc.exe"            "Applications\Development\Inspector"
call :pub "Signal.Flow"           "Applications\Development\Inspector\Signal.Flow"          "Signal.Flow.exe"           "Applications\Development\Inspector"
call :pub "Skill.Cast"            "Applications\Development\Inspector\Skill.Cast"           "Skill.Cast.exe"            "Applications\Development\Inspector"

:: ── Applications / Emoji.Icon ──────────────────────────────────────
call :pub "Glyph.Map"             "Applications\Emoji.Icon\Glyph.Map"                       "Glyph.Map.exe"             "Applications\Emoji.Icon\Glyph.Map"
call :pub "Icon.Hunt"             "Applications\Emoji.Icon\Icon.Hunt"                       "Icon.Hunt.exe"             "Applications\Emoji.Icon"

:: ── Applications / Files / Inspector ──────────────────────────────
call :pub "Disk.Lens"             "Applications\Files\Inspector\Disk.Lens"                  "Disk.Lens.exe"             "Applications\Files\Inspector\Disk.Lens"
call :pub "Hash.Check"            "Applications\Files\Inspector\Hash.Check"                 "Hash.Check.exe"            "Applications\Files\Inspector\Hash.Check"
call :pub "Manga.View"            "Applications\Files\Inspector\Manga.View"                 "Manga.View.exe"            "Applications\Files\Inspector"
call :pub "PDF.Forge"             "Applications\Files\Inspector\PDF.Forge"                  "Pdf.Forge.exe"             "Applications\Files\Inspector\Pdf.Forge"
call :pub "Zip.Peek"              "Applications\Files\Inspector\Zip.Peek"                   "Zip.Peek.exe"              "Applications\Files\Inspector\Zip.Peek"

:: ── Applications / Files / Manager ────────────────────────────────
call :pub "Batch.Rename"          "Applications\Files\Manager\Batch.Rename"                 "Batch.Rename.exe"          "Applications\Files\Manager"
call :pub "File.Duplicates"       "Applications\Files\Manager\File.Duplicates"              "File.Duplicates.exe"       "Applications\Files\Manager"
call :pub "File.Unlocker"         "Applications\Files\Manager\File.Unlocker"                "File.Unlocker.exe"         "Applications\Files\Manager"
call :pub "Folder.Purge"          "Applications\Files\Manager\Folder.Purge"                 "Folder.Purge.exe"          "Applications\Files\Manager"
call :pub "Shortcut.Forge"        "Applications\Files\Manager\Shortcut.Forge"               "ShortcutForge.exe"         "Applications\Files\Manager"

:: ── Applications / Network / Monitor ───────────────────────────────
call :pub "DNS.Flip"              "Applications\Network\Monitor\DNS.Flip"                   "Dns.Flip.exe"              "Applications\Network\Monitor"
call :pub "Net.Scan"              "Applications\Network\Monitor\Net.Scan"                   "Net.Scan.exe"              "Applications\Network\Monitor\Net.Scan"
call :pub "Port.Watch"            "Applications\Network\Monitor\Port.Watch"                 "Port.Watch.exe"            "Applications\Network\Monitor"

:: ── Applications / Network / Server ────────────────────────────────
call :pub "Api.Probe"             "Applications\Network\Server\Api.Probe"                   "Api.Probe.exe"             "Applications\Network\Server\Api.Probe"
call :pub "Mock.Server"           "Applications\Network\Server\Mock.Server"                 "Mock.Server.exe"           "Applications\Network\Server"
call :pub "Serve.Cast"            "Applications\Network\Server\Serve.Cast"                  "Serve.Cast.exe"            "Applications\Network\Server"

:: ── Applications / Photo.Picture ───────────────────────────────────
call :pub "Color.Grade"           "Applications\Photo.Picture\Color.Grade"                  "Color.Grade.exe"           "Applications\Photo.Picture\Color.Grade"
call :pub "Comic.Cast"            "Applications\Photo.Picture\Comic.Cast"                   "Comic.Cast.exe"            "Applications\Photo.Picture\Comic.Cast"
call :pub "Img.Cast"              "Applications\Photo.Picture\Img.Cast"                     "ImgCast.exe"               "Applications\Photo.Picture\Img.Cast"
call :pub "Img.Compare"           "Applications\Photo.Picture\Img.Compare"                  "Img.Compare.exe"           "Applications\Photo.Picture"
call :pub "Mosaic.Forge"          "Applications\Photo.Picture\Mosaic.Forge"                 "Mosaic.Forge.exe"          "Applications\Photo.Picture\Mosaic.Forge"
call :pub "Photo.Video.Organizer" "Applications\Photo.Picture\Photo.Video.Organizer"        "Photo.Video.Organizer.exe" "Applications\Photo.Picture"
call :pub "SVG.Forge"             "Applications\Photo.Picture\SVG.Forge"                    "SVG.Forge.exe"             "Applications\Photo.Picture\SVG.Forge"
call :pub "Web.Shot"              "Applications\Photo.Picture\Web.Shot"                     "Web.Shot.exe"              "Applications\Photo.Picture\Web.Shot"

:: ── Applications / System / Key ────────────────────────────────────
call :pub "Hotkey.Map"            "Applications\System\Key\Hotkey.Map"                      "Hotkey.Map.exe"            "Applications\System\Key"
call :pub "Key.Map"               "Applications\System\Key\Key.Map"                         "Key.Map.exe"               "Applications\System\Key\Key.Map"
call :pub "Key.Test"              "Applications\System\Key\Key.Test"                        "KeyTest.exe"               "Applications\System\Key"

:: ── Applications / System / Manager ───────────────────────────────
call :pub "Ctx.Menu"              "Applications\System\Manager\Ctx.Menu"                    "Ctx.Menu.exe"              "Applications\System\Manager\Ctx.Menu"
call :pub "Env.Guard"             "Applications\System\Manager\Env.Guard"                   "Env.Guard.exe"             "Applications\System\Manager"
call :pub "Ext.Boss"              "Applications\System\Manager\Ext.Boss"                    "Ext.Boss.exe"              "Applications\System\Manager\Ext.Boss"
call :pub "Pad.Forge"             "Applications\System\Manager\Pad.Forge"                   "Pad.Forge.exe"             "Applications\System\Manager\Pad.Forge"
call :pub "Path.Guard"            "Applications\System\Manager\Path.Guard"                  "PathGuard.exe"             "Applications\System\Manager"
call :pub "Reg.Vault"             "Applications\System\Manager\Reg.Vault"                   "RegVault.exe"              "Applications\System\Manager"
call :pub "Sched.Cast"            "Applications\System\Manager\Sched.Cast"                  "Sched.Cast.exe"            "Applications\System\Manager\Sched.Cast"
call :pub "Svc.Guard"             "Applications\System\Manager\Svc.Guard"                   "Svc.Guard.exe"             "Applications\System\Manager\Svc.Guard"
call :pub "Sys.Clean"             "Applications\System\Manager\Sys.Clean"                   "Sys.Clean.exe"             "Applications\System\Manager\Sys.Clean"
call :pub "Win.Scope"             "Applications\System\Manager\Win.Scope"                   "Win.Scope.exe"             "Applications\System\Manager"

:: ── Applications / System / Monitor ───────────────────────────────
call :pub "Boot.Map"              "Applications\System\Monitor\Boot.Map"                    "Boot.Map.exe"              "Applications\System\Monitor"
call :pub "Burn.Rate"             "Applications\System\Monitor\Burn.Rate"                   "Burn.Rate.exe"             "Applications\System\Monitor"
call :pub "Drive.Bench"           "Applications\System\Monitor\Drive.Bench"                 "Drive.Bench.exe"           "Applications\System\Monitor"
call :pub "Mem.Lens"              "Applications\System\Monitor\Mem.Lens"                    "MemLens.exe"               "Applications\System\Monitor"
call :pub "Proc.Bench"            "Applications\System\Monitor\Proc.Bench"                  "Proc.Bench.exe"            "Applications\System\Monitor"
call :pub "Spec.Report"           "Applications\System\Monitor\Spec.Report"                 "Spec.Report.exe"           "Applications\System\Monitor"
call :pub "Spec.View"             "Applications\System\Monitor\Spec.View"                   "Spec.View.exe"             "Applications\System\Monitor\Spec.View"
call :pub "Tray.Stats"            "Applications\System\Monitor\Tray.Stats"                  "Tray.Stats.exe"            "Applications\System\Monitor\Tray.Stats"

:: ── Applications / Text ────────────────────────────────────────────
call :pub "ANSI.Forge"            "Applications\Text\ANSI.Forge"                            "ANSI.Forge.exe"            "Applications\Text"
call :pub "Char.Art"              "Applications\Text\Char.Art"                              "Char.Art.exe"              "Applications\Text"
call :pub "Echo.Text"             "Applications\Text\Echo.Text"                             "Echo.Text.exe"             "Applications\Text\Echo.Text"
call :pub "Mark.View"             "Applications\Text\Mark.View"                             "Mark.View.exe"             "Applications\Text\Mark.View"
call :pub "Text.Forge"            "Applications\Text\Text.Forge"                            "Text.Forge.exe"            "Applications\Text"
call :pub "Word.Cloud"            "Applications\Text\Word.Cloud"                            "Word.Cloud.exe"            "Applications\Text"

:: ── Applications / Tools.Utility ──────────────────────────────────
call :pub "Badge.Forge"           "Applications\Tools.Utility\Badge.Forge"                  "Badge.Forge.exe"           "Applications\Tools.Utility"
call :pub "Clipboard.Stacker"     "Applications\Tools.Utility\Clipboard.Stacker"            "Clipboard.Stacker.exe"     "Applications\Tools.Utility"
call :pub "Dict.Cast"             "Applications\Tools.Utility\Dict.Cast"                    "Dict.Cast.exe"             "Applications\Tools.Utility\Dict.Cast"
call :pub "Icon.Maker"            "Applications\Tools.Utility\Icon.Maker"                   "Icon.Maker.exe"            "Applications\Tools.Utility"
call :pub "JSON.Fmt"              "Applications\Tools.Utility\JSON.Fmt"                     "JSON.Fmt.exe"              "Applications\Tools.Utility"
call :pub "Mouse.Flick"           "Applications\Tools.Utility\Mouse.Flick"                  "Mouse.Flick.exe"           "Applications\Tools.Utility"
call :pub "QR.Forge"              "Applications\Tools.Utility\QR.Forge"                     "QR.Forge.exe"              "Applications\Tools.Utility"

:: ── Applications / Video ───────────────────────────────────────────
call :pub "Screen.Recorder"       "Applications\Video\Screen.Recorder"                      "Screen.Recorder.exe"       "Applications\Video"

:: ── Games / Action ─────────────────────────────────────────────────
call :pub "Dungeon.Dash"          "Games\Action\Dungeon.Dash"                               "Dungeon.Dash.exe"          "Games\Action"

:: ── Games / Arcade ─────────────────────────────────────────────────
call :pub "Brick.Blitz"           "Games\Arcade\Brick.Blitz"                                "Brick.Blitz.exe"           "Games\Arcade"
call :pub "Dash.City"             "Games\Arcade\Dash.City"                                  "Dash.City.exe"             "Games\Arcade"
call :pub "Neon.Run"              "Games\Arcade\Neon.Run"                                   "Neon.Run.exe"              "Games\Arcade"
call :pub "Neon.Slice"            "Games\Arcade\Neon.Slice"                                 "Neon.Slice.exe"            "Games\Arcade\Neon.Slice"
call :pub "Sky.Drift"             "Games\Arcade\Sky.Drift"                                  "SkyDrift.exe"              "Games\Arcade"

:: ── Games / Casual ─────────────────────────────────────────────────
call :pub "Ear.Train"             "Games\Casual\Ear.Train"                                  "Ear.Train.exe"             "Games\Casual"
call :pub "Geo.Quiz"              "Games\Casual\Geo.Quiz"                                   "Geo.Quiz.exe"              "Games\Casual"
call :pub "Morse.Run"             "Games\Casual\Morse.Run"                                  "Morse.Run.exe"             "Games\Casual"
call :pub "Snap.Duel"             "Games\Casual\Snap.Duel"                                  "Snap.Duel.exe"             "Games\Casual"

:: ── Games / Idle ───────────────────────────────────────────────────
call :pub "Code.Idle"             "Games\Idle\Code.Idle"                                    "Code.Idle.exe"             "Games\Idle"

:: ── Games / Puzzle ─────────────────────────────────────────────────
call :pub "Bug.Hunt"              "Games\Puzzle\Bug.Hunt"                                   "Bug.Hunt.exe"              "Games\Puzzle"
call :pub "Cipher.Quest"          "Games\Puzzle\Cipher.Quest"                               "Cipher.Quest.exe"          "Games\Puzzle"
call :pub "Circuit.Break"         "Games\Puzzle\Circuit.Break"                              "Circuit.Break.exe"         "Games\Puzzle"
call :pub "Crossword.Cast"        "Games\Puzzle\Crossword.Cast"                             "Crossword.Cast.exe"        "Games\Puzzle"
call :pub "Escape.Key"            "Games\Puzzle\Escape.Key"                                 "Escape.Key.exe"            "Games\Puzzle"
call :pub "Gravity.Flip"          "Games\Puzzle\Gravity.Flip"                               "Gravity.Flip.exe"          "Games\Puzzle"
call :pub "Hue.Flow"              "Games\Puzzle\Hue.Flow"                                   "Hue.Flow.exe"              "Games\Puzzle"
call :pub "Orbit.Craft"           "Games\Puzzle\Orbit.Craft"                                "Orbit.Craft.exe"           "Games\Puzzle\Orbit.Craft"
call :pub "Orbit.Raid"            "Games\Puzzle\Orbit.Raid"                                 "Orbit.Raid.exe"            "Games\Puzzle"
call :pub "Persp.Shift"           "Games\Puzzle\Persp.Shift"                                "Persp.Shift.exe"           "Games\Puzzle"

:: ── Games / Racing ─────────────────────────────────────────────────
call :pub "Nitro.Drift"           "Games\Racing\Nitro.Drift"                                "Nitro.Drift.exe"           "Games\Racing"

:: ── Games / Rhythm ─────────────────────────────────────────────────
call :pub "Beat.Drop"             "Games\Rhythm\Beat.Drop"                                  "Beat.Drop.exe"             "Games\Rhythm"

:: ── Games / Sandbox ────────────────────────────────────────────────
call :pub "Sand.Fall"             "Games\Sandbox\Sand.Fall"                                 "Sand.Fall.exe"             "Games\Sandbox"

:: ── Games / Shooter ────────────────────────────────────────────────
call :pub "Dodge.Blitz"           "Games\Shooter\Dodge.Blitz"                               "Dodge.Blitz.exe"           "Games\Shooter"
call :pub "Dodge.Craft"           "Games\Shooter\Dodge.Craft"                               "DodgeCraft.exe"            "Games\Shooter"
call :pub "Star.Strike"           "Games\Shooter\Star.Strike"                               "Star.Strike.exe"           "Games\Shooter"

:: ── Games / Simulation ─────────────────────────────────────────────
call :pub "Leaf.Grow"             "Games\Simulation\Leaf.Grow"                              "Leaf.Grow.exe"             "Games\Simulation"

:: ── Games / Sports ─────────────────────────────────────────────────
call :pub "Golf.Cast"             "Games\Sports\Golf.Cast"                                  "Golf.Cast.exe"             "Games\Sports"

:: ── Games / Strategy ───────────────────────────────────────────────
call :pub "Tower.Guard"           "Games\Strategy\Tower.Guard"                              "Tower.Guard.exe"           "Games\Strategy"

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
