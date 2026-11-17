@echo off
rem Installs devops-tools on this machine. Usage: install-server.bat [targetDir]
rem Requires Node.js. For IIS hosting, add the site/iisnode (or reverse proxy) pointing at targetDir.
setlocal
set TARGET=%~1
if "%TARGET%"=="" set TARGET=C:\inetpub\devops-tools
set SRC=%~dp0..
if exist "%SRC%\install\server" set SRC=%SRC%\install
robocopy "%SRC%\server" "%TARGET%\server" /E >nul
robocopy "%SRC%\scripts" "%TARGET%\scripts" /E >nul
copy /y "%SRC%\package.json" "%TARGET%\package.json" >nul
echo Installed to %TARGET%. Start with: cd /d %TARGET% ^&^& node server\server.js
exit /b 0
