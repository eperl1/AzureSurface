# Windows Posture

## What changed

The Windows receiver now drives the supported Microsoft laptop/slate posture path instead of a private fake state:

- `windows/SurfacePostureDriver` is a root-enumerated KMDF bus package
- the bus publishes a child device compatible with `PNP0C60`
- that child causes the Microsoft inbox `GPIO Laptop or Slate Indicator Driver` to load
- the receiver writes to `GUID_GPIOBUTTONS_LAPTOPSLATE_INTERFACE`
- success is verified from Windows itself with `SM_CONVERTIBLESLATEMODE`

The receiver still keeps the existing touchscreen control and the `TABLET` / `LAPTOP` command flow.

## How it works

1. `SurfaceModeReceiver` receives `TABLET` or `LAPTOP`.
2. The posture controller asks the driver client to write the new state through the inbox indicator interface.
3. The driver client waits for `GetSystemMetrics(SM_CONVERTIBLESLATEMODE)` to match the requested mode.
4. If the indicator path is missing, the receiver falls back to the existing registry-and-broadcast behavior.
5. If the indicator path is present but Windows does not change the metric, the receiver reports failure instead of claiming success.

## What you must already have

Install the posture driver package first. It is VM-installable and does not require OEM firmware, but it does rely on Microsoft’s inbox GPIO laptop/slate indicator driver loading for the child device.

If the package is unsigned, enable Windows test signing first and reboot before installing it.

## Install

1. Build the Windows receiver.
2. Build the posture driver package.
3. Copy both artifacts to the VM.
4. Run `scripts\install-posture-driver.ps1` from an elevated PowerShell session.
5. Run `scripts\install-windows-receiver.ps1` from an elevated PowerShell session.
6. Confirm the tray app starts and the firewall rule is present.

## Verify

Use these checks on the VM:

```powershell
Get-PnpDevice -FriendlyName 'Surface Posture Injection Driver'
Get-Process SurfaceModeReceiver -ErrorAction SilentlyContinue
pnputil /enum-devices /class System | findstr /i "Surface Posture Injection Driver"
[System.Windows.Forms.SystemInformation]::ConvertibleSlateMode
```

The device list should show the bus package, and the metric should change after sending `TABLET` or `LAPTOP`.

If you want to verify the receiver side, send a request with `TABLET` and `LAPTOP` and confirm the log records `posture_driver` when the inbox driver path was used.

## Roll back

Use:

```powershell
scripts\uninstall-windows-receiver.ps1 -RemoveData
```

That removes the startup shortcut, firewall rule, install directory, and optional local config.

## Important limitation

Windows 11 build 10.0.26100.33158 may still show shell UI quirks even after the posture metric changes correctly. The receiver treats the metric transition as the real success condition and reports failures when Windows does not honor it.
