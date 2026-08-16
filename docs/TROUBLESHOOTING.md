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
- If the log says `posture_registry` instead of `posture_driver`, the VM-installable posture path is missing or not exposing the inbox interface the receiver expects.
- Use the checks in [Windows Posture](WINDOWS_POSTURE.md) to confirm whether the `PNP0C60`-compatible child exists and whether `SM_CONVERTIBLESLATEMODE` changes.

## The touch keyboard does not open

- Windows 11 touch keyboard behavior depends on the current shell state and available keyboard components.
- This project uses best-effort software control only.
- The VM does not expose genuine Surface convertible hardware, so the app relies on the posture driver package instead of pretending to toggle physical firmware state.

## The app does not look like it changed mode

- The Windows receiver now tries the posture driver path first.
- If the posture driver is missing, the app falls back to the registry-and-broadcast behavior and cannot become a true convertible on its own.
- On Windows 11 build 10.0.26100.33158, the shell may still not visually re-layout even when the metric changes correctly. Treat the metric transition as the acceptance check.

## Signing fails on GitHub Actions

- Re-check the Apple secrets.
- Make sure the provisioning profile matches the bundle identifier.
- Make sure the certificate password is correct.
- Make sure the Apple Developer Team ID is correct.

## Windows build fails

- Verify the solution path is `windows/SurfaceModeReceiver/SurfaceModeReceiver.sln`.
- Confirm you are using .NET 8.

