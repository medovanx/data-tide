using System.Runtime.InteropServices;

namespace DataTide.Limiting;

/// <summary>
/// Native packet buffers for the driver. It writes frames straight into them through pointers, so they must never move,
/// and reusing them keeps the packet path free of allocations. Grows in steps up to a cap; only the worker thread uses it.
/// </summary>
sealed unsafe class PacketPool(int bufferSize, int max) : IDisposable
{
    const int Step = 256;

    readonly Stack<nint> free = new();
    readonly List<nint> all = new();

    public int InUse => all.Count - free.Count;

    /// <summary>A free buffer, or 0 when the cap is reached.</summary>
    public nint Rent()
    {
        if (free.Count == 0) Grow();
        return free.Count > 0 ? free.Pop() : 0;
    }

    public void Return(nint buffer)
    {
        if (buffer != 0) free.Push(buffer);
    }

    void Grow()
    {
        int n = Math.Min(Step, max - all.Count);
        for (int i = 0; i < n; i++)
        {
            nint p = (nint)NativeMemory.Alloc((nuint)bufferSize);
            all.Add(p);
            free.Push(p);
        }
    }

    public void Dispose()
    {
        foreach (nint p in all) NativeMemory.Free((void*)p);
        all.Clear();
        free.Clear();
    }
}
