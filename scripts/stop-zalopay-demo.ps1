$ErrorActionPreference = 'Stop'
$stopRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$statePath = Join-Path $stopRoot '.tools/scrum6/zalopay-demo-state.json'
if (-not (Test-Path -LiteralPath $statePath)) { throw 'No S1 probe state was recorded.' }
$state = Get-Content -Encoding UTF8 -Raw -LiteralPath $statePath | ConvertFrom-Json
foreach ($target in @(
    @{ Id = [int]$state.tunnelProcessId; Name = 'cloudflared.exe'; Marker = 'http://127.0.0.1:5090' },
    @{ Id = [int]$state.probeProcessId; Name = 'dotnet.exe'; Marker = '--zalopay-probe-host' }
)) {
    $processInfo = Get-CimInstance Win32_Process -Filter ('ProcessId = ' + $target.Id) -ErrorAction SilentlyContinue
    if (-not $processInfo) { continue }
    if ($processInfo.Name -ne $target.Name -or $processInfo.CommandLine -notlike ('*' + $target.Marker + '*')) {
        throw ('Recorded process no longer matches the S1 probe: ' + $target.Id)
    }
    Stop-Process -Id $target.Id
    Write-Output ('Stopped S1 process: ' + $target.Id)
}
& (Get-Command dotnet.exe).Source user-secrets set 'Payments:ZaloPaySandbox:Enabled' 'false' --project (Join-Path $stopRoot 'Backend/src/CyberManagement.API') | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'Could not disable the stopped S1 sandbox configuration.' }
