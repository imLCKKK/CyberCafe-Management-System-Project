param([string]$SqlServer = '127.0.0.1\SQLEXPRESS')
$ErrorActionPreference = 'Stop'
$counterWorkspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
Set-Location -LiteralPath $counterWorkspace
$counterData = Join-Path $counterWorkspace '.tools/counter-topup'
New-Item -ItemType Directory -Path $counterData -Force | Out-Null
$counterStartupLock = [IO.File]::Open((Join-Path $counterData 'startup.lock'), [IO.FileMode]::OpenOrCreate, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
try {
$counterStatePath = Join-Path $counterData 'state.json'
$counterDatabase = 'CyberManagement_CounterDemo_' + [Guid]::NewGuid().ToString('N')
if (Test-Path -LiteralPath $counterStatePath) {
    $prior = Get-Content -Raw -LiteralPath $counterStatePath | ConvertFrom-Json
    if ([IO.Path]::GetFullPath($prior.workspace) -ne $counterWorkspace -or $prior.database -notmatch '^CyberManagement_CounterDemo_[a-f0-9]{32}$') {
        throw 'The recorded demo belongs to another workspace; its database was not used.'
    }
    $counterDatabase = $prior.database
    $SqlServer = $prior.sqlServer
}
foreach ($counterPort in @(5094, 5176)) {
    if (Get-NetTCPConnection -State Listen -LocalPort $counterPort -ErrorAction SilentlyContinue) {
        throw "Port $counterPort is already occupied. Use stop-counter-topup.ps1 for this demo before restarting; no existing process was stopped."
    }
}
& (Join-Path $PSScriptRoot 'dotnet-local.cmd') build Backend/CyberManagement.slnx --nologo
if ($LASTEXITCODE -ne 0) { throw 'Backend build failed.' }
if (-not (Test-Path -LiteralPath 'frontend/node_modules/vite/bin/vite.js')) {
    & (Join-Path $PSScriptRoot 'npm-local.cmd') --prefix frontend ci
    if ($LASTEXITCODE -ne 0) { throw 'Frontend dependency installation failed.' }
}
$counterConnection = New-Object System.Data.SqlClient.SqlConnectionStringBuilder
$counterConnection['Data Source'] = $SqlServer
$counterConnection['Initial Catalog'] = $counterDatabase
$counterConnection['Integrated Security'] = $true
$counterConnection['TrustServerCertificate'] = $true
$counterConnection['Encrypt'] = $false
$counterConnection['Connect Timeout'] = 10
$counterEntropy = New-Object byte[] 48
$counterRandom = [Security.Cryptography.RandomNumberGenerator]::Create()
try { $counterRandom.GetBytes($counterEntropy) } finally { $counterRandom.Dispose() }
$counterEnvironment = @{
    ConnectionStrings__DefaultConnection = $counterConnection.ConnectionString
    Authentication__Jwt__SigningKey = [Convert]::ToBase64String($counterEntropy)
    ASPNETCORE_ENVIRONMENT = 'Development'
    Database__InitializeOnStartup = 'false'
    Payments__ZaloPaySandbox__Enabled = 'false'
    API_PROXY_TARGET = 'http://127.0.0.1:5094'
    VITE_API_PROXY_TARGET = 'http://127.0.0.1:5094'
    VITE_API_BASE_URL = '/api'
}
$previousEnvironment = @{}
foreach ($name in $counterEnvironment.Keys) {
    $previousEnvironment[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
    [Environment]::SetEnvironmentVariable($name, $counterEnvironment[$name], 'Process')
}
$counterApi = $null
$counterFrontend = $null
try {
    @{
        workspace = $counterWorkspace; database = $counterDatabase; sqlServer = $SqlServer
        apiProcessId = $null; frontendProcessId = $null
        apiUrl = 'http://127.0.0.1:5094'; frontendUrl = 'http://127.0.0.1:5176'
    } | ConvertTo-Json | Set-Content -Encoding UTF8 -LiteralPath $counterStatePath
    & (Join-Path $PSScriptRoot 'dotnet-local.cmd') run --no-build --project Backend/tests/CyberManagement.API.Checks -- --counter-demo-seed
    if ($LASTEXITCODE -ne 0) { throw 'Isolated demo SQL setup failed.' }
    $counterApiDll = Join-Path $counterWorkspace 'Backend/src/CyberManagement.API/bin/Debug/net10.0/CyberManagement.API.dll'
    $counterViteJs = Join-Path $counterWorkspace 'frontend/node_modules/vite/bin/vite.js'
    $counterApi = Start-Process -FilePath (Get-Command dotnet.exe).Source -ArgumentList @(('"' + $counterApiDll + '"'), '--urls', 'http://127.0.0.1:5094') -WorkingDirectory $counterWorkspace -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $counterData 'api.log') -RedirectStandardError (Join-Path $counterData 'api.error.log')
    $counterFrontend = Start-Process -FilePath (Get-Command node.exe).Source -ArgumentList @(('"' + $counterViteJs + '"'), '--host', '127.0.0.1', '--port', '5176', '--strictPort') -WorkingDirectory (Join-Path $counterWorkspace 'frontend') -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $counterData 'frontend.log') -RedirectStandardError (Join-Path $counterData 'frontend.error.log')
    @{
        workspace = $counterWorkspace; database = $counterDatabase; sqlServer = $SqlServer
        apiProcessId = $counterApi.Id; frontendProcessId = $counterFrontend.Id
        apiUrl = 'http://127.0.0.1:5094'; frontendUrl = 'http://127.0.0.1:5176'
    } | ConvertTo-Json | Set-Content -Encoding UTF8 -LiteralPath $counterStatePath
    $counterDeadline = [DateTime]::UtcNow.AddSeconds(30)
    $counterReady = $false
    while ([DateTime]::UtcNow -lt $counterDeadline) {
        $counterApi.Refresh()
        $counterFrontend.Refresh()
        if ($counterApi.HasExited -or $counterFrontend.HasExited) { throw 'A demo process exited; startup did not take ownership of both ports.' }
        try {
            $counterHealth = Invoke-WebRequest -UseBasicParsing -Uri 'http://127.0.0.1:5094/health' -TimeoutSec 2
            $counterPage = Invoke-WebRequest -UseBasicParsing -Uri 'http://127.0.0.1:5176/client/auth' -TimeoutSec 2
            $apiOwned = @(Get-NetTCPConnection -State Listen -LocalPort 5094 -ErrorAction SilentlyContinue | Where-Object OwningProcess -eq $counterApi.Id).Count -gt 0
            $frontendOwned = @(Get-NetTCPConnection -State Listen -LocalPort 5176 -ErrorAction SilentlyContinue | Where-Object OwningProcess -eq $counterFrontend.Id).Count -gt 0
            if ($counterHealth.StatusCode -eq 200 -and $counterPage.StatusCode -eq 200 -and $apiOwned -and $frontendOwned) { $counterReady = $true; break }
        } catch { Start-Sleep -Milliseconds 300 }
    }
    if (-not $counterReady) { throw 'Counter demo startup timed out. See its .tools/counter-topup logs.' }
    Write-Host 'SCRUM-21 ready: http://127.0.0.1:5176/client/auth'
    Write-Host 'Customer: 0900000021 / CounterCustomer-2026!'
    Write-Host 'Admin: http://127.0.0.1:5176/admin/auth (admin / CounterAdmin-2026!)'
    Write-Host 'Use /admin/topups to confirm cash received. This is a separate, retained SQL demo database.'
} catch {
    foreach ($counterOwned in @(
        @{ Process = $counterFrontend; Name = 'node.exe'; Path = $counterViteJs },
        @{ Process = $counterApi; Name = 'dotnet.exe'; Path = $counterApiDll }
    )) {
        if (-not $counterOwned.Process) { continue }
        $counterActual = Get-CimInstance Win32_Process -Filter "ProcessId = $($counterOwned.Process.Id)" -ErrorAction SilentlyContinue
        if ($counterActual -and $counterActual.Name -eq $counterOwned.Name -and $counterActual.CommandLine.IndexOf($counterOwned.Path, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
            Stop-Process -Id $counterOwned.Process.Id -ErrorAction SilentlyContinue
        }
    }
    throw
} finally {
    foreach ($name in $previousEnvironment.Keys) {
        [Environment]::SetEnvironmentVariable($name, $previousEnvironment[$name], 'Process')
    }
}
} finally { $counterStartupLock.Dispose() }
