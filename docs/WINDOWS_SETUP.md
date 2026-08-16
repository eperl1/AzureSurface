# Windows Setup

## What the receiver does

- listens on a private address or Tailscale interface when it can
- validates the bearer token
- accepts only `TABLET`, `LAPTOP`, and `PING`
- logs every request without logging the token
- opens or hides the Windows touch keyboard using best-effort software control
- uses the posture bus package when it is installed and falls back to the existing registry-and-broadcast behavior only when the driver is absent

## Install

1. Build `SurfaceModeReceiver.zip` and `SurfacePostureDriver.zip` from GitHub Actions.
2. Copy both zips to the Azure VM.
3. Extract them somewhere stable, such as `C:\Program Files\SurfaceModeReceiver` and `C:\Program Files\SurfacePostureDriver`.
4. Run `scripts\install-posture-driver.ps1` from an elevated PowerShell session.
5. Run `scripts\install-windows-receiver.ps1` from an elevated PowerShell session if you want the startup shortcut and firewall rule created automatically.
6. Use `docs/WINDOWS_POSTURE.md` for posture verification and rollback details.

## Startup

The install script:

- copies the published files
- creates a Startup-folder shortcut
- adds an inbound firewall rule for the default control port
- starts the receiver

If the posture driver is installed, the receiver will use it automatically when `TABLET` and `LAPTOP` commands arrive.

## Token

The receiver generates a strong random token on first run.

Use the tray icon menu item `Copy pairing token` to place the current token on the clipboard.
Paste that token into the iPad app settings.

## Firewall

If you prefer to manage the firewall manually, allow inbound TCP on the configured control port only from your private network or Tailscale interface.

## Posture verification

The supported convertible path is documented in [Windows Posture](WINDOWS_POSTURE.md). The short version is:

- confirm the Microsoft `GPIO Laptop or Slate Indicator Driver` path is available through the posture driver package
- confirm `GetSystemMetrics(SM_CONVERTIBLESLATEMODE)` changes after `TABLET` and `LAPTOP`
- send `TABLET` and `LAPTOP` requests to the receiver
- check the receiver log for the posture backend it used

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

