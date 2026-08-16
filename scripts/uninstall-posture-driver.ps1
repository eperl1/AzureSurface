param(
    [string]$PackagePath = (Join-Path $PSScriptRoot '..\artifacts\windows\SurfacePostureDriver\package'),
    [string]$DevConPath = 'C:\Program Files (x86)\Windows Kits\10\Tools\10.0.28000.0\x64\devcon.exe'
)

$ErrorActionPreference = 'Stop'

if ($PackagePath -and (Test-Path $PackagePath)) {
    $packagePath = (Resolve-Path $PackagePath).Path
}
else {
    $packagePath = $null
}
$markerPath = Join-Path $env:ProgramData 'SurfaceModeReceiver\SurfacePostureDriver.install.json'

$device = Get-PnpDevice -PresentOnly:$false -FriendlyName 'Surface Posture Injection Driver' -ErrorAction SilentlyContinue
if ($device -and (Test-Path $DevConPath)) {
    & $DevConPath remove "@$($device.InstanceId)"
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

if (Test-Path $markerPath) {
    Remove-Item $markerPath -Force
}

$certPath = if ($packagePath) { Join-Path $packagePath 'SurfacePostureDriverTest.cer' } else { $null }
if ($certPath -and (Test-Path $certPath)) {
    foreach ($store in @('Cert:\LocalMachine\TrustedPublisher', 'Cert:\LocalMachine\Root')) {
        Get-ChildItem $store | Where-Object { $_.Subject -like '*SurfacePostureDriver Test Certificate*' } | Remove-Item -Force -ErrorAction SilentlyContinue
    }
}

if (Test-Path $DevConPath) {
    & $DevConPath rescan
}

Write-Host 'SurfacePostureDriver removed where present.'
