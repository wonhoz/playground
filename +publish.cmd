@echo off
chcp 65001 >nul
powershell -ExecutionPolicy Bypass -File "%~dp0+publish-ui.ps1" -Root "%~dp0"
