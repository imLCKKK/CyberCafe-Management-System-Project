@echo off
setlocal
for %%I in ("%~dp0..") do set "CYBER_ROOT=%%~fI"
set "CYBER_NODE_HOME=%CYBER_ROOT%\.tools\node-v24.22.0-win-x64"
if exist "%CYBER_NODE_HOME%\node.exe" goto portable
where npm.cmd >nul 2>nul
if errorlevel 1 (
  echo Node.js is missing. Install Node.js 22 or newer, then open a new terminal.
  exit /b 1
)
call npm.cmd %*
exit /b %ERRORLEVEL%

:portable
set "PATH=%CYBER_NODE_HOME%;%PATH%"
set "npm_config_cache=%CYBER_ROOT%\.tools\npm-cache"
call "%CYBER_NODE_HOME%\npm.cmd" %*
exit /b %ERRORLEVEL%
