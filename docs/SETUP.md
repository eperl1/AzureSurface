# Setup

## Recommended order

1. Build the Windows receiver first.
2. Install it on the Azure VM.
3. Copy the pairing token into the iPad app settings.
4. Build the iOS app.
5. Install the iOS app on the iPad.

## What you need

- an iPad Pro with a detachable keyboard/trackpad case
- a Windows 11 Azure VM
- a GitHub account
- a Tailscale network, or another private path between the iPad and the VM

## Repository layout

- `ios/`
- `windows/`
- `scripts/`
- `.github/workflows/`

## First workflow to run

Run `build-windows.yml` first.

That produces the receiver zip and lets you confirm the listener and token flow before you touch the iOS side.

