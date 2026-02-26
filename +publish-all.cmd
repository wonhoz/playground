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
call :pub "AI.Clip"               "Applications\AI\AI.Clip"                              "AiClip.exe"           "Applications\AI"

:: ── Applications / Audio ─────────────────────────────────────────
call :pub "Music.Player"          "Applications\Audio\Music.Player"                      "MusicPlayer.exe"      "Applications\Audio"

:: ── Applications / Automation ────────────────────────────────────
call :pub "Stay.Awake"            "Applications\Automation\Stay.Awake"                   "StayAwake.exe"        "Applications\Automation\StayAwake"

:: ── Applications / Files ─────────────────────────────────────────
call :pub "Batch.Rename"          "Applications\Files\Batch.Rename"                      "BatchRename.exe"      "Applications\Files"
call :pub "File.Duplicates"       "Applications\Files\File.Duplicates"                   "FileDuplicates.exe"   "Applications\Files"

:: ── Applications / Health ────────────────────────────────────────
call :pub "Toast.Cast"            "Applications\Health\Toast.Cast"                       "ToastCast.exe"        "Applications\Health"

:: ── Applications / Media ─────────────────────────────────────────
call :pub "Photo.Video.Organizer" "Applications\Media\Photo.Video.Organizer"             "PhotoVideoOrganizer.exe" "Applications\Media"

:: ── Applications / Tools / Dev ───────────────────────────────────
call :pub "Api.Probe"             "Applications\Tools\Dev\Api.Probe"                     "Api.Probe.exe"        "Applications\Tools\Dev\Api.Probe"
call :pub "Hash.Forge"            "Applications\Tools\Dev\Hash.Forge"                    "HashForge.exe"        "Applications\Tools\Dev"
call :pub "Log.Lens"              "Applications\Tools\Dev\Log.Lens"                      "LogLens.exe"          "Applications\Tools\Dev"
call :pub "Mock.Desk"             "Applications\Tools\Dev\Mock.Desk"                     "Mock.Desk.exe"        "Applications\Tools\Dev\Mock.Desk"
call :pub "Log.Tail"              "Applications\Tools\Dev\Log.Tail"                      "Log.Tail.exe"         "Applications\Tools\Dev"
call :pub "Mock.Server"           "Applications\Tools\Dev\Mock.Server"                   "Mock.Server.exe"      "Applications\Tools\Dev"

:: ── Applications / Tools / Network ──────────────────────────────
call :pub "DNS.Flip"              "Applications\Tools\Network\DNS.Flip"                  "DnsFlip.exe"          "Applications\Tools\Network"
call :pub "Port.Watch"            "Applications\Tools\Network\Port.Watch"                "PortWatch.exe"        "Applications\Tools\Network"

:: ── Applications / Tools / Productivity ─────────────────────────
call :pub "Clipboard.Stacker"     "Applications\Tools\Productivity\Clipboard.Stacker"    "ClipboardStacker.exe" "Applications\Tools\Productivity"
call :pub "QR.Forge"              "Applications\Tools\Productivity\QR.Forge"              "QR.Forge.exe"         "Applications\Tools\Productivity"
call :pub "Screen.Recorder"       "Applications\Tools\Productivity\Screen.Recorder"      "ScreenRecorder.exe"   "Applications\Tools\Productivity"
call :pub "Text.Forge"            "Applications\Tools\Productivity\Text.Forge"            "TextForge.exe"        "Applications\Tools\Productivity"
call :pub "Code.Snap"             "Applications\Tools\Productivity\Code.Snap"             "Code.Snap.exe"        "Applications\Tools\Productivity"
call :pub "Word.Cloud"            "Applications\Tools\Productivity\Word.Cloud"            "Word.Cloud.exe"       "Applications\Tools\Productivity"
call :pub "Char.Art"              "Applications\Tools\Productivity\Char.Art"              "Char.Art.exe"         "Applications\Tools\Productivity"

:: ── Applications / Tools / System ───────────────────────────────
call :pub "Env.Guard"             "Applications\Tools\System\Env.Guard"                  "EnvGuard.exe"         "Applications\Tools\System"

:: ── Games / Action ───────────────────────────────────────────────
call :pub "Dungeon.Dash"          "Games\Action\Dungeon.Dash"                            "DungeonDash.exe"      "Games\Action"

:: ── Games / Arcade ───────────────────────────────────────────────
call :pub "Brick.Blitz"           "Games\Arcade\Brick.Blitz"                             "BrickBlitz.exe"       "Games\Arcade"
call :pub "Dash.City"             "Games\Arcade\Dash.City"                               "DashCity.exe"         "Games\Arcade"
call :pub "Neon.Run"              "Games\Arcade\Neon.Run"                                "NeonRun.exe"          "Games\Arcade"

:: ── Games / Puzzle ───────────────────────────────────────────────
call :pub "Gravity.Flip"          "Games\Puzzle\Gravity.Flip"                            "GravityFlip.exe"      "Games\Puzzle"
call :pub "Hue.Flow"              "Games\Puzzle\Hue.Flow"                                "HueFlow.exe"          "Games\Puzzle"

:: ── Games / Racing ───────────────────────────────────────────────
call :pub "Nitro.Drift"           "Games\Racing\Nitro.Drift"                             "NitroDrift.exe"       "Games\Racing"

:: ── Games / Rhythm ───────────────────────────────────────────────
call :pub "Beat.Drop"             "Games\Rhythm\Beat.Drop"                               "BeatDrop.exe"         "Games\Rhythm"

:: ── Games / Shooter ──────────────────────────────────────────────
call :pub "Dodge.Blitz"           "Games\Shooter\Dodge.Blitz"                            "DodgeBlitz.exe"       "Games\Shooter"
call :pub "Star.Strike"           "Games\Shooter\Star.Strike"                            "StarStrike.exe"       "Games\Shooter"

:: ── Games / Strategy ─────────────────────────────────────────────
call :pub "Tower.Guard"           "Games\Strategy\Tower.Guard"                           "TowerGuard.exe"       "Games\Strategy"

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
