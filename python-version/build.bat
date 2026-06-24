@echo off
chcp 65001 >nul
REM ============================================================================
REM  Python 版本 — 打包脚本 (PyInstaller)
REM  将 download_mover.py 打包为单个 .exe 文件
REM ============================================================================

echo.
echo ╔══════════════════════════════════════════════════════╗
echo ║  下载路径修改工具 — Python 版打包脚本                ║
echo ╚══════════════════════════════════════════════════════╝
echo.

REM ---------------------------------------------------------------------------
REM 步骤 1: 检查 Python 是否已安装
REM ---------------------------------------------------------------------------
where python >nul 2>&1
if %errorlevel% neq 0 (
    echo [错误] 未找到 Python。请先安装 Python 3.10+。
    echo        下载地址: https://www.python.org/downloads/
    echo        安装时务必勾选 "Add Python to PATH"。
    pause
    exit /b 1
)

echo [✓] Python 已找到:
python --version
echo.

REM ---------------------------------------------------------------------------
REM 步骤 2: 安装 PyInstaller
REM ---------------------------------------------------------------------------
echo [信息] 正在安装/更新 PyQt6 和 PyInstaller...
pip install pyqt6 pyinstaller --upgrade
if %errorlevel% neq 0 (
    echo [错误] PyInstaller 安装失败，请检查网络连接。
    pause
    exit /b 1
)
echo [✓] PyInstaller 已就绪
echo.

REM ---------------------------------------------------------------------------
REM 步骤 3: 清理旧的构建产物
REM ---------------------------------------------------------------------------
if exist "build" rmdir /s /q "build"
if exist "dist"  rmdir /s /q "dist"
echo [✓] 已清理旧的构建产物
echo.

REM ---------------------------------------------------------------------------
REM 步骤 4: 使用 PyInstaller 打包
REM ---------------------------------------------------------------------------
echo [信息] 正在打包为单个 .exe 文件...
echo.
echo   参数说明:
echo     --onefile          打包成单个 .exe 文件
echo     --windowed         不显示控制台窗口 (纯 GUI)
echo     --name             输出文件名
echo     --icon             可选的图标文件
echo     --uac-admin        嵌入 UAC 管理员权限清单
echo     --clean            清理临时文件
echo.

pyinstaller ^
    --onefile ^
    --windowed ^
    --name "DownloadMover" ^
    --uac-admin ^
    --clean ^
    download_mover.py

if %errorlevel% neq 0 (
    echo.
    echo [错误] 打包失败！请检查上方错误信息。
    pause
    exit /b 1
)

echo.
echo ╔══════════════════════════════════════════════════════╗
echo ║  ✅ 打包成功！                                       ║
echo ║                                                      ║
echo ║  输出文件: dist\DownloadMover.exe                    ║
echo ║                                                      ║
echo ║  双击运行即可。首次运行会弹出 UAC 提示。             ║
echo ╚══════════════════════════════════════════════════════╝
echo.

REM ---------------------------------------------------------------------------
REM 步骤 5: 打开输出目录
REM ---------------------------------------------------------------------------
explorer dist
pause
