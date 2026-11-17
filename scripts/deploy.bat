@echo off
rem Usage: deploy.bat <package.zip> <siteName> <backendAppPool|-> <appsJson>
rem Stops the backend (.NET) app pool, replaces application files, starts it again.
setlocal
set ZIP=%~1
set SITE=%~2
set POOL=%~3
set APPS=%~4
set APPCMD=%windir%\system32\inetsrv\appcmd.exe
set STAGE=%TEMP%\devops-stage-%RANDOM%

powershell -NoProfile -Command "Expand-Archive -LiteralPath $env:ZIP -DestinationPath $env:STAGE -Force"
if errorlevel 1 exit /b 1

if not "%POOL%"=="-" "%APPCMD%" stop apppool /apppool.name:"%POOL%"

rem Each app: <stage>\<name> is copied over its targetPath.
powershell -NoProfile -Command "$apps = $env:APPS | ConvertFrom-Json; foreach ($a in $apps) { $src = Join-Path $env:STAGE $a.name; if (-not (Test-Path $src)) { $src = $env:STAGE }; robocopy $src $a.targetPath /MIR /XF web.config appsettings.Production.json | Out-Null; if ($LASTEXITCODE -ge 8) { exit 1 } }; exit 0"
set RC=%ERRORLEVEL%

if not "%POOL%"=="-" "%APPCMD%" start apppool /apppool.name:"%POOL%"
rmdir /s /q "%STAGE%"
exit /b %RC%
