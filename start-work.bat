@echo off
setlocal EnableExtensions EnableDelayedExpansion
chcp 65001 >nul

set "WORKDIR=C:\Users\jinghongjie\Desktop\space-build"
set "LOGFILE=%WORKDIR%\太空建造开发日志.md"
set "UNITY_PROJECT=%WORKDIR%\UnityProject"
set "UNITY_VERSION_FILE=%UNITY_PROJECT%\ProjectSettings\ProjectVersion.txt"
set "UNITY_VERSION="
set "UNITY_EXE="
set "UNITY_HUB_EXE=C:\Program Files\Unity Hub\Unity Hub.exe"

start "终端窗口1" cmd /k "cd /d ""%WORKDIR%"" && omx --high"

if exist "%LOGFILE%" (
    start "开发日志" "%LOGFILE%"
) else (
    echo [WARN] 未找到日志文件: %LOGFILE%
)

if exist "%UNITY_VERSION_FILE%" (
    for /f "tokens=2 delims=: " %%i in ('findstr /b "m_EditorVersion:" "%UNITY_VERSION_FILE%"') do set "UNITY_VERSION=%%i"
    set "UNITY_EXE=C:\Program Files\Unity\Hub\Editor\!UNITY_VERSION!\Editor\Unity.exe"

    if exist "!UNITY_EXE!" (
        echo [INFO] 正在打开 Unity 项目: %UNITY_PROJECT%
        echo [INFO] 使用 Unity Editor: !UNITY_EXE!
        start "Unity 项目" "!UNITY_EXE!" -projectPath "%UNITY_PROJECT%"
    ) else (
        echo [WARN] 未找到匹配版本的 Unity Editor: !UNITY_EXE!
        if exist "%UNITY_HUB_EXE%" (
            echo [INFO] 回退为仅打开 Unity Hub，请在 Hub 中手动添加/打开项目。
            start "Unity Hub" "%UNITY_HUB_EXE%"
        ) else (
            echo [WARN] 未找到 Unity Hub: %UNITY_HUB_EXE%
        )
    )
) else (
    echo [WARN] 未找到 Unity 项目版本文件: %UNITY_VERSION_FILE%
)

endlocal
