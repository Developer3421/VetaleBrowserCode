@echo off
echo ========================================
echo Testing Vetale AI Chat Integration
echo ========================================
echo.

cd /d E:\VetaleBrowser

echo [1/3] Building solution...
dotnet build VetaleBrowser.sln --no-restore -v quiet

if %ERRORLEVEL% NEQ 0 (
    echo ERROR: Build failed!
    pause
    exit /b 1
)

echo [2/3] Build successful!
echo.

echo [3/3] Test Instructions:
echo.
echo 1. Run the application
echo 2. Click on the Tools button (wrench icon)
echo 3. Click on "Vetale AI Chat" (robot icon)
echo 4. The VetaleAI window should open
echo 5. Test features:
echo    - Type a message and press Enter
echo    - Toggle "Enable Reasoning"
echo    - Select different languages
echo    - Click "New Chat"
echo.

echo ========================================
echo Ready to test! Press any key to exit...
pause > nul

