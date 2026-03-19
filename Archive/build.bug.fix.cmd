@echo off
chcp 65001 >nul
setlocal enabledelayedexpansion

for /f %%a in ('powershell -Command "[char]27"') do set "ESC=%%a"

echo.
echo   %ESC%[96m■%ESC%[0m 빌드 캐시 복구
echo   %ESC%[90m  .NET 10 SDK 프로젝트 obj\ 캐시 무효화 복구%ESC%[0m
echo   %ESC%[90m  └%ESC%[0m %date% %time:~0,8%
echo.
echo   %ESC%[90m─────────────────────────────────────────────────────────%ESC%[0m
echo.
echo   %ESC%[93m■%ESC%[0m 원인
echo   %ESC%[90m  └%ESC%[0m NuGet 패키지 변경 커밋 pull 후 obj\ 캐시 무효화
echo   %ESC%[90m  └%ESC%[0m MSBuild 가 MarkupCompile 단계를 스킵 -^> .g.cs 미생성
echo   %ESC%[90m  └%ESC%[0m InitializeComponent / Main 진입점 못 찾아 빌드 실패
echo   %ESC%[93m■%ESC%[0m 해결
echo   %ESC%[90m  └%ESC%[0m 모든 프로젝트 obj\ 삭제 후 dotnet restore
echo.
echo   %ESC%[90m─────────────────────────────────────────────────────────%ESC%[0m
echo.

set "ROOT=%~dp0"
set FAIL=0
set COUNT=0

:: ======================================================================
:: [1단계] 모든 프로젝트 obj\ 재귀 삭제 (Archieve 제외)
:: ======================================================================
echo   %ESC%[96m■%ESC%[0m [1/2] 프로젝트 obj\ 캐시 초기화 중...
echo.

for /d /r "%ROOT%" %%i in (obj) do (
    set "OBJ_PATH=%%i"
    echo !OBJ_PATH! | findstr /i "\\Archieve\\" >nul 2>&1
    if !errorlevel! neq 0 (
        if exist "!OBJ_PATH!" (
            rd /s /q "!OBJ_PATH!"
            if !errorlevel! equ 0 (
                set /a COUNT+=1
                echo   %ESC%[92m✓%ESC%[0m 삭제: %%~ni ^(%%~pi^)
            ) else (
                echo   %ESC%[91m✗%ESC%[0m 실패: !OBJ_PATH!
                set /a FAIL+=1
            )
        )
    )
)

echo.
echo   %ESC%[90m  총 %COUNT% 개 obj\ 삭제 완료%ESC%[0m
echo.

:: ======================================================================
:: [2단계] dotnet restore
:: ======================================================================
echo   %ESC%[96m■%ESC%[0m [2/2] NuGet restore 중...
echo   %ESC%[90m─────────────────────────────────────────────────────────%ESC%[0m

dotnet restore "%ROOT%Playground.slnx" --nologo -v minimal
if !errorlevel! equ 0 (
    echo   %ESC%[92m✓%ESC%[0m dotnet restore 완료
) else (
    echo   %ESC%[91m✗%ESC%[0m dotnet restore 실패
    set /a FAIL+=1
)

echo.
echo   %ESC%[90m─────────────────────────────────────────────────────────%ESC%[0m
echo.

if !FAIL! equ 0 (
    echo   %ESC%[92m✓%ESC%[0m [완료] 캐시 초기화 및 복구가 완료되었습니다.
    echo   %ESC%[90m  └%ESC%[0m 이제 dotnet build 또는 Visual Studio 에서 빌드하세요.
) else (
    echo   %ESC%[91m✗%ESC%[0m [경고] 일부 단계에서 실패가 발생했습니다. 위 오류를 확인하세요.
)

echo.
echo   %ESC%[90m─────────────────────────────────────────────────────────%ESC%[0m
echo.

:: ======================================================================
:: [3단계] 선택적 빌드 검증
:: ======================================================================
set /p "VERIFY=  빌드 검증하시겠습니까? [Y/N] "
if /i "!VERIFY!"=="Y" (
    echo.
    echo   %ESC%[96m■%ESC%[0m [3/3] Debug 빌드 검증 중...
    echo   %ESC%[90m─────────────────────────────────────────────────────────%ESC%[0m
    dotnet build "%ROOT%Playground.slnx" -c Debug --nologo -v minimal
    if !errorlevel! equ 0 (
        echo.
        echo   %ESC%[92m✓%ESC%[0m 빌드 검증 성공 - 복구가 정상적으로 완료되었습니다.
    ) else (
        echo.
        echo   %ESC%[91m✗%ESC%[0m 빌드 검증 실패 - 추가 조치가 필요합니다.
    )
    echo.
    echo   %ESC%[90m─────────────────────────────────────────────────────────%ESC%[0m
    echo.
)

endlocal
pause
