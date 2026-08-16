#define INITGUID
#include "SurfacePostureDriver.h"

static VOID SurfacePostureApplyMode(_In_ PDEVICE_CONTEXT Context, _In_ UCHAR RequestedMode)
{
    Context->Status.RequestedMode = RequestedMode;
    Context->Status.CurrentMode = RequestedMode;
    Context->Status.Applied = 1;
    Context->Status.LastAppliedStatus = STATUS_SUCCESS;
    Context->Status.Sequence += 1;
}

static VOID SurfacePostureInitializeStatus(_Out_ PSURFACE_POSTURE_STATUS Status)
{
    RtlZeroMemory(Status, sizeof(*Status));
    Status->Size = sizeof(*Status);
    Status->CurrentMode = SurfacePostureModeUnknown;
    Status->RequestedMode = SurfacePostureModeUnknown;
    Status->LastAppliedStatus = STATUS_UNSUCCESSFUL;
    Status->Applied = 0;
}

static NTSTATUS SurfacePostureCreateQueue(_In_ WDFDEVICE Device)
{
    WDF_IO_QUEUE_CONFIG queueConfig;
    WDF_IO_QUEUE_CONFIG_INIT_DEFAULT_QUEUE(&queueConfig, WdfIoQueueDispatchSequential);
    queueConfig.EvtIoWrite = SurfacePostureEvtIoWrite;
    queueConfig.EvtIoDeviceControl = SurfacePostureEvtIoDeviceControl;

    WDF_OBJECT_ATTRIBUTES queueAttributes;
    WDF_OBJECT_ATTRIBUTES_INIT(&queueAttributes);

    WDFQUEUE queue;
    return WdfIoQueueCreate(Device, &queueConfig, &queueAttributes, &queue);
}

NTSTATUS SurfacePostureEvtDeviceAdd(_In_ WDFDRIVER Driver, _Inout_ PWDFDEVICE_INIT DeviceInit)
{
    UNREFERENCED_PARAMETER(Driver);

    WDF_OBJECT_ATTRIBUTES deviceAttributes;
    WDF_OBJECT_ATTRIBUTES_INIT_CONTEXT_TYPE(&deviceAttributes, DEVICE_CONTEXT);

    WdfDeviceInitSetDeviceType(DeviceInit, FILE_DEVICE_UNKNOWN);
    WdfDeviceInitSetCharacteristics(DeviceInit, FILE_DEVICE_SECURE_OPEN, FALSE);

    WDFDEVICE device;
    NTSTATUS status = WdfDeviceCreate(&DeviceInit, &deviceAttributes, &device);
    if (!NT_SUCCESS(status))
    {
        return status;
    }

    PDEVICE_CONTEXT context = SurfacePostureGetContext(device);
    SurfacePostureInitializeStatus(&context->Status);

    status = WdfDeviceCreateDeviceInterface(device, &GUID_GPIOBUTTONS_LAPTOPSLATE_INTERFACE, NULL);
    if (!NT_SUCCESS(status))
    {
        return status;
    }

    status = SurfacePostureCreateQueue(device);
    if (!NT_SUCCESS(status))
    {
        return status;
    }

    KdPrintEx((DPFLTR_IHVDRIVER_ID, DPFLTR_INFO_LEVEL, "SurfacePostureDriver: device created\n"));
    return STATUS_SUCCESS;
}

VOID SurfacePostureEvtIoWrite(_In_ WDFQUEUE Queue, _In_ WDFREQUEST Request, _In_ size_t Length)
{
    UNREFERENCED_PARAMETER(Length);

    WDFDEVICE device = WdfIoQueueGetDevice(Queue);
    PDEVICE_CONTEXT context = SurfacePostureGetContext(device);

    size_t inputLength = 0;
    PUCHAR inputBuffer = NULL;
    NTSTATUS status = WdfRequestRetrieveInputBuffer(Request, 1, (PVOID*)&inputBuffer, &inputLength);
    if (!NT_SUCCESS(status))
    {
        context->Status.LastAppliedStatus = status;
        WdfRequestComplete(Request, status);
        return;
    }

    UCHAR requestedMode = inputBuffer[0];
    if (requestedMode != SurfacePostureModeTablet && requestedMode != SurfacePostureModeLaptop)
    {
        context->Status.RequestedMode = requestedMode;
        context->Status.LastAppliedStatus = STATUS_INVALID_PARAMETER;
        context->Status.Applied = 0;
        WdfRequestComplete(Request, STATUS_INVALID_PARAMETER);
        return;
    }

    SurfacePostureApplyMode(context, requestedMode);
    KdPrintEx((DPFLTR_IHVDRIVER_ID, DPFLTR_INFO_LEVEL,
        "SurfacePostureDriver: requested=%u current=%u sequence=%lu\n",
        requestedMode,
        context->Status.CurrentMode,
        context->Status.Sequence));

    WdfRequestCompleteWithInformation(Request, STATUS_SUCCESS, inputLength);
}

VOID SurfacePostureEvtIoDeviceControl(_In_ WDFQUEUE Queue, _In_ WDFREQUEST Request, _In_ size_t OutputBufferLength, _In_ size_t InputBufferLength, _In_ ULONG IoControlCode)
{
    UNREFERENCED_PARAMETER(InputBufferLength);

    WDFDEVICE device = WdfIoQueueGetDevice(Queue);
    PDEVICE_CONTEXT context = SurfacePostureGetContext(device);

    if (IoControlCode != IOCTL_SURFACE_POSTURE_GET_STATUS)
    {
        WdfRequestComplete(Request, STATUS_INVALID_DEVICE_REQUEST);
        return;
    }

    PSURFACE_POSTURE_STATUS output = NULL;
    NTSTATUS status = WdfRequestRetrieveOutputBuffer(Request, sizeof(SURFACE_POSTURE_STATUS), (PVOID*)&output, NULL);
    if (!NT_SUCCESS(status))
    {
        context->Status.LastAppliedStatus = status;
        WdfRequestComplete(Request, status);
        return;
    }

    if (OutputBufferLength < sizeof(SURFACE_POSTURE_STATUS))
    {
        context->Status.LastAppliedStatus = STATUS_BUFFER_TOO_SMALL;
        WdfRequestComplete(Request, STATUS_BUFFER_TOO_SMALL);
        return;
    }

    *output = context->Status;
    WdfRequestCompleteWithInformation(Request, STATUS_SUCCESS, sizeof(SURFACE_POSTURE_STATUS));
}

VOID SurfacePostureDriverUnload(_In_ WDFDRIVER Driver)
{
    UNREFERENCED_PARAMETER(Driver);
    KdPrintEx((DPFLTR_IHVDRIVER_ID, DPFLTR_INFO_LEVEL, "SurfacePostureDriver: unload\n"));
}

NTSTATUS DriverEntry(_In_ PDRIVER_OBJECT DriverObject, _In_ PUNICODE_STRING RegistryPath)
{
    WDF_DRIVER_CONFIG config;
    WDF_DRIVER_CONFIG_INIT(&config, SurfacePostureEvtDeviceAdd);
    config.EvtDriverUnload = SurfacePostureDriverUnload;

    WDF_OBJECT_ATTRIBUTES attributes;
    WDF_OBJECT_ATTRIBUTES_INIT(&attributes);

    return WdfDriverCreate(DriverObject, RegistryPath, &attributes, &config, WDF_NO_HANDLE);
}
