# Windows Setup

## What the receiver does

- listens on a private address or Tailscale interface when it can
- validates the bearer token
- accepts only `TABLET`, `LAPTOP`, and `PING`
- logs every request without logging the token
- opens or hides the Windows touch keyboard using best-effort software control

## Install

1. Build `SurfaceModeReceiver.zip` from GitHub Actions.
2. Copy the zip to the Azure VM.
3. Extract it somewhere stable, such as `C:\Program Files\SurfaceModeReceiver`.
4. Run `scripts\install-windows-receiver.ps1` from an elevated PowerShell session if you want the startup shortcut and firewall rule created automatically.

## Startup

The install script:

- copies the published files
- creates a Startup-folder shortcut
- adds an inbound firewall rule for the default control port
- starts the receiver

## Token

The receiver generates a strong random token on first run.

Use the tray icon menu item `Copy pairing token` to place the current token on the clipboard.
Paste that token into the iPad app settings.

## Firewall

If you prefer to manage the firewall manually, allow inbound TCP on the configured control port only from your private network or Tailscale interface.

## Logs

Logs are written under:

`C:\ProgramData\SurfaceModeReceiver\logs\receiver.log`

The log format includes:

- timestamp
- source
- event
- old mode
- new mode
- success or failure

