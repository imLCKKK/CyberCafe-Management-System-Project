param(
    [switch]$UseOfficialDemo,
    [Parameter(Mandatory = $true)][string]$CallbackUrl,
    [string]$RedirectUrl = 'http://127.0.0.1:5173'
)
$ErrorActionPreference = 'Stop'
$setupRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
if (-not $UseOfficialDemo) { throw 'Use User Secrets directly for your own merchant. This helper only imports the published official trial merchant.' }
$callback = $null
$redirect = $null
if (-not [Uri]::TryCreate($CallbackUrl, [UriKind]::Absolute, [ref]$callback) -or
    $callback.Scheme -ne 'https' -or $callback.Port -ne 443 -or $callback.HostNameType -ne 'Dns' -or
    $callback.AbsolutePath -ne '/api/payments/zalopay/callback' -or $callback.IsLoopback -or
    $callback.UserInfo -or $callback.Query -or $callback.Fragment) { throw 'A public HTTPS callback URL is required.' }
if (-not [Uri]::TryCreate($RedirectUrl, [UriKind]::Absolute, [ref]$redirect) -or
    ($redirect.Scheme -ne 'https' -and -not ($redirect.Scheme -eq 'http' -and $redirect.IsLoopback)) -or
    $redirect.UserInfo -or $redirect.Query -or $redirect.Fragment) { throw 'Invalid redirect URL.' }

$officialPage = Invoke-WebRequest -UseBasicParsing -Uri 'https://developer.zalopay.vn/v1/start/' -TimeoutSec 20
$plain = [Net.WebUtility]::HtmlDecode([regex]::Replace($officialPage.Content, '<[^>]+>', ' '))
$plain = [regex]::Replace($plain, '\s+', ' ')
$trial = [regex]::Match($plain, '(?i)Appid\s+(\d+)\s+Key1\s+([A-Za-z0-9]{16,128})\s+Key2\s+([A-Za-z0-9]{16,128})')
if (-not $trial.Success) { throw 'The official page no longer exposes a matching trial merchant. Configure your own sandbox keys locally; do not guess them.' }
$appId = $trial.Groups[1].Value
$key1 = $trial.Groups[2].Value
$key2 = $trial.Groups[3].Value
$probeReference = [DateTimeOffset]::UtcNow.ToOffset([TimeSpan]::FromHours(7)).ToString('yyMMdd') + '_prep_' + [Guid]::NewGuid().ToString('N').Substring(0, 20)
$hmac = New-Object Security.Cryptography.HMACSHA256
try {
    $hmac.Key = [Text.Encoding]::UTF8.GetBytes($key1)
    $signature = [BitConverter]::ToString($hmac.ComputeHash([Text.Encoding]::UTF8.GetBytes("$appId|$probeReference|$key1"))).Replace('-', '').ToLowerInvariant()
} finally { $hmac.Dispose() }
# Query a fresh nonexistent reference; no create/payment request is made.
$queryBody = @{ app_id = $appId; app_trans_id = $probeReference; mac = $signature }
$query = $null
$badMacQuery = $null
$queryFailure = $null
$badMacFailure = $null
$queryTransportFailure = $false
$badMacTransportFailure = $false
$queryHttpStatus = $null
$badMacHttpStatus = $null
try {
    $query = Invoke-RestMethod -Uri 'https://sb-openapi.zalopay.vn/v2/query' -Method Post `
        -ContentType 'application/x-www-form-urlencoded' -Body $queryBody -TimeoutSec 12
} catch {
    $queryFailure = $_.Exception.GetType().Name
    $queryTransportFailure = [bool]($_.Exception -is [Net.WebException] -and -not $_.Exception.Response)
    if ($_.Exception.Response) { $queryHttpStatus = [int]$_.Exception.Response.StatusCode }
}
# Current Query Order status codes: -101 = ORDER_NOT_EXIST, -402 = ILLEGAL_APP/SIGNATURE_REQUEST.
# https://docs.zalopay.vn/vi/docs/developer-tools/knowledge-base/status-codes/
# A fresh nonexistent reference must not be treated as the distinct -54 expiry case.
$queryMatched = [bool]($query -and $query.return_code -eq 2 -and $query.sub_return_code -eq -101)
if ($queryMatched) {
    # Keep the same AppId/reference; changing only MAC makes this an authentication negative control.
    $queryBody.mac = '0' * 64
    try {
        $badMacQuery = Invoke-RestMethod -Uri 'https://sb-openapi.zalopay.vn/v2/query' -Method Post `
            -ContentType 'application/x-www-form-urlencoded' -Body $queryBody -TimeoutSec 12
    } catch {
        $badMacFailure = $_.Exception.GetType().Name
        $badMacTransportFailure = [bool]($_.Exception -is [Net.WebException] -and -not $_.Exception.Response)
        if ($_.Exception.Response) { $badMacHttpStatus = [int]$_.Exception.Response.StatusCode }
    }
}
$negativeDiffers = [bool]($query -and $badMacQuery -and $badMacQuery.sub_return_code -ne $query.sub_return_code)
$negativeMacRejected = [bool]($badMacQuery -and $badMacQuery.return_code -eq 2 -and $badMacQuery.sub_return_code -eq -402)
$localDirectory = Join-Path $setupRoot '.tools/scrum6'
New-Item -ItemType Directory -Force -Path $localDirectory | Out-Null
$status = @{
    checkedAtVietnam = [DateTimeOffset]::UtcNow.ToOffset([TimeSpan]::FromHours(7)).ToString('o')
    source = 'https://developer.zalopay.vn/v1/start/'
    appId = [int]$appId
    queryReturnCode = $query.return_code
    querySubReturnCode = $query.sub_return_code
    badMacReturnCode = $badMacQuery.return_code
    badMacSubReturnCode = $badMacQuery.sub_return_code
    queryHttpStatus = $queryHttpStatus
    badMacHttpStatus = $badMacHttpStatus
    providerTransactionCreated = $false
    queryNotFound = $queryMatched
    badMacRejectedDifferently = $negativeDiffers
    badMacSignatureRejected = $negativeMacRejected
    queryFailure = $queryFailure
    badMacFailure = $badMacFailure
    networkFailure = if ($queryTransportFailure) { $queryFailure } elseif ($badMacTransportFailure) { $badMacFailure } else { $null }
    providerCompatibilityVerified = ($queryMatched -and $negativeMacRejected)
}
$status | ConvertTo-Json | Set-Content -Encoding UTF8 -LiteralPath (Join-Path $localDirectory 'provider-configuration-check.json')
Write-Output ("Official trial AppId {0}: query={1}/{2}, bad-MAC query={3}/{4}; no payment created." -f $appId, $query.return_code, $query.sub_return_code, $badMacQuery.return_code, $badMacQuery.sub_return_code)
if (-not $queryMatched -and -not $queryTransportFailure) {
    throw 'The signed query did not return 2/-101 ORDER_NOT_EXIST. Stop and review the current sandbox merchant/request; no credential compatibility was verified.'
}
if ($queryMatched -and -not $negativeMacRejected -and -not $badMacTransportFailure) {
    throw 'The wrong-MAC query did not return 2/-402 ILLEGAL_APP/SIGNATURE_REQUEST. Stop and review the negative control; no credential compatibility was verified.'
}
if ($queryTransportFailure -or $badMacTransportFailure) {
    Write-Output 'Provider query controls could not complete because a transport request failed. Continue configuration/fixture checks only; credentials remain unverified and no payment was created.'
}
$values = [ordered]@{
    'Payments:ZaloPaySandbox:Enabled' = 'false'
    'Payments:ZaloPaySandbox:AppId' = $appId
    'Payments:ZaloPaySandbox:Key1' = $key1
    'Payments:ZaloPaySandbox:Key2' = $key2
    'Payments:ZaloPaySandbox:CallbackUrl' = $CallbackUrl
    'Payments:ZaloPaySandbox:RedirectUrl' = $RedirectUrl
}
$dotnet = (Get-Command dotnet.exe).Source
$project = Join-Path $setupRoot 'Backend/src/CyberManagement.API'
foreach ($entry in $values.GetEnumerator()) {
    & $dotnet user-secrets set $entry.Key $entry.Value --project $project | Out-Null
    if ($LASTEXITCODE -ne 0) { throw ('Could not save User Secrets field: ' + $entry.Key) }
}
$enableAttempted = $false
try {
    $enableAttempted = $true
    & $dotnet user-secrets set 'Payments:ZaloPaySandbox:Enabled' 'true' --project $project | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Could not enable validated sandbox configuration.' }
    Write-Output 'Saved schema-valid sandbox configuration to API User Secrets. Enabled is a configuration flag, not proof that the provider accepted these credentials.'
    $ready = $false
    for ($attempt = 0; $attempt -lt 15; $attempt++) {
        try { $probe = Invoke-RestMethod -Uri $CallbackUrl -TimeoutSec 5; if ($probe.probeOnly -and $probe.configured) { $ready = $true; break } } catch { }
        Start-Sleep -Milliseconds 400
    }
    if (-not $ready) { throw 'Configured public S1 probe was not ready.' }
    $fixtureCount = 0
    foreach ($purpose in @('TopUp', 'Order')) {
        $data = [ordered]@{ app_id = [int]$appId; app_trans_id = $probeReference; amount = 35000; embed_data = ('{"purpose":"' + $purpose + '","fixtureOnly":true}') } | ConvertTo-Json -Compress
        $callbackHmac = New-Object Security.Cryptography.HMACSHA256
        try {
            $callbackHmac.Key = [Text.Encoding]::UTF8.GetBytes($key2)
            $mac = [BitConverter]::ToString($callbackHmac.ComputeHash([Text.Encoding]::UTF8.GetBytes($data))).Replace('-', '').ToLowerInvariant()
        } finally { $callbackHmac.Dispose() }
        $payload = @{ data = $data; mac = $mac; type = 1 } | ConvertTo-Json -Compress
        $result = Invoke-RestMethod -Uri $CallbackUrl -Method Post -ContentType 'application/json' -Body $payload -TimeoutSec 8
        if (-not $result.probeOnly -or -not $result.signatureValid -or $result.businessStateChanged -ne $false -or $result.providerAcknowledged -ne $false) { throw 'Unexpected S1 fixture response.' }
        $badPayload = @{ data = $data; mac = ('0' * 64); type = 1 } | ConvertTo-Json -Compress
        $badStatus = 0
        try { Invoke-RestMethod -Uri $CallbackUrl -Method Post -ContentType 'application/json' -Body $badPayload -TimeoutSec 8 | Out-Null }
        catch { if ($_.Exception.Response) { $badStatus = [int]$_.Exception.Response.StatusCode } }
        if ($badStatus -ne 400) { throw 'The S1 probe did not reject the invalid signature.' }
        $fixtureCount += 2
    }
    @{ callbackUrl = $CallbackUrl; fixtureChecksPassed = $fixtureCount; probeOnly = $true; providerAcknowledged = $false; businessStateChanged = $false } | ConvertTo-Json | Set-Content -Encoding UTF8 -LiteralPath (Join-Path $localDirectory 'https-fixture-check.json')
    Write-Output ("Public HTTPS signature fixtures: {0} passed; TopUp/Order are fixture metadata only, no payment settlement." -f $fixtureCount)
} catch {
    $setupFailure = $_
    if ($enableAttempted) {
        try {
            & $dotnet user-secrets set 'Payments:ZaloPaySandbox:Enabled' 'false' --project $project | Out-Null
            if ($LASTEXITCODE -ne 0) { [Console]::Error.WriteLine('Could not disable the failed official-demo configuration; disable it locally before retrying.') }
        } catch { [Console]::Error.WriteLine('Could not disable the failed official-demo configuration; disable it locally before retrying.') }
    }
    throw $setupFailure
}
