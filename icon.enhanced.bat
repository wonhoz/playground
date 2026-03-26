@echo off
chcp 65001 >nul

echo [1] explorer 종료
taskkill /f /im explorer.exe >nul 2>&1

echo [2] 트레이 아이콘 캐시 삭제
reg delete "HKCU\Software\Classes\Local Settings\Software\Microsoft\Windows\CurrentVersion\TrayNotify" /v IconStreams /f >nul 2>&1
reg delete "HKCU\Software\Classes\Local Settings\Software\Microsoft\Windows\CurrentVersion\TrayNotify" /v PastIconsStream /f >nul 2>&1

echo [3] 아이콘 캐시 삭제
del /a /f /q "%localappdata%\IconCache.db" >nul 2>&1
del /a /f /q "%localappdata%\Microsoft\Windows\Explorer\iconcache*" >nul 2>&1

::echo [4] 아이콘 캐시 초기화
::ie4uinit.exe -ClearIconCache

echo [5] explorer 재시작
start explorer.exe

echo 완료
pause
