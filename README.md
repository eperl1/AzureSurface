# AzureSurface-iPad

This project turns an iPad Pro with a detachable keyboard/trackpad case into a remote Surface-style experience for a Windows 11 Azure VM.

When the iPad keyboard attaches, the app sends `LAPTOP` to the Windows receiver.
When the keyboard detaches, it sends `TABLET`.

The repo contains:

- an iPad/iOS overlay for the FreeRDP iOS client
- a Windows 11 receiver written in C#/.NET
- a root-enumerated Windows posture driver package for VM installs
- GitHub Actions workflows for unsigned iOS builds, signed iOS builds, and Windows builds
- setup and signing documentation
- Windows posture install, verify, and rollback guidance

## What this project does

- Detects the physical keyboard attach/detach event on iPadOS using Apple `GameController` keyboard notifications
- Sends only predefined commands: `TABLET`, `LAPTOP`, and `PING`
- Stores the iPad auth token in Keychain
- Runs a Windows receiver that:
  - authenticates the request with a token
  - rejects stale or duplicate requests
  - logs mode transitions
  - shows/hides the Windows touch keyboard
  - talks to the posture driver when it is installed and falls back only when it is absent
  - keeps duplicate tablet/laptop events idempotent

## Architecture

- `ios/overlay/client/iOS/SurfaceMode/`
  - tiny Objective-C overlay that gets copied into the upstream FreeRDP iOS source tree during the build
  - registers keyboard notifications
  - adds a Surface Mode settings section
  - sends HTTP requests to the Windows receiver
- `windows/SurfaceModeReceiver/`
  - .NET 8 Windows tray app
  - authenticated HTTP listener
  - state machine and tests
- `windows/SurfacePostureDriver/`
  - root-enumerated KMDF posture driver package
  - exposes the Microsoft laptop/slate indicator interface GUID
  - accepts posture writes and reports applied state back to the receiver
- `.github/workflows/`
  - unsigned iOS build on GitHub-hosted macOS
  - signed iOS build on GitHub-hosted macOS
  - Windows receiver and posture-driver builds on GitHub-hosted Windows

## Current state

- Windows receiver source and tests are implemented in this repo.
- The iPad changes are implemented as an overlay on top of upstream FreeRDP 3.30.0.
- I could not run a local Windows or iOS build in this environment because the local shell does not have the .NET SDK or Xcode toolchain installed. The GitHub Actions workflows are the intended build path.
- The posture driver package is built separately and installed on the Windows VM before the receiver uses it.

## How to build

1. Run `.github/workflows/build-windows.yml` first to build the receiver and download `SurfaceModeReceiver.zip`.
2. Run `.github/workflows/build-ios.yml` to build an unsigned simulator app bundle.
3. Run `.github/workflows/build-ios-signed.yml` only after you have the Apple signing secrets configured.

## Docs

- [Setup](docs/SETUP.md)
- [Apple Signing](docs/APPLE_SIGNING.md)
- [Windows Setup](docs/WINDOWS_SETUP.md)
- [Windows Posture](docs/WINDOWS_POSTURE.md)
- [Troubleshooting](docs/TROUBLESHOOTING.md)

