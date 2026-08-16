#define INITGUID
#include "SurfacePostureDriver.h"

#define SURFACE_POSTURE_CHILD_INDEX 0
#define SURFACE_POSTURE_CHILD_DEVICE_ID L"Root\\SurfacePostureIndicator"
#define SURFACE_POSTURE_CHILD_HARDWARE_ID L"Root\\SurfacePostureIndicator"
#define SURFACE_POSTURE_CHILD_COMPATIBLE_ID L"PNP0C60"
#define SURFACE_POSTURE_CHILD_INSTANCE_ID L"0"
#define SURFACE_POSTURE_CHILD_DEVICE_TEXT L"Surface Posture Indicator"
#define SURFACE_POSTURE_CHILD_LOCATION_TEXT L"Surface Posture Driver"
#define SURFACE_POSTURE_LOCALE_ID 0x0409

static VOID SurfacePosturePublishChild(_In_ WDFDEVICE Device)
{
    WDFCHILDLIST childList = WdfFdoGetDefaultChildList(Device);
    SURFACE_POSTURE_CHILD_IDENTIFICATION_DESCRIPTION childDescription;

    WDF_CHILD_IDENTIFICATION_DESCRIPTION_HEADER_INIT(
        &childDescription.Header,
        sizeof(childDescription));
    childDescription.ChildIndex = SURFACE_POSTURE_CHILD_INDEX;

    WdfChildListBeginScan(childList);
    (VOID)WdfChildListAddOrUpdateChildDescriptionAsPresent(childList, &childDescription.Header, NULL);
    WdfChildListEndScan(childList);
}

static NTSTATUS SurfacePostureInitializeChildInit(_In_ PWDFDEVICE_INIT ChildInit)
{
    UNICODE_STRING deviceId;
    UNICODE_STRING hardwareId;
    UNICODE_STRING compatibleId;
    UNICODE_STRING instanceId;
    UNICODE_STRING deviceText;
    UNICODE_STRING locationText;
    WDF_PDO_EVENT_CALLBACKS pdoCallbacks;

    RtlInitUnicodeString(&deviceId, SURFACE_POSTURE_CHILD_DEVICE_ID);
    RtlInitUnicodeString(&hardwareId, SURFACE_POSTURE_CHILD_HARDWARE_ID);
    RtlInitUnicodeString(&compatibleId, SURFACE_POSTURE_CHILD_COMPATIBLE_ID);
    RtlInitUnicodeString(&instanceId, SURFACE_POSTURE_CHILD_INSTANCE_ID);
    RtlInitUnicodeString(&deviceText, SURFACE_POSTURE_CHILD_DEVICE_TEXT);
    RtlInitUnicodeString(&locationText, SURFACE_POSTURE_CHILD_LOCATION_TEXT);

    WDF_PDO_EVENT_CALLBACKS_INIT(&pdoCallbacks);
    WdfPdoInitSetEventCallbacks(ChildInit, &pdoCallbacks);
    WdfPdoInitSetDefaultLocale(ChildInit, SURFACE_POSTURE_LOCALE_ID);

    NTSTATUS status = WdfPdoInitAssignDeviceID(ChildInit, &deviceId);
    if (!NT_SUCCESS(status))
    {
        return status;
    }

    status = WdfPdoInitAddHardwareID(ChildInit, &hardwareId);
    if (!NT_SUCCESS(status))
    {
        return status;
    }

    status = WdfPdoInitAddCompatibleID(ChildInit, &compatibleId);
    if (!NT_SUCCESS(status))
    {
        return status;
    }

    status = WdfPdoInitAssignInstanceID(ChildInit, &instanceId);
    if (!NT_SUCCESS(status))
    {
        return status;
    }

    status = WdfPdoInitAddDeviceText(ChildInit, &deviceText, &locationText, SURFACE_POSTURE_LOCALE_ID);
    if (!NT_SUCCESS(status))
    {
        return status;
    }

    return STATUS_SUCCESS;
}

NTSTATUS SurfacePostureEvtChildListCreateDevice(
    _In_ WDFCHILDLIST ChildList,
    _In_ PWDF_CHILD_IDENTIFICATION_DESCRIPTION_HEADER IdentificationDescription,
    _In_ PWDFDEVICE_INIT ChildInit)
{
    UNREFERENCED_PARAMETER(ChildList);

    PSURFACE_POSTURE_CHILD_IDENTIFICATION_DESCRIPTION childDescription =
        CONTAINING_RECORD(IdentificationDescription, SURFACE_POSTURE_CHILD_IDENTIFICATION_DESCRIPTION, Header);

    if (childDescription->ChildIndex != SURFACE_POSTURE_CHILD_INDEX)
    {
        return STATUS_INVALID_PARAMETER;
    }

    return SurfacePostureInitializeChildInit(ChildInit);
}

BOOLEAN SurfacePostureEvtChildListIdentificationDescriptionCompare(
    _In_ WDFCHILDLIST ChildList,
    _In_ PWDF_CHILD_IDENTIFICATION_DESCRIPTION_HEADER FirstIdentificationDescription,
    _In_ PWDF_CHILD_IDENTIFICATION_DESCRIPTION_HEADER SecondIdentificationDescription)
{
    UNREFERENCED_PARAMETER(ChildList);

    PSURFACE_POSTURE_CHILD_IDENTIFICATION_DESCRIPTION first =
        CONTAINING_RECORD(FirstIdentificationDescription, SURFACE_POSTURE_CHILD_IDENTIFICATION_DESCRIPTION, Header);
    PSURFACE_POSTURE_CHILD_IDENTIFICATION_DESCRIPTION second =
        CONTAINING_RECORD(SecondIdentificationDescription, SURFACE_POSTURE_CHILD_IDENTIFICATION_DESCRIPTION, Header);

    return first->ChildIndex == second->ChildIndex;
}

NTSTATUS SurfacePostureEvtDeviceD0Entry(_In_ WDFDEVICE Device, _In_ WDF_POWER_DEVICE_STATE PreviousState)
{
    UNREFERENCED_PARAMETER(PreviousState);
    SurfacePosturePublishChild(Device);
    return STATUS_SUCCESS;
}

NTSTATUS SurfacePostureEvtDeviceAdd(_In_ WDFDRIVER Driver, _Inout_ PWDFDEVICE_INIT DeviceInit)
{
    UNREFERENCED_PARAMETER(Driver);

    WDF_CHILD_LIST_CONFIG childListConfig;
    WDF_OBJECT_ATTRIBUTES childListAttributes;
    WDF_PNPPOWER_EVENT_CALLBACKS pnpCallbacks;
    WDF_OBJECT_ATTRIBUTES deviceAttributes;

    WDF_PNPPOWER_EVENT_CALLBACKS_INIT(&pnpCallbacks);
    pnpCallbacks.EvtDeviceD0Entry = SurfacePostureEvtDeviceD0Entry;
    WdfDeviceInitSetPnpPowerEventCallbacks(DeviceInit, &pnpCallbacks);

    WDF_CHILD_LIST_CONFIG_INIT(
        &childListConfig,
        sizeof(SURFACE_POSTURE_CHILD_IDENTIFICATION_DESCRIPTION),
        SurfacePostureEvtChildListCreateDevice);
    childListConfig.EvtChildListIdentificationDescriptionCompare = SurfacePostureEvtChildListIdentificationDescriptionCompare;

    WDF_OBJECT_ATTRIBUTES_INIT(&childListAttributes);
    WdfFdoInitSetDefaultChildListConfig(DeviceInit, &childListConfig, &childListAttributes);

    WdfDeviceInitSetDeviceType(DeviceInit, FILE_DEVICE_UNKNOWN);
    WdfDeviceInitSetCharacteristics(DeviceInit, FILE_DEVICE_SECURE_OPEN, FALSE);

    WDF_OBJECT_ATTRIBUTES_INIT(&deviceAttributes);

    WDFDEVICE device;
    NTSTATUS status = WdfDeviceCreate(&DeviceInit, &deviceAttributes, &device);
    if (!NT_SUCCESS(status))
    {
        return status;
    }

    KdPrintEx((DPFLTR_IHVDRIVER_ID, DPFLTR_INFO_LEVEL,
        "SurfacePostureDriver: published PNP0C60-compatible child\n"));

    return STATUS_SUCCESS;
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
