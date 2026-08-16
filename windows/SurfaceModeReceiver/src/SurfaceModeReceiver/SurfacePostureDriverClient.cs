using Microsoft.Win32.SafeHandles;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;

namespace SurfaceModeReceiver;

internal static class SurfacePostureDriverClient
{
    private const uint GenericRead = 0x80000000;
    private const uint GenericWrite = 0x40000000;
    private const uint FileShareReadWrite = 0x00000003;
    private const uint FileModeOpen = 0x00000003;
    private const uint FileFlagsAndAttributesNormal = 0x00000080;
    private const int StatusBufferSize = 24;
    private static readonly Guid LaptopSlateInterfaceGuid = new("317fc439-3f77-41c8-b09e-08ad63272aa3");

    public static bool TryApply(SurfaceMode targetMode, out string message)
    {
        message = string.Empty;

        if (!TryFindDeviceInterfacePath(LaptopSlateInterfaceGuid, out var path))
        {
            message = "Surface posture driver interface not present.";
            return false;
        }

        try
        {
            using var handle = NativeMethods.CreateFile(
                path,
                GenericRead | GenericWrite,
                FileShareReadWrite,
                IntPtr.Zero,
                FileModeOpen,
                FileFlagsAndAttributesNormal,
                IntPtr.Zero);

            if (handle.IsInvalid)
            {
                message = $"Failed to open posture driver interface {path}: {new Win32Exception(Marshal.GetLastWin32Error()).Message}";
                return false;
            }

            if (!TryQueryStatus(handle, out var before, out var queryMessage))
            {
                message = $"Posture driver status query failed for {path}: {queryMessage}";
                return false;
            }

            var payload = new[] { targetMode == SurfaceMode.Tablet ? (byte)0 : (byte)1 };
            if (!NativeMethods.WriteFile(handle, payload, payload.Length, out var bytesWritten, IntPtr.Zero))
            {
                message = $"Failed to write posture request to {path}: {new Win32Exception(Marshal.GetLastWin32Error()).Message}";
                return false;
            }

            if (bytesWritten != payload.Length)
            {
                message = $"Posture driver at {path} only accepted {bytesWritten} byte(s) of the {payload.Length}-byte request.";
                return false;
            }

            if (!TryQueryStatus(handle, out var after, out queryMessage))
            {
                message = $"Posture driver did not report an applied state for {path}: {queryMessage}";
                return false;
            }

            var expectedMode = targetMode == SurfaceMode.Tablet ? 0u : 1u;
            if (after.CurrentMode != expectedMode || after.LastAppliedStatus != 0 || after.RequestedMode != expectedMode)
            {
                message = $"Posture driver acknowledged {path} but reported current={after.CurrentMode}, requested={after.RequestedMode}, status={after.LastAppliedStatus}.";
                return false;
            }

            message = $"Applied {targetMode} through {path} (sequence {before.Sequence}->{after.Sequence}).";
            return true;
        }
        catch (Exception ex)
        {
            message = $"Posture driver write failed for {path}: {ex.Message}";
            return false;
        }
    }

    private static bool TryQueryStatus(SafeFileHandle handle, out SurfacePostureDriverStatus status, out string message)
    {
        status = default;
        message = string.Empty;

        var output = new byte[StatusBufferSize];
        if (!NativeMethods.DeviceIoControl(
                handle,
                SurfacePostureDriverIoctls.GetStatus,
                IntPtr.Zero,
                0,
                output,
                output.Length,
                out var bytesReturned,
                IntPtr.Zero))
        {
            message = new Win32Exception(Marshal.GetLastWin32Error()).Message;
            return false;
        }

        if (bytesReturned < Marshal.SizeOf<SurfacePostureDriverStatus>())
        {
            message = $"Driver returned only {bytesReturned} bytes of posture status.";
            return false;
        }

        status = MemoryMarshal.Read<SurfacePostureDriverStatus>(output);
        if (status.Size < (uint)Marshal.SizeOf<SurfacePostureDriverStatus>())
        {
            message = $"Driver returned an unexpected posture status size of {status.Size}.";
            return false;
        }

        return true;
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

                var detailBuffer = Marshal.AllocHGlobal(requiredSize);
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

internal readonly record struct SurfacePostureDriverStatus(
    uint Size,
    uint CurrentMode,
    uint RequestedMode,
    uint Sequence,
    uint LastAppliedStatus,
    uint Reserved);

internal static class SurfacePostureDriverIoctls
{
    public const uint GetStatus = 0x83332004;
}

