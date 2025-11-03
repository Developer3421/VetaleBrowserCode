@echo off
echo ========================================
echo Building VetaleBrowser with Console Output
echo ========================================
echo.

REM Очищення старих файлів публікації
if exist "publish_console" (
    echo Deleting old publish_console folder...
    rmdir /s /q publish_console
)

echo.
echo Building and publishing with console output...
dotnet publish VetaleBrowser\VetaleBrowser.csproj -c Release -r win-x64 --self-contained -o publish_console /p:PublishSingleFile=false /p:OutputType=Exe

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
echo Published files are in: publish_console\
echo.
echo Starting application with console output...
echo Press Ctrl+C to stop
echo.

cd publish_console
VetaleBrowser.exe > ..\console_output.log 2>&1

cd ..
echo.
echo Application closed.
echo Output saved to: console_output.log
echo.
type console_output.log
echo.
pause

