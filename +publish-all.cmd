@echo off
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

:: Applications
call :pub "AI.Clip"               "Applications\AI\AI.Clip"                      "AiClip.exe"
call :pub "Music.Player"          "Applications\Audio\Music.Player"              "MusicPlayer.exe"
call :pub "Stay.Awake"            "Applications\Automation\Stay.Awake"           "StayAwake.exe"
call :pub "File.Duplicates"       "Applications\Files\File.Duplicates"           "FileDuplicates.exe"
call :pub "Batch.Rename"          "Applications\Files\Batch.Rename"              "BatchRename.exe"
call :pub "Photo.Video.Organizer" "Applications\Media\Photo.Video.Organizer"     "PhotoVideoOrganizer.exe"
call :pub "Clipboard.Stacker"     "Applications\Tools\Clipboard.Stacker"         "ClipboardStacker.exe"
call :pub "Screen.Recorder"       "Applications\Tools\Screen.Recorder"           "ScreenRecorder.exe"
call :pub "Text.Forge"            "Applications\Tools\Text.Forge"                "TextForge.exe"
call :pub "Log.Lens"             "Applications\Tools\Log.Lens"                 "LogLens.exe"
call :pub "Env.Guard"            "Applications\Tools\Env.Guard"                "EnvGuard.exe"
call :pub "DNS.Flip"             "Applications\Tools\DNS.Flip"                 "DnsFlip.exe"
call :pub "Toast.Cast"           "Applications\Health\Toast.Cast"               "ToastCast.exe"
:: Games - Action
call :pub "Fist.Fury"             "Games\Action\Fist.Fury"                       "FistFury.exe"
call :pub "Dungeon.Dash"          "Games\Action\Dungeon.Dash"                    "DungeonDash.exe"
:: Games - Arcade
call :pub "Brick.Blitz"           "Games\Arcade\Brick.Blitz"                     "BrickBlitz.exe"
call :pub "Neon.Run"              "Games\Arcade\Neon.Run"                        "NeonRun.exe"
call :pub "Dash.City"             "Games\Arcade\Dash.City"                       "DashCity.exe"
:: Games - Puzzle
call :pub "Hue.Flow"              "Games\Puzzle\Hue.Flow"                        "HueFlow.exe"
call :pub "Gravity.Flip"          "Games\Puzzle\Gravity.Flip"                    "GravityFlip.exe"
:: Games - Racing
call :pub "Nitro.Drift"           "Games\Racing\Nitro.Drift"                     "NitroDrift.exe"
:: Games - Rhythm
call :pub "Beat.Drop"             "Games\Rhythm\Beat.Drop"                       "BeatDrop.exe"
:: Games - Shooter
call :pub "Dodge.Blitz"           "Games\Shooter\Dodge.Blitz"                    "DodgeBlitz.exe"
call :pub "Star.Strike"           "Games\Shooter\Star.Strike"                    "StarStrike.exe"
:: Games - Sports
call :pub "Track.Star"            "Games\Sports\Track.Star"                      "TrackStar.exe"
:: Games - Strategy
call :pub "Tower.Guard"           "Games\Strategy\Tower.Guard"                   "TowerGuard.exe"

:: Remove .pdb files
echo !DG!Cleaning .pdb files...!RS!
del /q "%BIN%\*.pdb" 2>nul
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
echo !CY!  ^> %NM%!RS!
dotnet publish "%DIR%" -c Release -o "%BIN%" > "%TEMP%\pub_%NM%.log" 2>&1
set "RC=!errorlevel!"
if !RC! equ 0 set /a PASS+=1
if !RC! equ 0 echo !GR!    [OK]  %EX%!RS!
if !RC! neq 0 set /a FAIL+=1
if !RC! neq 0 set "FAILED=!FAILED! %NM%"
if !RC! neq 0 echo !RE!    [!!]  Failed - log: %TEMP%\pub_%NM%.log!RS!
echo.
goto :eof
