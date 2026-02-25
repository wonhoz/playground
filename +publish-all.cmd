@echo off
chcp 65001 >nul
:: 전체 프로젝트 배포 — 선택 UI 없이 즉시 실행
powershell -ExecutionPolicy Bypass -File "%~dp0+publish.ps1" -All
