using System.Runtime.InteropServices;

namespace DataTide.Interop;

/// <summary>
/// Reads the Windows interface table (GetIfTable2) for the flags .NET does not expose. Windows lists every
/// filter driver bound to an adapter (WFP, QoS Packet Scheduler, firewalls, VPN clients) as an interface of
/// its own, and each one sees the same packets, so counting them would repeat the adapter's traffic many times.
/// </summary>
static unsafe class IfTable
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct MIB_IF_ROW2
    {
        public ulong InterfaceLuid;
        public uint InterfaceIndex;
        public Guid InterfaceGuid;
        public fixed char Alias[257];
        public fixed char Description[257];
        public uint PhysicalAddressLength;
        public fixed byte PhysicalAddress[32];
        public fixed byte PermanentPhysicalAddress[32];
        public uint Mtu;
        public uint Type;
        public uint TunnelType;
        public uint MediaType;
        public uint PhysicalMediumType;
        public uint AccessType;
        public uint DirectionType;
        public byte InterfaceAndOperStatusFlags;
        public uint OperStatus;
        public uint AdminStatus;
        public uint MediaConnectState;
        public Guid NetworkGuid;
        public uint ConnectionType;
        public ulong TransmitLinkSpeed;
        public ulong ReceiveLinkSpeed;
        public ulong InOctets, InUcastPkts, InNUcastPkts, InDiscards, InErrors, InUnknownProtos, InUcastOctets, InMulticastOctets, InBroadcastOctets;
        public ulong OutOctets, OutUcastPkts, OutNUcastPkts, OutDiscards, OutErrors, OutUcastOctets, OutMulticastOctets, OutBroadcastOctets, OutQLen;
    }

    [DllImport("iphlpapi.dll")] static extern int GetIfTable2(out IntPtr table);
    [DllImport("iphlpapi.dll")] static extern void FreeMibTable(IntPtr table);

    const byte HardwareInterface = 0x01, FilterInterface = 0x02;

    /// <summary>One row per interface GUID: whether it is a filter binding and whether it is real hardware.</summary>
    public sealed record Info(Guid Id, bool IsFilter, bool IsHardware);

    public static int RowSize => sizeof(MIB_IF_ROW2);

    public static Dictionary<Guid, Info> Read()
    {
        var result = new Dictionary<Guid, Info>();
        // The layout must match the 64-bit MIB_IF_ROW2 (1352 bytes); if it ever doesn't, filter nothing rather than misread.
        if (sizeof(MIB_IF_ROW2) != 1352 || GetIfTable2(out var table) != 0) return result;
        try
        {
            int count = Marshal.ReadInt32(table);
            var rows = (MIB_IF_ROW2*)(table + 8); // NumEntries, then the rows aligned to 8
            for (int i = 0; i < count; i++)
            {
                var r = rows[i];
                result[r.InterfaceGuid] = new Info(r.InterfaceGuid,
                    (r.InterfaceAndOperStatusFlags & FilterInterface) != 0,
                    (r.InterfaceAndOperStatusFlags & HardwareInterface) != 0);
            }
        }
        finally { FreeMibTable(table); }
        return result;
    }
}
