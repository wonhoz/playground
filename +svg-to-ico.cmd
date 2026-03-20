@echo off
chcp 65001 >nul
powershell -ExecutionPolicy Bypass -File "%~dp0+svg-to-ico.ps1"
