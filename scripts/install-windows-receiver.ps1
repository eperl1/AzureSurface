param(
    [string]$SourcePath = (Join-Path $PSScriptRoot '..\windows\SurfaceModeReceiver\bin\Release\publish'),
    [string]$InstallPath = "$env:ProgramFiles\SurfaceModeReceiver",
    [int]$Port = 47889
)

$SourcePath = (Resolve-Path $SourcePath).Path
New-Item -ItemType Directory -Force -Path $InstallPath | Out-Null
Copy-Item -Path (Join-Path $SourcePath '*') -Destination $InstallPath -Recurse -Force

$exe = Join-Path $InstallPath 'SurfaceModeReceiver.exe'
if (-not (Test-Path $exe)) {
    throw "SurfaceModeReceiver.exe not found at $exe"
}

$startupFolder = [Environment]::GetFolderPath('Startup')
$shortcutPath = Join-Path $startupFolder 'SurfaceModeReceiver.lnk'
$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut($shortcutPath)
$shortcut.TargetPath = $exe
$shortcut.WorkingDirectory = $InstallPath
$shortcut.WindowStyle = 1
$shortcut.Description = 'SurfaceModeReceiver'
$shortcut.Save()

if (-not (Get-NetFirewallRule -DisplayName 'SurfaceModeReceiver' -ErrorAction SilentlyContinue)) {
    New-NetFirewallRule -DisplayName 'SurfaceModeReceiver' -Direction Inbound -Action Allow -Protocol TCP -LocalPort $Port | Out-Null
}

Start-Process -FilePath $exe -WorkingDirectory $InstallPath

Write-Host "Installed SurfaceModeReceiver to $InstallPath"
Write-Host "Startup shortcut created at $shortcutPath"
Write-Host "Firewall rule added for TCP $Port"
Write-Host "Open the tray icon and choose 'Copy pairing token' to read the Keychain-protected token."
