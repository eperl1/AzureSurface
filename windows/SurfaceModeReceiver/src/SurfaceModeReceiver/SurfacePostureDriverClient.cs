using Microsoft.Win32.SafeHandles;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace SurfaceModeReceiver;

internal static class SurfacePostureDriverClient
{
    private const uint GenericWrite = 0x40000000;
    private const uint FileShareReadWrite = 0x00000003;
    private const uint FileModeOpen = 0x00000003;
    private const uint FileFlagsAndAttributesNormal = 0x00000080;
    private const int ConvertibleSlateModeMetric = 0x2003;
    private const int PostureChangeTimeoutMs = 5000;
    private const int PosturePollIntervalMs = 100;
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
                GenericWrite,
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

            var payload = new[] { targetMode == SurfaceMode.Tablet ? (byte)0 : (byte)1 };
            var expectedMetric = targetMode == SurfaceMode.Tablet ? 0 : 1;
            var beforeMetric = NativeMethods.GetSystemMetrics(ConvertibleSlateModeMetric);

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

            if (!TryWaitForWindowsMetric(expectedMetric, out var observedMetric, out var waitMessage))
            {
                message = $"Posture driver wrote successfully to {path}, but Windows did not report the requested posture change. {waitMessage}";
                return false;
            }

            message = $"Applied {targetMode} through {path} (SM_CONVERTIBLESLATEMODE {beforeMetric}->{observedMetric}).";
            return true;
        }
        catch (Exception ex)
        {
            message = $"Posture driver write failed for {path}: {ex.Message}";
            return false;
        }
    }

    private static bool TryWaitForWindowsMetric(int expectedMetric, out int observedMetric, out string message)
    {
        var stopwatch = Stopwatch.StartNew();
        observedMetric = NativeMethods.GetSystemMetrics(ConvertibleSlateModeMetric);
        while (stopwatch.ElapsedMilliseconds < PostureChangeTimeoutMs)
        {
            if (observedMetric == expectedMetric)
            {
                message = string.Empty;
                return true;
            }

            Thread.Sleep(PosturePollIntervalMs);
            observedMetric = NativeMethods.GetSystemMetrics(ConvertibleSlateModeMetric);
        }

        message = $"Observed SM_CONVERTIBLESLATEMODE={observedMetric} after {PostureChangeTimeoutMs}ms; expected {expectedMetric}.";
        return false;
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

                    var pathOffset = 4;
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
