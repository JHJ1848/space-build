@echo off
setlocal

set "PROJECT_PATH=%~dp0UnityProject"
set "UNITY_EXE=C:\Program Files\Unity\Hub\Editor\6000.4.0f1\Editor\Unity.exe"

if not exist "%PROJECT_PATH%" (
    echo [ERROR] Unity project not found:
    echo %PROJECT_PATH%
    pause
    exit /b 1
)

if not exist "%UNITY_EXE%" (
    echo [ERROR] Unity editor not found:
    echo %UNITY_EXE%
    echo.
    echo Please update UNITY_EXE in this script.
    pause
    exit /b 1
)

echo Launching Unity project...
echo Editor : %UNITY_EXE%
echo Project: %PROJECT_PATH%

start "" "%UNITY_EXE%" -projectPath "%PROJECT_PATH%"
exit /b 0
