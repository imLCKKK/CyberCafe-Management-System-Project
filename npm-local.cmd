@echo off
setlocal
set "NODE_HOME=%~dp0.tools\node-v24.22.0-win-x64"
if not exist "%NODE_HOME%\node.exe" (
  echo Node.js portable is missing from "%NODE_HOME%".
  exit /b 1
)
set "PATH=%NODE_HOME%;%PATH%"
set "npm_config_cache=%~dp0.tools\npm-cache"
call "%NODE_HOME%\npm.cmd" %*
exit /b %ERRORLEVEL%
