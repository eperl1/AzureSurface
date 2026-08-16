# Windows Posture

## What changed

The Windows receiver now tries to drive real convertible posture using Microsoft’s supported GPIO laptop/slate indicator path first:

- `GUID_GPIOBUTTONS_LAPTOPSLATE_INTERFACE`
- `GPIO Laptop or Slate Indicator Driver`
- `SM_CONVERTIBLESLATEMODE` / `WM_SETTINGCHANGE`

The receiver still keeps the existing touchscreen control and the `TABLET` / `LAPTOP` command flow.

## How it works

1. `SurfaceModeReceiver` receives `TABLET` or `LAPTOP`.
2. The new posture controller checks the current mode.
3. If the Windows GPIO laptop/slate interface is present, it writes to that interface.
4. If the interface is not present, it falls back to the existing registry-and-broadcast path so the app still behaves predictably.

## What you must already have

For the supported GPIO path to work, Windows must already expose the laptop/slate indicator device through ACPI and the inbox driver.

Microsoft’s docs are explicit that user-mode or kernel-mode injection can target the inbox driver, but the ACPI declaration for the laptop/slate indicator still has to exist.

If you are using an OEM or test-signing driver package that exposes the device, install that package with `pnputil /add-driver <path>\*.inf /install` before you rely on the posture backend.

If that package is unsigned, enable Windows test signing first and reboot before installing it.

## Install

1. Build the Windows receiver.
2. Copy the published files to the VM.
3. Run `scripts\install-windows-receiver.ps1` from an elevated PowerShell session.
4. Confirm the tray app starts and the firewall rule is present.

## Verify

Use these checks on the VM:

```powershell
Get-Process SurfaceModeReceiver -ErrorAction SilentlyContinue
reg query HKLM\SYSTEM\CurrentControlSet\Control\PriorityControl /v ConvertibleSlateMode
pnputil /enum-devices /class HIDClass | findstr /i "GPIO Laptop or Slate"
```

The last command should show the Microsoft GPIO indicator driver if the firmware path exists.

If you want to verify the receiver side, send a request with `TABLET` and `LAPTOP` and confirm the log records the posture backend it used.

## Roll back

Use:

```powershell
scripts\uninstall-windows-receiver.ps1 -RemoveData
```

That removes the startup shortcut, firewall rule, install directory, and optional local config.

## Important limitation

This repo can install the receiver, wire up the HTTP control flow, and try the supported Windows indicator interface.

It cannot synthesize the ACPI `PNP0C60` device from user mode. If the VM firmware does not already expose the convertible indicator path, Windows will stay on the fallback behavior and will not become a true convertible just because the receiver is installed.
