param([switch]$UseOfficialDemo)
$ErrorActionPreference = 'Stop'
if ($env:ZALOPAY_PROBE_URL -and $env:ZALOPAY_PROBE_URL -notin @('http://127.0.0.1:5090', 'http://127.0.0.1:5090/')) {
    throw 'ZALOPAY_PROBE_URL conflicts with the demo listener. Unset it or use http://127.0.0.1:5090.'
}
$demoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$demoLocal = Join-Path $demoRoot '.tools/scrum6'
New-Item -ItemType Directory -Force -Path $demoLocal | Out-Null
if (Get-NetTCPConnection -LocalPort 5090 -State Listen -ErrorAction SilentlyContinue) { throw 'Port 5090 is already in use; keep the existing listener or stop your previous S1 probe before retrying.' }
$tunnelCommand = Get-Command cloudflared.exe -ErrorAction SilentlyContinue
$tunnelExe = if ($tunnelCommand) { $tunnelCommand.Source } else { Join-Path $demoLocal 'cloudflared.exe' }
if (-not (Test-Path -LiteralPath $tunnelExe)) { throw 'Install official cloudflared or put cloudflared.exe in .tools/scrum6. See the S1-M-2 contract for the official download link.' }
$dotnet = (Get-Command dotnet.exe).Source
$artifactPath = Join-Path $demoLocal 'artifacts'
& $dotnet build (Join-Path $demoRoot 'Backend/tests/CyberManagement.API.Checks') --artifacts-path $artifactPath -p:NuGetAudit=false
if ($LASTEXITCODE -ne 0) { throw 'Probe build failed.' }
$demoOwnsEnabledFlag = $false
if ($UseOfficialDemo) {
    & $dotnet user-secrets set 'Payments:ZaloPaySandbox:Enabled' 'false' --project (Join-Path $demoRoot 'Backend/src/CyberManagement.API') | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Could not initialize the S1 probe configuration.' }
    $demoOwnsEnabledFlag = $true
}
$probeDll = Join-Path $artifactPath 'bin/CyberManagement.API.Checks/debug/CyberManagement.API.Checks.dll'
$probeProcess = $null
$tunnelProcess = $null
try {
    $probeProcess = Start-Process -FilePath $dotnet -ArgumentList @(('"' + $probeDll + '"'),'--zalopay-probe-host') `
        -WorkingDirectory $demoRoot -WindowStyle Hidden -PassThru `
        -RedirectStandardOutput (Join-Path $demoLocal 'callback-probe.log') -RedirectStandardError (Join-Path $demoLocal 'callback-probe-error.log')
    $probeReady = $false
    for ($attempt = 0; $attempt -lt 30; $attempt++) {
        if ($probeProcess.HasExited) { throw 'The S1 probe stopped. Inspect callback-probe-error.log.' }
        try { $ready = Invoke-RestMethod 'http://127.0.0.1:5090/api/payments/zalopay/callback' -TimeoutSec 2; if ($ready.probeOnly) { $probeReady = $true; break } } catch { }
        Start-Sleep -Milliseconds 400
    }
    if (-not $probeReady) { throw 'S1 probe startup timed out.' }
    # Only the isolated test listener is public; the normal API/SQL/admin UI stay separate.
    $tunnelProcess = Start-Process -FilePath $tunnelExe -ArgumentList @('tunnel','--url','http://127.0.0.1:5090','--protocol','http2','--no-autoupdate') `
        -WorkingDirectory $demoLocal -WindowStyle Hidden -PassThru `
        -RedirectStandardOutput (Join-Path $demoLocal 'tunnel.log') -RedirectStandardError (Join-Path $demoLocal 'tunnel-error.log')
    $publicBase = $null
    for ($attempt = 0; $attempt -lt 50; $attempt++) {
        if ($tunnelProcess.HasExited) { throw 'The tunnel stopped. Inspect tunnel-error.log.' }
        $tunnelLog = Get-Content -Raw -ErrorAction SilentlyContinue (Join-Path $demoLocal 'tunnel-error.log')
        if ($tunnelLog -match 'https://[a-z0-9-]+\.trycloudflare\.com') { $publicBase = $Matches[0]; break }
        Start-Sleep -Milliseconds 500
    }
    if (-not $publicBase) { throw 'No public HTTPS tunnel URL was assigned.' }
    $callbackUrl = $publicBase + '/api/payments/zalopay/callback'
    if ($UseOfficialDemo) {
        & (Join-Path $PSScriptRoot 'configure-zalopay-sandbox.ps1') -UseOfficialDemo -CallbackUrl $callbackUrl
    }
    $publicReady = $false
    $configurationDisabled = $false
    for ($attempt = 0; $attempt -lt 15; $attempt++) {
        try {
            $publicProbe = Invoke-RestMethod -Uri $callbackUrl -TimeoutSec 5
            if ($publicProbe.service -eq 'CyberManagement.ZaloPay.S1Probe' -and $publicProbe.probeOnly) {
                if ($publicProbe.configured -ne $true) { $configurationDisabled = $true; break }
                $publicReady = $true
                break
            }
        } catch { }
        Start-Sleep -Milliseconds 500
    }
    if ($configurationDisabled) { throw 'The sandbox is disabled. Set Enabled=true with valid server-side merchant configuration before retrying; HTTPS is not verified and an own-merchant callback URL was not changed.' }
    if (-not $publicReady) { throw 'The public callback probe was not ready with enabled, valid sandbox configuration. Check callback-probe-error.log; do not mark HTTPS verified.' }
    if (-not $UseOfficialDemo) {
        # Save an own merchant's new URL only after enabled configuration and public reachability pass.
        & $dotnet user-secrets set 'Payments:ZaloPaySandbox:CallbackUrl' $callbackUrl --project (Join-Path $demoRoot 'Backend/src/CyberManagement.API') | Out-Null
        if ($LASTEXITCODE -ne 0) { throw 'Could not save the assigned callback URL for the configured sandbox merchant.' }
    }
    $demoState = @{
        probeProcessId = $probeProcess.Id
        tunnelProcessId = $tunnelProcess.Id
        callbackUrl = $callbackUrl
        checkedAtVietnam = [DateTimeOffset]::UtcNow.ToOffset([TimeSpan]::FromHours(7)).ToString('o')
        probeOnly = $true
        businessStateChanged = $false
        configured = $publicProbe.configured
    }
    $demoState | ConvertTo-Json | Set-Content -Encoding UTF8 -LiteralPath (Join-Path $demoLocal 'zalopay-demo-state.json')
    Write-Output ('Verified public HTTPS S1 callback probe: ' + $callbackUrl)
    Write-Output 'Probe only: no provider acknowledgement, database writes, wallet credit or order settlement.'
} catch {
    $startFailure = $_
    if ($UseOfficialDemo -and $demoOwnsEnabledFlag) {
        try {
            & $dotnet user-secrets set 'Payments:ZaloPaySandbox:Enabled' 'false' --project (Join-Path $demoRoot 'Backend/src/CyberManagement.API') | Out-Null
            if ($LASTEXITCODE -ne 0) { [Console]::Error.WriteLine('Could not disable the failed official-demo configuration; disable it locally before retrying.') }
        } catch { [Console]::Error.WriteLine('Could not disable the failed official-demo configuration; disable it locally before retrying.') }
    }
    foreach ($ownedProcess in @($tunnelProcess, $probeProcess)) {
        try { if ($ownedProcess -and -not $ownedProcess.HasExited) { Stop-Process -Id $ownedProcess.Id -Force } }
        catch { [Console]::Error.WriteLine('Could not stop an owned S1 process; check it locally before retrying.') }
    }
    throw $startFailure
}
