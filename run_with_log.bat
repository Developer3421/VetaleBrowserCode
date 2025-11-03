@echo off
echo Running VetaleBrowser with error logging...
echo.
echo Log will be saved to: run_log.txt
echo.

cd publish
VetaleBrowser.exe 2>&1 | tee ../run_log.txt

cd ..
echo.
echo Application closed. Check run_log.txt for any errors.
pause

