@echo off
setlocal
for %%I in ("%~dp0..") do set "CYBER_ROOT=%%~fI"
cd /d "%CYBER_ROOT%\frontend"
if not exist "node_modules\.bin\vite.cmd" (
  call "%~dp0npm-local.cmd" ci
  if errorlevel 1 exit /b 1
)
call "%~dp0npm-local.cmd" run dev -- --host 127.0.0.1 --port 5173 --strictPort
exit /b %ERRORLEVEL%
