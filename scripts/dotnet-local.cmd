@echo off
setlocal
for %%I in ("%~dp0..") do set "CYBER_ROOT=%%~fI"
if exist "%CYBER_ROOT%\.tools\dotnet\dotnet.exe" goto portable
where dotnet.exe >nul 2>nul
if errorlevel 1 (
  echo .NET SDK is missing. Install the .NET 10 SDK, then open a new terminal.
  exit /b 1
)
call dotnet.exe %*
exit /b %ERRORLEVEL%

:portable
set "DOTNET_ROOT=%CYBER_ROOT%\.tools\dotnet"
set "PATH=%DOTNET_ROOT%;%PATH%"
set "DOTNET_CLI_HOME=%CYBER_ROOT%\.tools\dotnet-home"
set "NUGET_PACKAGES=%CYBER_ROOT%\.tools\nuget-packages"
set "NUGET_HTTP_CACHE_PATH=%CYBER_ROOT%\.tools\nuget-http-cache"
set "APPDATA=%CYBER_ROOT%\.tools\appdata"
if not exist "%APPDATA%" mkdir "%APPDATA%"
call "%DOTNET_ROOT%\dotnet.exe" %*
exit /b %ERRORLEVEL%
