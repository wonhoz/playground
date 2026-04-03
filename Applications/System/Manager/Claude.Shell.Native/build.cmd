@echo off

net session >nul 2>&1
if %errorlevel% neq 0 (
    powershell -Command "Start-Process '%~f0' -Verb RunAs"
    exit /b
)

echo [build] makeappx pack start...
powershell -NoProfile -ExecutionPolicy Bypass -Command "& 'C:\Program Files (x86)\Windows Kits\10\bin\10.0.26100.0\x64\makeappx.exe' pack /d 'C:\Users\admin\source\repos\+Playground\Applications\System\Manager\Claude.Shell.Native\bin\Release' /p 'C:\Users\admin\source\repos\+Playground\Applications\System\Manager\Claude.Shell.Native\bin\ClaudeContextMenu.msix' /nv /o"

if %errorlevel% equ 0 (
    echo [build] done: ClaudeContextMenu.msix
) else (
    echo [build] error: %errorlevel%
)

pause
