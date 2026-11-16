using System.Runtime.InteropServices;

namespace DataTide.Limiting.Ndis;

/// <summary>ADAPTER_MODE (Common.h lines 330 to 334, pack 1): 12 bytes on 64-bit.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
struct AdapterMode
{
    public const int Size = 12;

    public nint Adapter;
    public uint Flags;
}
