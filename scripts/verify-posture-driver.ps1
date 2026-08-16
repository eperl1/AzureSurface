param(
    [string]$PackagePath = (Join-Path $PSScriptRoot '..\artifacts\windows\SurfacePostureDriver\package'),
    [string]$ReceiverUrl = 'http://127.0.0.1:47889/api/mode',
    [string]$BearerToken = '',
    [switch]$RequireInstalledDevice
)

$ErrorActionPreference = 'Stop'

$packagePath = (Resolve-Path $PackagePath).Path
$inf = Join-Path $packagePath 'SurfacePostureDriver.inf'
$sys = Join-Path $packagePath 'SurfacePostureDriver.sys'
$cat = Join-Path $packagePath 'SurfacePostureDriver.cat'
$cer = Join-Path $packagePath 'SurfacePostureDriverTest.cer'

foreach ($file in @($inf, $sys, $cat, $cer)) {
    if (-not (Test-Path $file)) {
        throw "Missing package file: $file"
    }
}

$device = Get-PnpDevice -PresentOnly:$false -FriendlyName 'Surface Posture Injection Driver' -ErrorAction SilentlyContinue
if (-not $device) {
    if ($RequireInstalledDevice) {
        throw 'SurfacePostureDriver device is not present.'
    }
}

if ($device) {
    $relations = & pnputil /enum-devices /instanceid "$($device.InstanceId)" /relations
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to enumerate relations for $($device.InstanceId)."
    }

    $child = Get-PnpDevice -PresentOnly:$false | Where-Object {
        $_.InstanceId -match '^ROOT\\SURFACEPOSTUREINDICATOR\\' -or
        $_.FriendlyName -eq 'Surface Posture Indicator'
    } | Select-Object -First 1

    if (-not $child) {
        throw 'SurfacePostureDriver child device was not present after installation.'
    }

    if ($relations -notmatch [regex]::Escape($child.InstanceId) -and $relations -notmatch 'SurfacePostureIndicator') {
        throw "SurfacePostureDriver child $($child.InstanceId) was not shown in the parent relations."
    }

    $childDetails = & pnputil /enum-devices /instanceid "$($child.InstanceId)"
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to enumerate child device $($child.InstanceId)."
    }

    if ($childDetails -notmatch 'PNP0C60') {
        throw "SurfacePostureDriver child $($child.InstanceId) does not advertise compatible ID PNP0C60."
    }
}

if ($BearerToken) {
    $body = @{
        command = 'PING'
        source = 'verify-script'
        timestampUtc = [DateTimeOffset]::UtcNow.ToString('o')
        nonce = [Guid]::NewGuid().ToString()
    } | ConvertTo-Json -Depth 3

    $response = Invoke-RestMethod -Method Post -Uri $ReceiverUrl -Headers @{ Authorization = "Bearer $BearerToken" } -ContentType 'application/json' -Body $body
    if (-not $response.ok) {
        throw 'Receiver health check failed.'
    }
}

Write-Host 'SurfacePostureDriver package verified.'
if ($device) {
    Write-Host "Device: $($device.InstanceId)"
}
