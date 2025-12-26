@echo off
echo ========================================
echo Game OCR Overlay - Build Script
echo ========================================
echo.

echo Calling PowerShell build script...
powershell -ExecutionPolicy Bypass -File "%~dp0build.ps1"

if %ERRORLEVEL% NEQ 0 (
    echo.
    echo Build failed with error code %ERRORLEVEL%
    pause
    exit /b %ERRORLEVEL%
)

echo.
echo Build completed successfully!
echo Output: output\StellasoraPotentialOverlay.exe
pause