using System.Runtime.InteropServices;

namespace DataTide.Limiting.Ndis;

/// <summary>
/// UNSORTED_READ_SEND_REQUEST (Common.h lines 895 to 899, pack 1): a pointer to an array of INTERMEDIATE_BUFFER pointers
/// and its count. On a read the driver writes back how many it filled; on a send, how many it accepted (a tally, not a prefix).
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
struct UnsortedRequest
{
    public const int Size = 12;

    public nint Packets;
    public uint Count;
}
