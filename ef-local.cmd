@echo off
setlocal
set "DOTNET_ROOT=%~dp0.tools\dotnet"
set "PATH=%DOTNET_ROOT%;%PATH%"
set "DOTNET_CLI_HOME=%~dp0.tools\dotnet-home"
set "NUGET_PACKAGES=%~dp0.tools\nuget-packages"
set "NUGET_HTTP_CACHE_PATH=%~dp0.tools\nuget-http-cache"
set "APPDATA=%~dp0.tools\appdata"
call "%~dp0.tools\dotnet-tools\dotnet-ef.exe" %*
exit /b %ERRORLEVEL%
