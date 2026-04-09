@echo off
:: Claude Code Context Menu - Custom Terminal Configuration
:: Registry key: HKCU\Software\ClaudeCode
::   TerminalPath (REG_SZ)   - full path to terminal executable
::   TerminalType (REG_SZ)   - "wt" (Windows Terminal args) or "cmd" (cmd.exe args)
::   NewTab       (REG_DWORD) - 1 = open in new tab (wt.exe only)

echo ============================================================
echo  Claude Code Context Menu - Terminal Setup
echo ============================================================
echo.
echo Current settings:
reg query "HKCU\Software\ClaudeCode" /v TerminalPath 2>nul || echo   TerminalPath: (not set - auto detect)
reg query "HKCU\Software\ClaudeCode" /v TerminalType 2>nul || echo   TerminalType: (not set - wt)
reg query "HKCU\Software\ClaudeCode" /v NewTab       2>nul || echo   NewTab:       (not set - 0)
echo.
echo Options:
echo   1. Set custom terminal path
echo   2. Set terminal type (wt/cmd)
echo   3. Toggle new-tab mode
echo   4. Remove all settings (restore defaults)
echo   5. Exit
echo.
set /p CHOICE=Select option (1-5):

if "%CHOICE%"=="1" goto SET_PATH
if "%CHOICE%"=="2" goto SET_TYPE
if "%CHOICE%"=="3" goto SET_NEWTAB
if "%CHOICE%"=="4" goto REMOVE_ALL
goto END

:SET_PATH
echo.
echo Enter full path to terminal executable.
echo Example: C:\Users\admin\AppData\Local\Programs\Hyper\Hyper.exe
echo Leave blank to remove (use auto-detect).
echo.
set /p TERM_PATH=Terminal path:
if "%TERM_PATH%"=="" (
    reg delete "HKCU\Software\ClaudeCode" /v TerminalPath /f 2>nul
    echo TerminalPath removed.
) else (
    if not exist "%TERM_PATH%" (
        echo WARNING: File not found: %TERM_PATH%
        echo Saving anyway - verify the path is correct.
    )
    reg add "HKCU\Software\ClaudeCode" /v TerminalPath /t REG_SZ /d "%TERM_PATH%" /f >nul
    echo TerminalPath saved.
)
goto END

:SET_TYPE
echo.
echo Terminal argument style:
echo   wt  - Windows Terminal style: -d "path" cmd /k claude  (default)
echo   cmd - cmd.exe style:          /k cd /d "path" ^&^& claude
echo.
set /p TERM_TYPE=Type (wt/cmd):
if /i "%TERM_TYPE%"=="wt"  goto SET_TYPE_WT
if /i "%TERM_TYPE%"=="cmd" goto SET_TYPE_CMD
echo Invalid input. Use "wt" or "cmd".
goto END

:SET_TYPE_WT
reg add "HKCU\Software\ClaudeCode" /v TerminalType /t REG_SZ /d "wt" /f >nul
echo TerminalType set to "wt".
goto END

:SET_TYPE_CMD
reg add "HKCU\Software\ClaudeCode" /v TerminalType /t REG_SZ /d "cmd" /f >nul
echo TerminalType set to "cmd".
goto END

:SET_NEWTAB
echo.
set /p NT_VAL=Enable new-tab mode? (1=yes / 0=no):
if "%NT_VAL%"=="1" (
    reg add "HKCU\Software\ClaudeCode" /v NewTab /t REG_DWORD /d 1 /f >nul
    echo NewTab enabled.
) else (
    reg add "HKCU\Software\ClaudeCode" /v NewTab /t REG_DWORD /d 0 /f >nul
    echo NewTab disabled.
)
goto END

:REMOVE_ALL
reg delete "HKCU\Software\ClaudeCode" /v TerminalPath /f 2>nul
reg delete "HKCU\Software\ClaudeCode" /v TerminalType /f 2>nul
reg delete "HKCU\Software\ClaudeCode" /v NewTab /f 2>nul
echo All custom terminal settings removed.
goto END

:END
echo.
pause
