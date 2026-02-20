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

call :pub "AI.Clip"               "AI\AI.Clip"                          "AiClip.exe"
call :pub "Music.Player"          "Audio\Music.Player"                  "MusicPlayer.exe"
call :pub "Sound.Board"           "Audio\Sound.Board"                   "SoundBoard.exe"
call :pub "Stay.Awake"            "Automation\Stay.Awake"               "StayAwake.exe"
call :pub "File.Duplicates"       "Files\File.Duplicates"               "FileDuplicates.exe"
call :pub "Photo.Video.Organizer" "Media\Photo.Video.Organizer"         "PhotoVideoOrganizer.exe"
call :pub "Quick.Launcher"        "Tools\Quick.Launcher"                "QuickLauncher.exe"
call :pub "Workspace.Switcher"    "Tools\Workspace.Switcher"            "WorkspaceSwitcher.exe"

:: Remove .pdb files
echo !DG!Cleaning .pdb files...!RS!
del /q "%BIN%\*.pdb" 2>nul
echo !DG!--------------------------------------------------!RS!
echo.
if !FAIL! gtr 0 echo !BD!!RE!Result: !PASS!/!TOTAL! OK  ^|  Failed:!FAILED!!RS!
if !FAIL! equ 0 echo !BD!!GR!Result: !PASS!/!TOTAL! All succeeded!RS!
echo.
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
