param(
    [string]$PackagePath = (Join-Path $PSScriptRoot '..\artifacts\windows\SurfacePostureDriver\package'),
    [string]$DeviceHardwareId = 'Root\SurfacePostureDriver',
    [string]$DevConPath = 'C:\Program Files (x86)\Windows Kits\10\Tools\10.0.28000.0\x64\devcon.exe'
)

$ErrorActionPreference = 'Stop'

$packagePath = (Resolve-Path $PackagePath).Path
$certPath = Join-Path $packagePath 'SurfacePostureDriverTest.cer'
if (-not (Test-Path $certPath)) {
    throw "Certificate not found at $certPath"
}

Import-Certificate -FilePath $certPath -CertStoreLocation Cert:\LocalMachine\TrustedPublisher | Out-Null
Import-Certificate -FilePath $certPath -CertStoreLocation Cert:\LocalMachine\Root | Out-Null

$inf = Get-ChildItem -Path $packagePath -Filter 'SurfacePostureDriver.inf' | Select-Object -First 1
if (-not $inf) {
    throw 'SurfacePostureDriver.inf not found in the package.'
}

$pnputil = Join-Path $env:SystemRoot 'System32\pnputil.exe'
$driverEntries = & $pnputil /enum-drivers
$match = $driverEntries | Select-String -Pattern 'Original Name\s*:\s*SurfacePostureDriver\.inf' -Context 0,8 | Select-Object -First 1
if ($match) {
    $oemLine = ($match.Context.PostContext | Where-Object { $_ -match 'Published Name\s*:\s*(oem\d+\.inf)' } | Select-Object -First 1)
    if ($oemLine -and $oemLine.Matches.Count -gt 0) {
        $oem = $oemLine.Matches[0].Groups[1].Value
        & $pnputil /delete-driver $oem /uninstall /force
    }
}

$existingDevice = Get-PnpDevice -PresentOnly:$false -FriendlyName 'Surface Posture Injection Driver' -ErrorAction SilentlyContinue
if ($existingDevice) {
    if (Test-Path $DevConPath) {
        & $DevConPath remove "@$($existingDevice.InstanceId)"
    }
}

& $pnputil /add-driver $inf.FullName /install
if ($LASTEXITCODE -ne 0) {
    if (-not (Test-Path $DevConPath)) {
        throw "pnputil failed and devcon was not found at $DevConPath"
    }

    & $DevConPath install $inf.FullName $DeviceHardwareId
    if ($LASTEXITCODE -ne 0) {
        throw 'Failed to install the posture driver with both pnputil and devcon.'
    }
}

if (Test-Path $DevConPath) {
    & $DevConPath rescan
}

$device = Get-PnpDevice -PresentOnly:$false -FriendlyName 'Surface Posture Injection Driver' -ErrorAction SilentlyContinue
if (-not $device) {
    throw 'SurfacePostureDriver did not appear after installation.'
}

$markerDir = Join-Path $env:ProgramData 'SurfaceModeReceiver'
New-Item -ItemType Directory -Force -Path $markerDir | Out-Null
$marker = [pscustomobject]@{
    PackagePath = $packagePath
    InstalledAtUtc = [DateTime]::UtcNow.ToString('o')
    DeviceHardwareId = $DeviceHardwareId
}
$marker | ConvertTo-Json -Depth 3 | Set-Content -Path (Join-Path $markerDir 'SurfacePostureDriver.install.json') -Encoding UTF8

Write-Host 'SurfacePostureDriver installed.'
Write-Host "Package: $packagePath"
