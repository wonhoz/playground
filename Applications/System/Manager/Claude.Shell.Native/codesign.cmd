@echo off

net session >nul 2>&1
if %errorlevel% neq 0 (
    powershell -Command "Start-Process '%~f0' -Verb RunAs"
    exit /b
)

set PROJ=%~dp0

echo [codesign] signtool start...
powershell -NoProfile -ExecutionPolicy Bypass -Command "& 'C:\Program Files (x86)\Windows Kits\10\bin\10.0.26100.0\x64\signtool.exe' sign /fd SHA256 /tr http://timestamp.digicert.com /td SHA256 /sha1 9F106306FCC107B7B9A5F7B6F7A2424028521DB3 '%PROJ%bin\ClaudeContextMenu.msix'"

if %errorlevel% equ 0 (
    echo [codesign] done: ClaudeContextMenu.msix
) else (
    echo [codesign] error: %errorlevel%
)

pause
