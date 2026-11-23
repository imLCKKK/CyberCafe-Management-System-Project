param(
    [string]$SqlConnection = 'Server=127.0.0.1\SQLEXPRESS;Integrated Security=true;TrustServerCertificate=true;Connect Timeout=10',
    [string]$CallbackUrl,
    [switch]$PrepareOrder,
    [switch]$NewOrderFixture,
    [string]$ArtifactsPath
)
$ErrorActionPreference = 'Stop'
$taskWorkspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$taskDll = Join-Path $taskWorkspace 'Backend\tests\CyberManagement.API.Checks\bin\Debug\net10.0\CyberManagement.API.Checks.dll'
if ($ArtifactsPath) { $taskDll = Join-Path ([IO.Path]::GetFullPath($ArtifactsPath)) 'bin\CyberManagement.API.Checks\debug\CyberManagement.API.Checks.dll' }
if ($NewOrderFixture -and !$PrepareOrder) { throw 'NewOrderFixture requires PrepareOrder and preserves the prior SQL source.' }
if (!(Test-Path -LiteralPath $taskDll)) { throw 'Build Backend/CyberManagement.slnx first.' }
if ($CallbackUrl) {
    $callback = [Uri]$CallbackUrl
    if (!$callback.IsAbsoluteUri -or $callback.Scheme -ne 'https' -or $callback.IsLoopback -or $callback.UserInfo -or $callback.Query -or $callback.Fragment -or $callback.AbsolutePath -ne '/api/payments/zalopay/callback') {
        throw 'CallbackUrl must be the public HTTPS callback route.'
    }
    $env:Payments__ZaloPaySandbox__CallbackUrl = $CallbackUrl
}
$env:AUTH_CHECKS_SQL_CONNECTION = $SqlConnection
$env:Payments__ZaloPaySandbox__RedirectUrl = 'http://localhost:5177/client/wallet'
$env:VITE_API_PROXY_TARGET = 'http://127.0.0.1:5099'
Push-Location $taskWorkspace
try {
    if ($PrepareOrder) {
        $taskArguments = @($taskDll, '--zalopay-integration-prepare-order')
        if ($NewOrderFixture) { $taskArguments += '--new-order-fixture' }
        & dotnet @taskArguments
        if ($LASTEXITCODE -ne 0) { throw 'Could not prepare the retained Order contract fixture.' }
        return
    }
    foreach ($port in @(5099, 5100, 5177)) {
        if (Get-NetTCPConnection -State Listen -LocalPort $port -ErrorAction SilentlyContinue) {
            throw "Port $port is occupied. Reuse its existing demo or stop that verified process before starting again."
        }
    }
    $logs = Join-Path $taskWorkspace '.tools\integration-sandbox'
    New-Item -ItemType Directory -Path $logs -Force | Out-Null
    $api = Start-Process -FilePath (Get-Command dotnet.exe).Source -ArgumentList @('"' + $taskDll + '"', '--zalopay-integration-host') -WorkingDirectory $taskWorkspace -WindowStyle Hidden -RedirectStandardOutput (Join-Path $logs 'api.log') -RedirectStandardError (Join-Path $logs 'api.err.log') -PassThru
    $record = @{ workspace = $taskWorkspace; apiPid = $api.Id; frontendPid = $null }
    $record | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $logs 'processes.json') -Encoding UTF8
    $ready = $false
    for ($attempt = 0; $attempt -lt 30; $attempt++) {
        if ($api.HasExited) { throw 'API startup failed. Read .tools/integration-sandbox/api.err.log.' }
        try { $ready = (Invoke-WebRequest 'http://127.0.0.1:5099/health' -UseBasicParsing -TimeoutSec 2).StatusCode -eq 200 } catch { }
        if ($ready) { break }
        Start-Sleep -Milliseconds 500
    }
    if (!$ready) { throw 'API is still starting; inspect its retained logs before restarting.' }
    $vite = Join-Path $taskWorkspace 'frontend\node_modules\vite\bin\vite.js'
    if (!(Test-Path -LiteralPath $vite)) { throw 'Run npm.cmd --prefix frontend ci before starting the frontend.' }
    $frontend = Start-Process -FilePath (Get-Command node.exe).Source -ArgumentList @('"' + $vite + '"', '--host', '127.0.0.1', '--port', '5177', '--strictPort') -WorkingDirectory (Join-Path $taskWorkspace 'frontend') -WindowStyle Hidden -RedirectStandardOutput (Join-Path $logs 'frontend.log') -RedirectStandardError (Join-Path $logs 'frontend.err.log') -PassThru
    $record.frontendPid = $frontend.Id
    $record | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $logs 'processes.json') -Encoding UTF8
    Write-Host 'SCRUM-23 wallet: http://localhost:5177/client/wallet'
    Write-Host 'Customer: 0900000023 / SandboxCustomer-2026! (private test database only)'
    Write-Host 'Expose only http://127.0.0.1:5100 through HTTPS; 5099 remains local.'
} finally { Pop-Location }
