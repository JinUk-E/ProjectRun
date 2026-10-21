using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace MoraeGames.Network
{
    /// <summary>IPv4 endpoints for local-network sessions. No DNS or cloud dependency.</summary>
    public static class LanAddress
    {
        public static bool TryParse(string value, out string address, out ushort port)
        {
            address = null;
            port = NetworkSessionManager.DefaultPort;
            if (string.IsNullOrWhiteSpace(value)) return false;
            var parts = value.Trim().Split(':');
            if (parts.Length > 2 || (parts.Length == 2 && (!ushort.TryParse(parts[1], out port) || port == 0))) return false;
            var octets = parts[0].Split('.');
            if (octets.Length != 4) return false;
            foreach (var octet in octets)
            {
                if (octet.Length == 0 || octet.Length > 3 || !byte.TryParse(octet, out _)) return false;
                foreach (char character in octet) if (character < '0' || character > '9') return false;
            }
            if (!IPAddress.TryParse(parts[0], out var ip) || ip.AddressFamily != AddressFamily.InterNetwork) return false;
            var bytes = ip.GetAddressBytes();
            if (bytes[0] == 0 || bytes[0] >= 224 || ip.Equals(IPAddress.Broadcast)) return false;
            address = ip.ToString();
            return true;
        }

        /// <summary>Lists usable adapters once when hosting, keeping multiple NICs visible.</summary>
        public static string[] GetLocalAddresses()
        {
            var addresses = new List<string>();
            try
            {
                foreach (var adapter in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (adapter.OperationalStatus != OperationalStatus.Up || adapter.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                    foreach (var entry in adapter.GetIPProperties().UnicastAddresses)
                        if (entry.Address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(entry.Address))
                        {
                            string address = entry.Address.ToString();
                            if (!addresses.Contains(address)) addresses.Add(address);
                        }
                }
            }
            catch (NetworkInformationException) { }
            catch (PlatformNotSupportedException) { }
            return addresses.ToArray();
        }
    }
}
