using System.Runtime.InteropServices;

namespace DataTide.Limiting.Ndis;

/// <summary>
/// The fixed part of INTERMEDIATE_BUFFER (Common.h lines 184 to 197, pack 1). The frame bytes (m_IBuffer) follow at
/// offset 52. The first 16 bytes are a union of m_hAdapter and a LIST_ENTRY; the driver writes the adapter handle
/// there on read and reads it back on an unsorted send, so it must be left untouched.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
unsafe struct IntermediateBufferHeader
{
    public const int Size = 52;

    public nint Adapter;
    public nint LinkBack;
    public uint DeviceFlags;
    public uint Length;
    public uint Flags;
    public uint Vlan8021q;
    public uint FilterId;
    public fixed uint Reserved[4];
}
