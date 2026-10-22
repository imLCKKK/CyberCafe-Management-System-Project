@echo off
setlocal
set "DOTNET_ROOT=%~dp0.tools\dotnet"
if not exist "%DOTNET_ROOT%\dotnet.exe" (
  echo .NET SDK is missing from "%DOTNET_ROOT%".
  exit /b 1
)
set "PATH=%DOTNET_ROOT%;%PATH%"
set "DOTNET_CLI_HOME=%~dp0.tools\dotnet-home"
set "NUGET_PACKAGES=%~dp0.tools\nuget-packages"
set "NUGET_HTTP_CACHE_PATH=%~dp0.tools\nuget-http-cache"
set "APPDATA=%~dp0.tools\appdata"
if not exist "%APPDATA%" mkdir "%APPDATA%"
call "%DOTNET_ROOT%\dotnet.exe" %*
exit /b %ERRORLEVEL%
