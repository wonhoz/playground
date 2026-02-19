@echo off
setlocal enabledelayedexpansion

set "SLN=%~dp0Playground.sln"
set "FAILED=0"
set "DEBUG_OK=0"
set "RELEASE_OK=0"

echo.
echo ---------------------------------------------
echo   Building [Debug]
echo ---------------------------------------------
echo.

dotnet build "%SLN%" -c Debug --nologo
if !ERRORLEVEL! equ 0 (set "DEBUG_OK=1") else (set "FAILED=1")

echo.
echo ---------------------------------------------
echo   Building [Release]
echo ---------------------------------------------
echo.

dotnet build "%SLN%" -c Release --nologo
if !ERRORLEVEL! equ 0 (set "RELEASE_OK=1") else (set "FAILED=1")

echo.
echo ---------------------------------------------
echo   Summary
echo ---------------------------------------------

if !DEBUG_OK! equ 1 (echo   [OK]  Debug      Build succeeded) else (echo   [!!]  Debug      Build FAILED)
if !RELEASE_OK! equ 1 (echo   [OK]  Release    Build succeeded) else (echo   [!!]  Release    Build FAILED)

echo ---------------------------------------------
echo.

if !FAILED! equ 0 (
    echo   All configurations built successfully!
    echo.
    exit /b 0
) else (
    echo   Build failed. Check errors above.
    echo.
    exit /b 1
)
