# Windows Posture

## What changed

The Windows receiver now tries to drive real convertible posture using Microsoft’s supported GPIO laptop/slate indicator path through a VM-installable posture driver package:

- `GUID_GPIOBUTTONS_LAPTOPSLATE_INTERFACE`
- `GPIO Laptop or Slate Indicator Driver`
- `SM_CONVERTIBLESLATEMODE` / `WM_SETTINGCHANGE`

The receiver still keeps the existing touchscreen control and the `TABLET` / `LAPTOP` command flow.

## How it works

1. `SurfaceModeReceiver` receives `TABLET` or `LAPTOP`.
2. The new posture controller checks the current mode.
3. If the posture driver is installed, it writes to the `GUID_GPIOBUTTONS_LAPTOPSLATE_INTERFACE` path exposed by that driver and verifies that the driver reports the new state back.
4. If the posture driver is not present, it falls back to the existing registry-and-broadcast path so the app still behaves predictably.

## What you must already have

Install the posture driver package first. It root-enumerates a supported laptop/slate indicator interface that the receiver can talk to even when the VM does not expose OEM convertible firmware.

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
reg query HKLM\SYSTEM\CurrentControlSet\Control\PriorityControl /v ConvertibleSlateMode
pnputil /enum-devices /class System | findstr /i "Surface Posture Injection Driver"
```

The last command should show the posture driver device once it is installed.

If you want to verify the receiver side, send a request with `TABLET` and `LAPTOP` and confirm the log records `posture_driver` when the driver path was used.

## Roll back

Use:

```powershell
scripts\uninstall-windows-receiver.ps1 -RemoveData
```

That removes the startup shortcut, firewall rule, install directory, and optional local config.

## Important limitation

This repo can install the receiver, install the posture driver package, wire up the HTTP control flow, and try the supported Windows indicator interface.

The remaining limitation is that Windows shell behavior still depends on the OS honoring the indicator state the driver exposes. If the VM does not react to the driver-backed indicator, the receiver will report that failure instead of pretending the posture changed.
