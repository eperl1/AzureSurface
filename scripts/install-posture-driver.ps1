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

$existingDevice = Get-PnpDevice -PresentOnly:$false -FriendlyName 'Surface Posture Injection Driver' -ErrorAction SilentlyContinue
if ($existingDevice) {
    Write-Host 'SurfacePostureDriver already installed.'
}
else {
    $pnputil = Join-Path $env:SystemRoot 'System32\pnputil.exe'
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
