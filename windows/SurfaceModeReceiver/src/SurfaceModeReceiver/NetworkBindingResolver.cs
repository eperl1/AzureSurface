using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace SurfaceModeReceiver;

internal static class NetworkBindingResolver
{
    public static IPAddress ResolveBindAddress(string configuredAddress)
    {
        if (IPAddress.TryParse(configuredAddress, out var explicitAddress))
        {
            return explicitAddress;
        }

        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.OperationalStatus != OperationalStatus.Up)
            {
                continue;
            }

            if (!nic.Name.Contains("tailscale", StringComparison.OrdinalIgnoreCase) &&
                !nic.Description.Contains("tailscale", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var ip = GetFirstIpv4(nic);
            if (ip is not null)
            {
                return ip;
            }
        }

        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.OperationalStatus != OperationalStatus.Up)
            {
                continue;
            }

            var ip = GetFirstPrivateIpv4(nic);
            if (ip is not null)
            {
                return ip;
            }
        }

        return IPAddress.Any;
    }

    private static IPAddress? GetFirstIpv4(NetworkInterface nic)
    {
        return nic.GetIPProperties().UnicastAddresses
            .Select(address => address.Address)
            .FirstOrDefault(address => address.AddressFamily == AddressFamily.InterNetwork &&
                                        !IPAddress.IsLoopback(address));
    }

    private static IPAddress? GetFirstPrivateIpv4(NetworkInterface nic)
    {
        return nic.GetIPProperties().UnicastAddresses
            .Select(address => address.Address)
            .FirstOrDefault(IsPrivateIPv4);
    }

    private static bool IsPrivateIPv4(IPAddress address)
    {
        if (address.AddressFamily != AddressFamily.InterNetwork)
        {
            return false;
        }

        var bytes = address.GetAddressBytes();
        return bytes[0] == 10 ||
               (bytes[0] == 172 && bytes[1] is >= 16 and <= 31) ||
               (bytes[0] == 192 && bytes[1] == 168);
    }
}
