# Implementation Notes

## iPad / FreeRDP overlay files

These files are copied into the upstream FreeRDP iOS tree during the build:

- `ios/overlay/client/iOS/SurfaceMode/SurfaceModeBootstrap.h`
- `ios/overlay/client/iOS/SurfaceMode/SurfaceModeBootstrap.m`
- `ios/overlay/client/iOS/SurfaceMode/SurfaceModeControlClient.h`
- `ios/overlay/client/iOS/SurfaceMode/SurfaceModeControlClient.m`
- `ios/overlay/client/iOS/SurfaceMode/SurfaceModeSettings.h`
- `ios/overlay/client/iOS/SurfaceMode/SurfaceModeSettings.m`
- `ios/overlay/client/iOS/SurfaceMode/SurfaceModeStatusCenter.h`
- `ios/overlay/client/iOS/SurfaceMode/SurfaceModeStatusCenter.m`
- `ios/overlay/client/iOS/SurfaceMode/AppSettingsController+SurfaceMode.h`
- `ios/overlay/client/iOS/SurfaceMode/AppSettingsController+SurfaceMode.m`

## Build helpers

- `ios/scripts/apply-freerdp-overlay.sh`
- `ios/scripts/build-freerdp-ios.sh`
- `scripts/build-windows.ps1`
- `scripts/build-posture-driver.ps1`
- `scripts/install-posture-driver.ps1`
- `scripts/uninstall-posture-driver.ps1`
- `scripts/verify-posture-driver.ps1`
- `scripts/install-windows-receiver.ps1`
- `scripts/uninstall-windows-receiver.ps1`
- `docs/WINDOWS_POSTURE.md`

## Windows receiver

- `windows/SurfaceModeReceiver/src/SurfaceModeReceiver/Program.cs`
- `windows/SurfaceModeReceiver/src/SurfaceModeReceiver/TrayApplicationContext.cs`
- `windows/SurfaceModeReceiver/src/SurfaceModeReceiver/ReceiverConfig.cs`
- `windows/SurfaceModeReceiver/src/SurfaceModeReceiver/ModeCommand.cs`
- `windows/SurfaceModeReceiver/src/SurfaceModeReceiver/ModeStateMachine.cs`
- `windows/SurfaceModeReceiver/src/SurfaceModeReceiver/ReceiverLog.cs`
- `windows/SurfaceModeReceiver/src/SurfaceModeReceiver/NetworkBindingResolver.cs`
- `windows/SurfaceModeReceiver/src/SurfaceModeReceiver/TouchKeyboardController.cs`
- `windows/SurfaceModeReceiver/src/SurfaceModeReceiver/SurfaceModeServer.cs`
- `windows/SurfaceModeReceiver/src/SurfaceModeReceiver/SurfacePostureController.cs`
- `windows/SurfaceModeReceiver/src/SurfaceModeReceiver/SurfacePostureDriverClient.cs`
- `windows/SurfaceModeReceiver/src/SurfaceModeReceiver/Properties/AssemblyInfo.cs`

## Windows posture driver

- `windows/SurfacePostureDriver/SurfacePostureDriver.sln`
- `windows/SurfacePostureDriver/SurfacePostureDriver.vcxproj`
- `windows/SurfacePostureDriver/SurfacePostureDriver.inf`
- `windows/SurfacePostureDriver/src/SurfacePostureDriver.c`
- `windows/SurfacePostureDriver/src/SurfacePostureDriver.h`

This package is a root-enumerated KMDF bus driver. It publishes a child device with a `PNP0C60` compatible ID so Microsoft's inbox GPIO laptop/slate indicator driver can bind to it in a VM that does not have OEM convertible firmware. The receiver then writes to `GUID_GPIOBUTTONS_LAPTOPSLATE_INTERFACE` on that inbox path and verifies the change by reading `SM_CONVERTIBLESLATEMODE` from Windows.

## Tests

- `windows/SurfaceModeReceiver/tests/SurfaceModeReceiver.Tests/ModeParserTests.cs`
- `windows/SurfaceModeReceiver/tests/SurfaceModeReceiver.Tests/ModeStateMachineTests.cs`
- `windows/SurfaceModeReceiver/tests/SurfaceModeReceiver.Tests/ModeRequestTests.cs`

## GitHub Actions

- `.github/workflows/build-windows.yml`
- `.github/workflows/build-ios.yml`
- `.github/workflows/build-ios-signed.yml`

