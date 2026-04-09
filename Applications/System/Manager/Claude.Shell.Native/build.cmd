@echo off

net session >nul 2>&1
if %errorlevel% neq 0 (
    powershell -Command "Start-Process '%~f0' -Verb RunAs"
    exit /b
)

set PROJ=%~dp0

echo [build] syncing AppxManifest.xml from package/ to bin/Release/...
copy /y "%PROJ%package\AppxManifest.xml" "%PROJ%bin\Release\AppxManifest.xml" >nul
if %errorlevel% neq 0 (
    echo [build] error: failed to copy AppxManifest.xml
    pause
    exit /b 1
)
echo [build] AppxManifest.xml synced.

echo [build] makeappx pack start...
powershell -NoProfile -ExecutionPolicy Bypass -Command "& 'C:\Program Files (x86)\Windows Kits\10\bin\10.0.26100.0\x64\makeappx.exe' pack /d '%PROJ%bin\Release' /p '%PROJ%bin\ClaudeContextMenu.msix' /nv /o"

if %errorlevel% equ 0 (
    echo [build] done: ClaudeContextMenu.msix
) else (
    echo [build] error: %errorlevel%
)

pause
