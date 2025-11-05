@echo off
echo ====================================
echo Installing Playwright Browsers
echo ====================================
echo.

echo Checking for Playwright CLI...
where playwright >nul 2>&1
if %ERRORLEVEL% NEQ 0 (
    echo Playwright CLI not found in PATH
    echo Installing globally...
    dotnet tool install --global Microsoft.Playwright.CLI
    echo.
)

echo Installing Chromium browser...
playwright install chromium

echo.
echo ====================================
echo Installation complete!
echo ====================================
echo.
echo You can now run VetaleBrowser with Playwright support
echo.
pause

