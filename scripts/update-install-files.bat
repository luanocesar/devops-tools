@echo off
rem Developer script: refreshes the install package after finishing changes.
setlocal
set ROOT=%~dp0..
if not exist "%ROOT%\install\server" mkdir "%ROOT%\install\server"
if not exist "%ROOT%\install\scripts" mkdir "%ROOT%\install\scripts"
robocopy "%ROOT%\server" "%ROOT%\install\server" /MIR /XD bin obj >nul
robocopy "%ROOT%\scripts" "%ROOT%\install\scripts" /MIR >nul
echo Install files updated.
exit /b 0
