@echo off
setlocal enabledelayedexpansion

set "ROOT=%~dp0"
set "BIN=%ROOT%bin"

:: ANSI 색상 변수 — 서브루틴 호출 전 미리 계산 (subroutine 내 [90m 오인식 방지)
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
echo !DG!출력 위치: %BIN%!RS!
echo !DG!━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━!RS!
echo.

call :pub "Stay.Awake"            "Slack\Stay.Awake"                    "StayAwake.exe"
call :pub "Photo.Video.Organizer" "Photo.Video\Photo.Video.Organizer"   "PhotoVideoOrganizer.exe"
call :pub "Music.Player"          "Music\Music.Player"                  "MusicPlayer.exe"
call :pub "AI.Clip"               "AI\AI.Clip"                          "AiClip.exe"
call :pub "File.Duplicates"       "Files\File.Duplicates"               "FileDuplicates.exe"
call :pub "Sound.Board"           "Sound\Sound.Board"                   "SoundBoard.exe"
call :pub "Quick.Launcher"        "Tools\Quick.Launcher"                "QuickLauncher.exe"
call :pub "Workspace.Switcher"    "Tools\Workspace.Switcher"            "WorkspaceSwitcher.exe"

echo !DG!━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━!RS!
echo.
if !FAIL! gtr 0 echo !BD!!RE!결과: !PASS!/!TOTAL! 성공  ^|  실패:!FAILED!!RS!
if !FAIL! equ 0 echo !BD!!GR!결과: !PASS!/!TOTAL! 모두 성공!RS!
echo.
pause
goto :eof

:: ── 서브루틴 ──────────────────────────────────────────────────────────────────
:pub
set /a TOTAL+=1
set "NM=%~1"
set "DIR=%ROOT%%~2"
set "EX=%~3"
echo !CY!  ▶ %NM%!RS!
dotnet publish "%DIR%" -c Release -o "%BIN%" > "%TEMP%\pub_%NM%.log" 2>&1
set "RC=!errorlevel!"
if !RC! equ 0 set /a PASS+=1
if !RC! equ 0 echo !GR!    ✔ %EX%!RS!
if !RC! neq 0 set /a FAIL+=1
if !RC! neq 0 set "FAILED=!FAILED! %NM%"
if !RC! neq 0 echo !RE!    ✖ 실패  ^(로그: %TEMP%\pub_%NM%.log^)!RS!
echo.
goto :eof
