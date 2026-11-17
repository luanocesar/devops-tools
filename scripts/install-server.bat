@echo off
rem Publishes and installs devops-tools. Usage: install-server.bat [targetDir]
rem Requires the .NET 8 SDK. Configure IIS to host the published ASP.NET Core app.
setlocal
set TARGET=%~1
if "%TARGET%"=="" set TARGET=C:\inetpub\devops-tools
set SRC=%~dp0..
dotnet publish "%SRC%\server\DevOpsTools\DevOpsTools.csproj" --configuration Release --output "%TARGET%\server"
if errorlevel 1 exit /b 1
if not exist "%TARGET%\scripts" mkdir "%TARGET%\scripts"
copy /y "%SRC%\scripts\deploy.bat" "%TARGET%\scripts\deploy.bat" >nul
echo Installed to %TARGET%. Configure IIS to host "%TARGET%\server\DevOpsTools.dll".
exit /b 0
