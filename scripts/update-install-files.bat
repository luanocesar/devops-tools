@echo off
rem Developer script: after finishing changes, copies the working files into the
rem "install first" package (install\) that is committed to the repository.
setlocal
set ROOT=%~dp0..
robocopy "%ROOT%\server" "%ROOT%\install\server" /MIR /XD node_modules >nul
robocopy "%ROOT%\scripts" "%ROOT%\install\scripts" /MIR >nul
copy /y "%ROOT%\package.json" "%ROOT%\install\package.json" >nul
echo Install files updated.
exit /b 0
