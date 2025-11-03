@echo off
setlocal enabledelayedexpansion

echo ========================================
echo VetaleBrowser Quick Test
echo ========================================
echo.

echo Cleaning old builds...
if exist "bin\Release" rmdir /s /q "bin\Release" 2>nul
if exist "obj\Release" rmdir /s /q "obj\Release" 2>nul

echo.
echo Building in Release mode...
cd VetaleBrowser
dotnet build -c Release --no-incremental

if %ERRORLEVEL% neq 0 (
    echo.
    echo ERROR: Build failed!
    cd ..
    pause
    exit /b 1
)

echo.
echo Build successful! Starting application...
echo.

cd bin\Release\net9.0
VetaleBrowser.exe

cd ..\..\..\..
echo.
echo Application closed.
pause

