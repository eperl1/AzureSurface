using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace SurfaceModeReceiver;

internal interface IPostureController
{
    SurfaceMode CurrentMode { get; }

    PostureApplyResult Apply(SurfaceMode targetMode);
}

internal sealed record PostureApplyResult(
    bool Ok,
    bool Changed,
    SurfaceMode PreviousMode,
    SurfaceMode CurrentMode,
    string Path,
    string Message);

[SupportedOSPlatform("windows")]
internal sealed class SurfacePostureController : IPostureController
{
    private const int ConvertibleSlateModeMetric = 0x2003;
    private const int BroadcastTimeoutMs = 1000;
    private const string ConvertibleSlateModeValueName = "ConvertibleSlateMode";
    private const string ConvertibleSlateModeRegistryPath = @"SYSTEM\CurrentControlSet\Control\PriorityControl";
    private readonly object _gate = new();
    private readonly ReceiverLog _log;

    public SurfacePostureController(ReceiverLog log)
    {
        _log = log;
        CurrentMode = ReadCurrentSystemMode();
    }

    public SurfaceMode CurrentMode { get; private set; }

    public PostureApplyResult Apply(SurfaceMode targetMode)
    {
        lock (_gate)
        {
            var previous = CurrentMode;
            if (previous == targetMode)
            {
                return new PostureApplyResult(true, false, previous, targetMode, "no-op", "Posture already matches the requested mode.");
            }

            if (TryToggleSurfacePostureDriver(targetMode, out var driverMessage))
            {
                CurrentMode = targetMode;
                _log.Info("posture", "driver", previous.ToString(), targetMode.ToString(), true, driverMessage);
                return new PostureApplyResult(true, true, previous, targetMode, "driver", driverMessage);
            }

            if (driverMessage != "Surface posture driver interface not present.")
            {
                _log.Error("posture", "driver", previous.ToString(), targetMode.ToString(), false, driverMessage);
                return new PostureApplyResult(false, false, previous, previous, "driver", driverMessage);
            }

            var registryMessage = UpdateConvertibleSlateModeRegistry(targetMode);
            BroadcastConvertibleSlateModeChange();
            CurrentMode = targetMode;

            _log.Info("posture", "registry", previous.ToString(), targetMode.ToString(), true, registryMessage);
            return new PostureApplyResult(true, true, previous, targetMode, "registry", registryMessage);
        }
    }

    private static SurfaceMode ReadCurrentSystemMode()
    {
        try
        {
            return NativeMethods.GetSystemMetrics(ConvertibleSlateModeMetric) == 0
                ? SurfaceMode.Tablet
                : SurfaceMode.Laptop;
        }
        catch
        {
            return SurfaceMode.Tablet;
        }
    }

    private bool TryToggleSurfacePostureDriver(SurfaceMode targetMode, out string message)
    {
        if (SurfacePostureDriverClient.TryApply(targetMode, out message))
        {
            return true;
        }
        return false;
    }

    private static string UpdateConvertibleSlateModeRegistry(SurfaceMode targetMode)
    {
        try
        {
            using var key = Registry.LocalMachine.CreateSubKey(ConvertibleSlateModeRegistryPath, writable: true);
            if (key is null)
            {
                return "Registry key unavailable; could not update ConvertibleSlateMode.";
            }

            key.SetValue(ConvertibleSlateModeValueName, targetMode == SurfaceMode.Tablet ? 0 : 1, RegistryValueKind.DWord);
            return $"Updated HKLM\\{ConvertibleSlateModeRegistryPath}\\{ConvertibleSlateModeValueName} to {(targetMode == SurfaceMode.Tablet ? 0 : 1)}.";
        }
        catch (Exception ex)
        {
            return $"Registry fallback failed: {ex.Message}";
        }
    }

    private static void BroadcastConvertibleSlateModeChange()
    {
        try
        {
            NativeMethods.SendSettingChange("ConvertibleSlateMode");
        }
        catch
        {
            // best-effort only
        }
    }

}

internal static class NativeMethods
{
    private const int BroadcastTimeoutMs = 1000;
    public const int ErrorInsufficientBuffer = 122;
    public const uint DigcfPresent = 0x00000002;
    public const uint DigcfDeviceinterface = 0x00000010;
    public const uint GenericWrite = 0x40000000;
    public const uint FileShareReadWrite = 0x00000003;
    public const uint FileModeOpen = 0x00000003;
    public const uint FileFlagsAndAttributesNormal = 0x00000080;
    public static readonly IntPtr InvalidHandleValue = new(-1);

    [StructLayout(LayoutKind.Sequential)]
    internal struct SpDeviceInterfaceData
    {
        public int CbSize;
        public Guid InterfaceClassGuid;
        public int Flags;
        public IntPtr Reserved;
    }

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern IntPtr SetupDiGetClassDevs(
        ref Guid classGuid,
        string? enumerator,
        IntPtr hwndParent,
        uint flags);

    [DllImport("setupapi.dll", SetLastError = true)]
    internal static extern bool SetupDiEnumDeviceInterfaces(
        IntPtr deviceInfoSet,
        IntPtr deviceInfoData,
        ref Guid interfaceClassGuid,
        int memberIndex,
        ref SpDeviceInterfaceData deviceInterfaceData);

    [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern bool SetupDiGetDeviceInterfaceDetail(
        IntPtr deviceInfoSet,
        ref SpDeviceInterfaceData deviceInterfaceData,
        IntPtr deviceInterfaceDetailData,
        int deviceInterfaceDetailDataSize,
        out int requiredSize,
        IntPtr deviceInfoData);

    [DllImport("setupapi.dll", SetLastError = true)]
    internal static extern bool SetupDiDestroyDeviceInfoList(IntPtr deviceInfoSet);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern SafeFileHandle CreateFile(
        string lpFileName,
        uint dwDesiredAccess,
        uint dwShareMode,
        IntPtr lpSecurityAttributes,
        uint dwCreationDisposition,
        uint dwFlagsAndAttributes,
        IntPtr hTemplateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern bool WriteFile(
        SafeFileHandle hFile,
        byte[] lpBuffer,
        int nNumberOfBytesToWrite,
        out int lpNumberOfBytesWritten,
        IntPtr lpOverlapped);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern bool DeviceIoControl(
        SafeFileHandle hDevice,
        uint dwIoControlCode,
        IntPtr lpInBuffer,
        int nInBufferSize,
        byte[] lpOutBuffer,
        int nOutBufferSize,
        out int lpBytesReturned,
        IntPtr lpOverlapped);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr SendMessageTimeout(
        IntPtr hWnd,
        uint Msg,
        IntPtr wParam,
        string lParam,
        uint fuFlags,
        uint uTimeout,
        out IntPtr lpdwResult);

    [DllImport("kernel32.dll")]
    internal static extern int GetSystemMetrics(int nIndex);

    internal static void SendSettingChange(string settingName)
    {
        const uint wmSettingChange = 0x001A;
        const uint smtoAbortIfHung = 0x0002;
        _ = SendMessageTimeout(
            new IntPtr(-1),
            wmSettingChange,
            IntPtr.Zero,
            settingName,
            smtoAbortIfHung,
            BroadcastTimeoutMs,
            out _);
    }
}
