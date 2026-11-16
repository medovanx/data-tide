using System.Runtime.InteropServices;

namespace DataTide.Limiting.Ndis;

/// <summary>ADAPTER_EVENT (Common.h lines 359 to 363, pack 1): the adapter and a Win32 event the driver signals when packets queue.</summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
struct AdapterEvent
{
    public const int Size = 16;

    public nint Adapter;
    public nint Event;
}
