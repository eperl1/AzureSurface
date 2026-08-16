param(
    [string]$InstallPath = "$env:ProgramFiles\SurfaceModeReceiver",
    [int]$Port = 47889,
    [switch]$RemoveData
)

$shortcutPath = Join-Path ([Environment]::GetFolderPath('Startup')) 'SurfaceModeReceiver.lnk'
if (Test-Path $shortcutPath) {
    Remove-Item $shortcutPath -Force
}

$rule = Get-NetFirewallRule -DisplayName 'SurfaceModeReceiver' -ErrorAction SilentlyContinue
if ($rule) {
    Remove-NetFirewallRule -DisplayName 'SurfaceModeReceiver'
}

$processes = Get-CimInstance Win32_Process -Filter "Name = 'SurfaceModeReceiver.exe'" -ErrorAction SilentlyContinue
foreach ($process in $processes) {
    if ($process.ExecutablePath -and ($process.ExecutablePath -like "$InstallPath*")) {
        Stop-Process -Id $process.ProcessId -Force -ErrorAction SilentlyContinue
    }
}

if (Test-Path $InstallPath) {
    Remove-Item -Path $InstallPath -Recurse -Force
}

if ($RemoveData) {
    $configDir = Join-Path $env:ProgramData 'SurfaceModeReceiver'
    if (Test-Path $configDir) {
        Remove-Item -Path $configDir -Recurse -Force
    }
}

Write-Host "Removed SurfaceModeReceiver startup entry, firewall rule, and install directory."
if ($RemoveData) {
    Write-Host "Removed SurfaceModeReceiver data directory."
}
