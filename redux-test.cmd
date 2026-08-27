@echo off
pwsh -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0redux-test.ps1" %*
exit /b %ERRORLEVEL%

