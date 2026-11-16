using System.Runtime.InteropServices;

namespace DataTide.Limiting.Ndis;

/// <summary>
/// TCP_AdapterList (Common.h lines 136 to 144, inside #pragma pack(push,1)). Handles are kernel pointers,
/// 64-bit in a 64-bit process, and sit unaligned at offset 8196 because of the 1-byte packing.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
unsafe struct TcpAdapterList
{
    public const int Size = 8836;

    public uint AdapterCount;
    public fixed byte AdapterNameList[NdisConstants.AdapterListSize * NdisConstants.AdapterNameSize];
    public fixed long AdapterHandle[NdisConstants.AdapterListSize];
    public fixed uint AdapterMediumList[NdisConstants.AdapterListSize];
    public fixed byte CurrentAddress[NdisConstants.AdapterListSize * NdisConstants.EtherAddrLength];
    public fixed ushort Mtu[NdisConstants.AdapterListSize];
}
