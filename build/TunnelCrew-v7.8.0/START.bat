@echo off
setlocal
cd /d "%~dp0"

rem Optional: START.bat 5189  -> run on port 5189
if not "%~1"=="" set PORT=%~1
if "%PORT%"=="" set PORT=5188

echo.
echo  ==========================================
echo   TUNNEL CREW v7.8.0  (LAN co-op server)
echo  ==========================================
echo.
echo  Starting server on port %PORT% ...
echo  If Windows Firewall asks, click ALLOW.
echo  Close this window to stop the server.
echo.

start "" cmd /c "timeout /t 2 >nul & start http://localhost:%PORT%/"
"%~dp0node\node.exe" "%~dp0coop\server.mjs"

echo.
echo  Server stopped. (If it exited immediately, the port may be in use.)
echo  Try another port:  START.bat 5189
pause
