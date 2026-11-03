@echo off
setlocal
for %%I in ("%~dp0..") do set "CYBER_ROOT=%%~fI"
if exist "%CYBER_ROOT%\.tools\dotnet-tools\dotnet-ef.exe" goto local_tool
call "%~dp0dotnet-local.cmd" ef %*
exit /b %ERRORLEVEL%

:local_tool
if not exist "%CYBER_ROOT%\.tools\dotnet\dotnet.exe" goto run_tool
set "DOTNET_ROOT=%CYBER_ROOT%\.tools\dotnet"
set "PATH=%CYBER_ROOT%\.tools\dotnet;%PATH%"
set "DOTNET_CLI_HOME=%CYBER_ROOT%\.tools\dotnet-home"
set "NUGET_PACKAGES=%CYBER_ROOT%\.tools\nuget-packages"
set "NUGET_HTTP_CACHE_PATH=%CYBER_ROOT%\.tools\nuget-http-cache"
set "APPDATA=%CYBER_ROOT%\.tools\appdata"
if not exist "%APPDATA%" mkdir "%APPDATA%"

:run_tool
call "%CYBER_ROOT%\.tools\dotnet-tools\dotnet-ef.exe" %*
exit /b %ERRORLEVEL%
