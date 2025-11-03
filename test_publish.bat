@echo off
echo ========================================
echo Testing VetaleBrowser Publish and Run
echo ========================================
echo.

REM Очищення старих файлів публікації
if exist "publish" (
    echo Deleting old publish folder...
    rmdir /s /q publish
)

echo.
echo Building and publishing...
dotnet publish VetaleBrowser\VetaleBrowser.csproj -c Release -r win-x64 --self-contained -o publish

if %ERRORLEVEL% neq 0 (
    echo.
    echo ERROR: Build failed!
    pause
    exit /b 1
)

echo.
echo ========================================
echo Build successful!
echo ========================================
echo.
echo Published files are in: publish\
echo.
echo Starting application...
echo.

cd publish
VetaleBrowser.exe

cd ..
echo.
echo Application closed.
pause

