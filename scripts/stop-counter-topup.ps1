$ErrorActionPreference = 'Stop'
$counterWorkspace = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$counterStatePath = Join-Path $counterWorkspace '.tools/counter-topup/state.json'
if (-not (Test-Path -LiteralPath $counterStatePath)) { Write-Host 'No counter demo state exists.'; exit 0 }
$counterState = Get-Content -Raw -LiteralPath $counterStatePath | ConvertFrom-Json
if ([IO.Path]::GetFullPath($counterState.workspace) -ne $counterWorkspace) { throw 'The state belongs to another workspace.' }
foreach ($counterTarget in @(
    @{ Id = $counterState.apiProcessId; Name = 'dotnet.exe'; Path = (Join-Path $counterWorkspace 'Backend/src/CyberManagement.API/bin/Debug/net10.0/CyberManagement.API.dll') },
    @{ Id = $counterState.frontendProcessId; Name = 'node.exe'; Path = (Join-Path $counterWorkspace 'frontend/node_modules/vite/bin/vite.js') }
)) {
    if (-not $counterTarget.Id) { continue }
    $counterProcess = Get-CimInstance Win32_Process -Filter "ProcessId = $([int]$counterTarget.Id)" -ErrorAction SilentlyContinue
    if (-not $counterProcess) { continue }
    if ($counterProcess.Name -ne $counterTarget.Name -or $counterProcess.CommandLine.IndexOf($counterTarget.Path, [StringComparison]::OrdinalIgnoreCase) -lt 0) {
        throw "Recorded PID $($counterTarget.Id) has changed owner; it was not stopped."
    }
    Stop-Process -Id $counterTarget.Id
}
Write-Host 'Counter demo stopped. SQL data and receipts were retained.'
