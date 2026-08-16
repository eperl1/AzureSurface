#pragma once

#include <ntddk.h>
#include <wdf.h>

DEFINE_GUID(GUID_GPIOBUTTONS_LAPTOPSLATE_INTERFACE,
    0x317fc439, 0x3f77, 0x41c8, 0xb0, 0x9e, 0x08, 0xad, 0x63, 0x27, 0x2a, 0xa3);

typedef enum _SURFACE_POSTURE_MODE
{
    SurfacePostureModeTablet = 0,
    SurfacePostureModeLaptop = 1,
    SurfacePostureModeUnknown = 2
} SURFACE_POSTURE_MODE;

typedef struct _SURFACE_POSTURE_STATUS
{
    ULONG Size;
    ULONG CurrentMode;
    ULONG RequestedMode;
    ULONG Sequence;
    NTSTATUS LastAppliedStatus;
    ULONG Applied;
} SURFACE_POSTURE_STATUS, *PSURFACE_POSTURE_STATUS;

#define FILE_DEVICE_SURFACE_POSTURE 0x8333
#define IOCTL_SURFACE_POSTURE_GET_STATUS CTL_CODE(FILE_DEVICE_SURFACE_POSTURE, 0x801, METHOD_BUFFERED, FILE_ANY_ACCESS)

typedef struct _DEVICE_CONTEXT
{
    SURFACE_POSTURE_STATUS Status;
} DEVICE_CONTEXT, *PDEVICE_CONTEXT;

WDF_DECLARE_CONTEXT_TYPE_WITH_NAME(DEVICE_CONTEXT, SurfacePostureGetContext)

EVT_WDF_DRIVER_DEVICE_ADD SurfacePostureEvtDeviceAdd;
EVT_WDF_IO_QUEUE_IO_WRITE SurfacePostureEvtIoWrite;
EVT_WDF_IO_QUEUE_IO_DEVICE_CONTROL SurfacePostureEvtIoDeviceControl;
