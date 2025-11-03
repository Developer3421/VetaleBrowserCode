@echo off
echo ========================================
echo Testing VetaleBrowser - Non-Single-File
echo ========================================
echo.

REM Очищення старих файлів публікації
if exist "publish_test" (
    echo Deleting old publish_test folder...
    rmdir /s /q publish_test
)

echo.
echo Building and publishing (non-single-file for debugging)...
dotnet publish VetaleBrowser\VetaleBrowser.csproj -c Release -r win-x64 --self-contained -o publish_test /p:PublishSingleFile=false

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
echo Published files are in: publish_test\
echo.
echo Starting application...
echo.

cd publish_test
VetaleBrowser.exe

cd ..
echo.
echo Application closed.
pause

