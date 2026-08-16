using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
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
    private static readonly Guid LaptopSlateInterfaceGuid = new("317fc439-3f77-41c8-b09e-08ad63272aa3");
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

            if (TryToggleGpioLaptopSlateIndicator(targetMode, out var path, out var gpioMessage))
            {
                CurrentMode = targetMode;
                _log.Info("posture", "gpio", previous.ToString(), targetMode.ToString(), true, gpioMessage);
                return new PostureApplyResult(true, true, previous, targetMode, "gpio", gpioMessage);
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

    private bool TryToggleGpioLaptopSlateIndicator(SurfaceMode targetMode, out string? devicePath, out string message)
    {
        devicePath = null;
        message = string.Empty;

        if (!TryFindDeviceInterfacePath(LaptopSlateInterfaceGuid, out var path))
        {
            message = "GPIO laptop/slate interface not present; using registry fallback.";
            return false;
        }

        devicePath = path;
        try
        {
            using var handle = NativeMethods.CreateFile(
                devicePath,
                NativeMethods.GenericWrite,
                NativeMethods.FileShareReadWrite,
                IntPtr.Zero,
                NativeMethods.FileModeOpen,
                NativeMethods.FileFlagsAndAttributesNormal,
                IntPtr.Zero);

            if (handle.IsInvalid)
            {
                message = $"Failed to open {devicePath}: {new Win32Exception(Marshal.GetLastWin32Error()).Message}";
                return false;
            }

            using var stream = new FileStream(handle, FileAccess.Write);
            stream.WriteByte(targetMode == SurfaceMode.Tablet ? (byte)0x00 : (byte)0x01);
            stream.Flush(true);

            BroadcastConvertibleSlateModeChange();
            message = $"Toggled Microsoft GPIO laptop/slate indicator via {devicePath}.";
            return true;
        }
        catch (Exception ex)
        {
            message = $"GPIO indicator write failed for {devicePath}: {ex.Message}";
            return false;
        }
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

    private static bool TryFindDeviceInterfacePath(Guid interfaceGuid, [NotNullWhen(true)] out string? devicePath)
    {
        devicePath = null;

        var deviceInfo = NativeMethods.SetupDiGetClassDevs(
            ref interfaceGuid,
            null,
            IntPtr.Zero,
            NativeMethods.DigcfDeviceinterface | NativeMethods.DigcfPresent);

        if (deviceInfo == NativeMethods.InvalidHandleValue)
        {
            return false;
        }

        try
        {
            var interfaceData = new NativeMethods.SpDeviceInterfaceData
            {
                CbSize = Marshal.SizeOf<NativeMethods.SpDeviceInterfaceData>()
            };

            for (var index = 0; NativeMethods.SetupDiEnumDeviceInterfaces(deviceInfo, IntPtr.Zero, ref interfaceGuid, index, ref interfaceData); index++)
            {
                if (!NativeMethods.SetupDiGetDeviceInterfaceDetail(deviceInfo, ref interfaceData, IntPtr.Zero, 0, out var requiredSize, IntPtr.Zero))
                {
                    var error = Marshal.GetLastWin32Error();
                    if (error != NativeMethods.ErrorInsufficientBuffer)
                    {
                        continue;
                    }
                }

                var detailBuffer = Marshal.AllocHGlobal((int)requiredSize);
                try
                {
                    var cbSize = IntPtr.Size == 8 ? 8 : 6;
                    Marshal.WriteInt32(detailBuffer, cbSize);

                    if (!NativeMethods.SetupDiGetDeviceInterfaceDetail(deviceInfo, ref interfaceData, detailBuffer, requiredSize, out _, IntPtr.Zero))
                    {
                        continue;
                    }

                    var pathOffset = IntPtr.Size == 8 ? 8 : 6;
                    var candidate = Marshal.PtrToStringUni(IntPtr.Add(detailBuffer, pathOffset));
                    if (!string.IsNullOrWhiteSpace(candidate))
                    {
                        devicePath = candidate;
                        return true;
                    }
                }
                finally
                {
                    Marshal.FreeHGlobal(detailBuffer);
                }
            }

            return false;
        }
        finally
        {
            NativeMethods.SetupDiDestroyDeviceInfoList(deviceInfo);
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
