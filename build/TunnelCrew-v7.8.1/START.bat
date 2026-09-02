@echo off
setlocal EnableDelayedExpansion
cd /d "%~dp0"

rem Usage: START.bat          -> port 5188 (or the next free port if 5188 is taken)
rem        START.bat 5189     -> force port 5189
set FIXED=%~1
if "%FIXED%"=="" (set PORT=5188) else (set PORT=%FIXED%)
set TRIES=0

:check
netstat -ano -p TCP | findstr /R /C:":%PORT% .*LISTENING" >nul
if not errorlevel 1 (
  echo  [*] Port %PORT% is already used by another program on this PC.
  if not "%FIXED%"=="" goto :busy
  set /a PORT+=1
  set /a TRIES+=1
  if !TRIES! GEQ 12 goto :busy
  echo      Trying port !PORT! instead ...
  goto :check
)

echo.
echo  ==========================================
echo   TUNNEL CREW v7.8.1  (LAN co-op server)
echo  ==========================================
echo.
echo  Starting server on port %PORT% ...
echo  If Windows Firewall asks, click ALLOW.
echo  Close this window to stop the server.
echo.

start "" cmd /c "timeout /t 2 >nul & start http://localhost:%PORT%/"
"%~dp0node\node.exe" "%~dp0coop\server.mjs" %PORT%

echo.
echo  Server stopped. (If it exited immediately, the port may be in use.)
echo  Try another port:  START.bat 5189
pause
exit /b

:busy
echo.
echo  Could not find a free port. Another server (maybe an older Tunnel Crew
echo  or a dev server) is running on this PC. Close it, or run:
echo      START.bat 5200
echo.
pause
exit /b 1
