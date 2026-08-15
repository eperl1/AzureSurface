# Troubleshooting

## The iPad says connection error

- Confirm the Windows receiver is running.
- Confirm the host or Tailscale address is correct.
- Confirm the port matches on both sides.
- Confirm the token matches exactly.

## Tablet/Laptop changes do not reach Windows

- Make sure the iPad app has a host, port, and token saved.
- Open the iPad app settings and use `Test Connection`.
- Check the Windows receiver log file.

## The touch keyboard does not open

- Windows 11 touch keyboard behavior depends on the current shell state and available keyboard components.
- This project uses best-effort software control only.
- The VM does not expose genuine Surface convertible hardware, so the app does not pretend to toggle real ACPI slate mode.

## The app does not look like it changed mode

- This project does not rely on undocumented registry hacks to force genuine hardware convertible mode.
- The Windows receiver only recreates the parts of the tablet experience that can be changed safely from software.

## Signing fails on GitHub Actions

- Re-check the Apple secrets.
- Make sure the provisioning profile matches the bundle identifier.
- Make sure the certificate password is correct.
- Make sure the Apple Developer Team ID is correct.

## Windows build fails

- Verify the solution path is `windows/SurfaceModeReceiver/SurfaceModeReceiver.sln`.
- Confirm you are using .NET 8.

