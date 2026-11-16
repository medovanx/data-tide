using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;

namespace DataTide.Services;

/// <summary>
/// Reads the byte counters of every real network interface (not loopback or tunnels) and finds which one
/// carries the default route, for the "Auto" choice.
/// </summary>
sealed class NetworkMonitor
{
    [DllImport("iphlpapi.dll")] static extern int GetBestInterface(uint destAddr, out uint bestIfIndex);

    readonly Dictionary<string, NetworkInterface> byId = [];
    DateTime listedAt = DateTime.MinValue, routeAt = DateTime.MinValue;
    string? defaultId;

    /// <summary>Current counters of every interface that is up.</summary>
    public IReadOnlyList<NetInterface> Read()
    {
        // Re-list adapters every 5 s (plugging in Wi-Fi, VPNs); reading counters of a known adapter is cheap.
        if ((DateTime.UtcNow - listedAt).TotalSeconds >= 5) Relist();
        var list = new List<NetInterface>();
        foreach (var ni in byId.Values)
        {
            try
            {
                if (ni.OperationalStatus != OperationalStatus.Up) continue;
                var st = ni.GetIPStatistics();
                list.Add(new NetInterface(ni.Id, ni.Name, ni.Description, st.BytesReceived, st.BytesSent));
            }
            catch { /* adapter vanished between listing and reading */ }
        }
        return list;
    }

    void Relist()
    {
        listedAt = DateTime.UtcNow;
        byId.Clear();
        try
        {
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
                if (ni.NetworkInterfaceType is not (NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel))
                    byId[ni.Id] = ni;
        }
        catch (Exception ex) { Log.Error(ex); }
    }

    /// <summary>Every known interface (up or not), for the settings list.</summary>
    public IReadOnlyList<(string id, string name, string description, bool up)> All()
    {
        Relist();
        return byId.Values.OrderBy(n => n.Name, StringComparer.OrdinalIgnoreCase)
            .Select(n => (n.Id, n.Name, n.Description, n.OperationalStatus == OperationalStatus.Up)).ToList();
    }

    /// <summary>The interface Windows would use to reach the internet (checked every 5 s).</summary>
    public string? DefaultInterfaceId()
    {
        if ((DateTime.UtcNow - routeAt).TotalSeconds < 5 && defaultId != null && byId.ContainsKey(defaultId)) return defaultId;
        routeAt = DateTime.UtcNow;
        defaultId = null;
        try
        {
            // 1.1.1.1 is only used to ask the routing table; nothing is sent to it.
            uint dest = BitConverter.ToUInt32(IPAddress.Parse("1.1.1.1").GetAddressBytes(), 0);
            if (GetBestInterface(dest, out uint index) == 0)
                foreach (var ni in byId.Values)
                {
                    var ip4 = ni.Supports(NetworkInterfaceComponent.IPv4) ? ni.GetIPProperties().GetIPv4Properties() : null;
                    if (ip4?.Index == index) { defaultId = ni.Id; break; }
                }
        }
        catch (Exception ex) { Log.Error(ex); }
        return defaultId;
    }
}
