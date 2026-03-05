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
call :pub "AI.Clip"               "Applications\AI\AI.Clip"                              "Ai.Clip.exe"               "Applications\AI"

:: ── Applications / Audio ─────────────────────────────────────────
call :pub "Music.Player"          "Applications\Audio\Music.Player"                      "Music.Player.exe"          "Applications\Audio"

:: ── Applications / Automation ────────────────────────────────────
call :pub "Stay.Awake"            "Applications\Automation\Stay.Awake"                   "Stay.Awake.exe"            "Applications\Automation\Stay.Awake"

:: ── Applications / Files ─────────────────────────────────────────
call :pub "Batch.Rename"          "Applications\Files\Batch.Rename"                      "Batch.Rename.exe"          "Applications\Files"
call :pub "Deep.Diff"             "Applications\Files\Deep.Diff"                         "DeepDiff.exe"              "Applications\Files\Deep.Diff"
call :pub "Disk.Lens"             "Applications\Files\Disk.Lens"                         "Disk.Lens.exe"             "Applications\Files\Disk.Lens"
call :pub "File.Duplicates"       "Applications\Files\File.Duplicates"                   "File.Duplicates.exe"       "Applications\Files"
call :pub "File.Unlocker"         "Applications\Files\File.Unlocker"                     "File.Unlocker.exe"         "Applications\Files"
call :pub "Folder.Purge"          "Applications\Files\Folder.Purge"                      "Folder.Purge.exe"          "Applications\Files"
call :pub "PDF.Forge"             "Applications\Files\PDF.Forge"                         "PdfForge.exe"              "Applications\Files"
call :pub "Zip.Peek"              "Applications\Files\Zip.Peek"                          "ZipPeek.exe"               "Applications\Files"
call :pub "Hash.Check"           "Applications\Files\Hash.Check"                        "HashCheck.exe"             "Applications\Files"

:: ── Applications / Health ────────────────────────────────────────
call :pub "Toast.Cast"            "Applications\Health\Toast.Cast"                       "Toast.Cast.exe"            "Applications\Health"

:: ── Applications / Media ─────────────────────────────────────────
call :pub "Photo.Video.Organizer" "Applications\Media\Photo.Video.Organizer"             "Photo.Video.Organizer.exe" "Applications\Media"

:: ── Applications / Tools / Dev ───────────────────────────────────
call :pub "Api.Probe"             "Applications\Tools\Dev\Api.Probe"                     "Api.Probe.exe"             "Applications\Tools\Dev\Api.Probe"
call :pub "Glyph.Map"             "Applications\Tools\Dev\Glyph.Map"                     "Glyph.Map.exe"             "Applications\Tools\Dev\Glyph.Map"
call :pub "Hex.Peek"              "Applications\Tools\Dev\Hex.Peek"                      "Hex.Peek.exe"              "Applications\Tools\Dev"
call :pub "Log.Lens"              "Applications\Tools\Dev\Log.Lens"                      "Log.Lens.exe"              "Applications\Tools\Dev"
call :pub "Mock.Server"           "Applications\Tools\Dev\Mock.Server"                   "Mock.Server.exe"           "Applications\Tools\Dev"
call :pub "Signal.Flow"           "Applications\Tools\Dev\Signal.Flow"                   "Signal.Flow.exe"           "Applications\Tools\Dev"
call :pub "Serve.Cast"            "Applications\Tools\Dev\Serve.Cast"                    "Serve.Cast.exe"            "Applications\Tools\Dev"
call :pub "Table.Craft"           "Applications\Tools\Dev\Table.Craft"                   "Table.Craft.exe"           "Applications\Tools\Dev"

:: ── Applications / Tools / Network ──────────────────────────────
call :pub "DNS.Flip"              "Applications\Tools\Network\DNS.Flip"                  "Dns.Flip.exe"              "Applications\Tools\Network"
call :pub "Port.Watch"            "Applications\Tools\Network\Port.Watch"                "Port.Watch.exe"            "Applications\Tools\Network"

:: ── Applications / Tools / Productivity ─────────────────────────
call :pub "Clipboard.Stacker"     "Applications\Tools\Productivity\Clipboard.Stacker"    "Clipboard.Stacker.exe"     "Applications\Tools\Productivity"
call :pub "Mouse.Flick"           "Applications\Tools\Productivity\Mouse.Flick"          "Mouse.Flick.exe"           "Applications\Tools\Productivity"
call :pub "Code.Snap"             "Applications\Tools\Productivity\Code.Snap"            "Code.Snap.exe"             "Applications\Tools\Productivity"
call :pub "QR.Forge"              "Applications\Tools\Productivity\QR.Forge"             "QR.Forge.exe"              "Applications\Tools\Productivity"
call :pub "Screen.Recorder"       "Applications\Tools\Productivity\Screen.Recorder"      "Screen.Recorder.exe"       "Applications\Tools\Productivity"
call :pub "Text.Forge"            "Applications\Tools\Productivity\Text.Forge"           "Text.Forge.exe"            "Applications\Tools\Productivity"
call :pub "Word.Cloud"            "Applications\Tools\Productivity\Word.Cloud"           "Word.Cloud.exe"            "Applications\Tools\Productivity"
call :pub "Char.Art"              "Applications\Tools\Productivity\Char.Art"             "Char.Art.exe"              "Applications\Tools\Productivity"
call :pub "Mark.View"             "Applications\Tools\Productivity\Mark.View"            "Mark.View.exe"             "Applications\Tools\Productivity\Mark.View"

:: ── Applications / Tools / System ───────────────────────────────
call :pub "Env.Guard"             "Applications\Tools\System\Env.Guard"                  "Env.Guard.exe"             "Applications\Tools\System"
call :pub "Sys.Clean"             "Applications\Tools\System\Sys.Clean"                  "Sys.Clean.exe"             "Applications\Tools\System\Sys.Clean"

:: ── Games / Action ───────────────────────────────────────────────
call :pub "Dungeon.Dash"          "Games\Action\Dungeon.Dash"                            "Dungeon.Dash.exe"          "Games\Action"

:: ── Games / Arcade ───────────────────────────────────────────────
call :pub "Brick.Blitz"           "Games\Arcade\Brick.Blitz"                             "Brick.Blitz.exe"           "Games\Arcade"
call :pub "Dash.City"             "Games\Arcade\Dash.City"                               "Dash.City.exe"             "Games\Arcade"
call :pub "Neon.Run"              "Games\Arcade\Neon.Run"                                "Neon.Run.exe"              "Games\Arcade"
call :pub "Neon.Slice"            "Games\Arcade\Neon.Slice"                              "Neon.Slice.exe"            "Games\Arcade\Neon.Slice"

:: ── Games / Puzzle ───────────────────────────────────────────────
call :pub "Gravity.Flip"          "Games\Puzzle\Gravity.Flip"                            "Gravity.Flip.exe"          "Games\Puzzle"
call :pub "Hue.Flow"              "Games\Puzzle\Hue.Flow"                                "Hue.Flow.exe"              "Games\Puzzle"
call :pub "Orbit.Craft"           "Games\Puzzle\Orbit.Craft"                             "Orbit.Craft.exe"           "Games\Puzzle\Orbit.Craft"

:: ── Games / Racing ───────────────────────────────────────────────
call :pub "Nitro.Drift"           "Games\Racing\Nitro.Drift"                             "Nitro.Drift.exe"           "Games\Racing"

:: ── Games / Rhythm ───────────────────────────────────────────────
call :pub "Beat.Drop"             "Games\Rhythm\Beat.Drop"                               "Beat.Drop.exe"             "Games\Rhythm"

:: ── Games / Sandbox ──────────────────────────────────────────────
call :pub "Sand.Fall"             "Games\Sandbox\Sand.Fall"                              "Sand.Fall.exe"             "Games\Sandbox"

:: ── Games / Shooter ──────────────────────────────────────────────
call :pub "Dodge.Blitz"           "Games\Shooter\Dodge.Blitz"                            "Dodge.Blitz.exe"           "Games\Shooter"
call :pub "Star.Strike"           "Games\Shooter\Star.Strike"                            "Star.Strike.exe"           "Games\Shooter"

:: ── Games / Strategy ─────────────────────────────────────────────
call :pub "Tower.Guard"           "Games\Strategy\Tower.Guard"                           "Tower.Guard.exe"           "Games\Strategy"

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
