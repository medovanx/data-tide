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
            var flags = IfTable.Read();
            virtualIds.Clear();
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel) continue;
                // "Ethernet-WFP 802.3 MAC Layer...", "Ethernet-QoS Packet Scheduler...": the same adapter again.
                var f = Guid.TryParse(ni.Id, out var g) ? flags.GetValueOrDefault(g) : null;
                if (f is { IsFilter: true })
                {
                    filterIds.Add(ni.Id);
                    continue;
                }
                byId[ni.Id] = ni;
                if (f is { IsHardware: false }) virtualIds.Add(ni.Id);
            }
        }
        catch (Exception ex) { Log.Error(ex); }
    }

    readonly HashSet<string> filterIds = [];
    readonly HashSet<string> virtualIds = [];

    /// <summary>True for adapters Windows doesn't mark as hardware: VPNs, Hyper-V and WSL switches, VM host adapters.</summary>
    public bool IsVirtual(string id) => virtualIds.Contains(id);

    /// <summary>Ids of filter bindings seen so far: duplicates of a real adapter, never counted.</summary>
    public IReadOnlyCollection<string> FilterIds
    {
        get
        {
            if (listedAt == DateTime.MinValue) Relist();
            return filterIds;
        }
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
