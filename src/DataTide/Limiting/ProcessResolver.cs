using System.Buffers.Binary;
using System.Runtime.InteropServices;

namespace DataTide.Limiting;

/// <summary>
/// Maps the local end of a flow to the program that owns the socket, from the Windows TCP and UDP owner tables.
/// The tables are read on a background thread (every 500 ms, sooner after a miss but at most every 100 ms),
/// so the packet path only does dictionary lookups and never waits on the system.
/// A socket bound to 0.0.0.0 or :: matches any local address on its port.
/// </summary>
sealed unsafe class ProcessResolver : IDisposable
{
    const int RefreshMs = 500;
    const int MissGapMs = 100;

    readonly record struct SocketKey(byte Proto, UInt128 Address, ushort Port);

    sealed class Snapshot
    {
        public readonly Dictionary<SocketKey, int> Exact = new();
        public readonly Dictionary<(byte proto, ushort port), int> Any4 = new();
        public readonly Dictionary<(byte proto, ushort port), int> Any6 = new();
        public readonly Dictionary<int, ProcessOwner> Owners = new();
    }

    // ::ffff:0:0/96, how an IPv4 address is kept so both families share one key type.
    static readonly UInt128 MappedPrefix = new(0, 0x0000FFFF00000000UL);

    volatile Snapshot current = new();
    readonly AutoResetEvent missed = new(false);
    readonly Dictionary<int, ProcessOwner?> pidCache = new();
    Thread? thread;
    volatile bool running;
    volatile bool missPending;
    byte* table;
    int tableSize;

    public void Start()
    {
        if (running) return;
        Refresh();
        running = true;
        thread = new Thread(Loop) { IsBackground = true, Name = "Limiter owners" };
        thread.Start();
    }

    /// <summary>The program owning <paramref name="key"/>, or null when unknown (yet). Called on the packet path.</summary>
    public ProcessOwner? Find(FlowKey key)
    {
        var s = current;
        Span<byte> b = stackalloc byte[16];
        if (!key.LocalAddress.TryWriteBytes(b, out int n)) return null;
        UInt128 addr = n == 4 ? MappedPrefix | BinaryPrimitives.ReadUInt32BigEndian(b) : BinaryPrimitives.ReadUInt128BigEndian(b);
        bool v4 = n == 4 || IsMapped(addr);
        if (s.Exact.TryGetValue(new SocketKey(key.Protocol, addr, key.LocalPort), out int pid)
            || (v4 && s.Any4.TryGetValue((key.Protocol, key.LocalPort), out pid))
            || s.Any6.TryGetValue((key.Protocol, key.LocalPort), out pid)) // dual-stack sockets on :: also take IPv4
            return s.Owners.GetValueOrDefault(pid);
        if (!missPending)
        {
            missPending = true;
            missed.Set();
        }
        return null;
    }

    static bool IsMapped(UInt128 a) => (a >> 32) == (MappedPrefix >> 32);

    void Loop()
    {
        while (running)
        {
            missed.WaitOne(RefreshMs);
            if (!running) break;
            try { Refresh(); }
            catch (Exception ex) { Log.Error(ex); }
            Thread.Sleep(MissGapMs);
            missPending = false;
        }
    }

    void Refresh()
    {
        var s = new Snapshot();
        var pids = new HashSet<int>();
        ReadTable(s, pids, tcp: true, LimiterNative.AfInet);
        ReadTable(s, pids, tcp: true, LimiterNative.AfInet6);
        ReadTable(s, pids, tcp: false, LimiterNative.AfInet);
        ReadTable(s, pids, tcp: false, LimiterNative.AfInet6);

        foreach (int pid in pids)
        {
            if (!pidCache.TryGetValue(pid, out var owner)) pidCache[pid] = owner = QueryOwner(pid);
            if (owner != null) s.Owners[pid] = owner;
        }
        // A pid that no longer owns any socket may be reused by another program later.
        foreach (int gone in pidCache.Keys.Where(p => !pids.Contains(p)).ToList()) pidCache.Remove(gone);
        current = s;
    }

    void ReadTable(Snapshot s, HashSet<int> pids, bool tcp, int af)
    {
        int cls = tcp ? LimiterNative.TcpTableOwnerPidAll : LimiterNative.UdpTableOwnerPid;
        for (int attempt = 0; ; attempt++)
        {
            int size = tableSize;
            uint rc = tcp
                ? LimiterNative.GetExtendedTcpTable(table, ref size, false, af, cls, 0)
                : LimiterNative.GetExtendedUdpTable(table, ref size, false, af, cls, 0);
            if (rc == 0) break;
            if (rc != LimiterNative.ErrorInsufficientBuffer || attempt > 4) return;
            tableSize = size + 16 * 1024; // room for sockets opened between the two calls
            table = (byte*)NativeMemory.Realloc(table, (nuint)tableSize);
        }

        // Row layouts from the Windows SDK tcpmib.h / udpmib.h: a DWORD count, then the rows. Ports are in network order in the DWORD's first two bytes.
        uint count = *(uint*)table;
        byte* row = table + 4;
        byte proto = tcp ? (byte)6 : (byte)17;
        bool v4 = af == LimiterNative.AfInet;
        // (row size, address offset, port offset, pid offset)
        (int size, int addr, int port, int pid) l = (tcp, v4) switch
        {
            (true, true) => (24, 4, 8, 20),    // MIB_TCPROW_OWNER_PID
            (true, false) => (56, 0, 20, 52),  // MIB_TCP6ROW_OWNER_PID
            (false, true) => (12, 0, 4, 8),    // MIB_UDPROW_OWNER_PID
            _ => (28, 0, 20, 24),              // MIB_UDP6ROW_OWNER_PID
        };
        long max = (tableSize - 4) / l.size;
        for (long i = 0; i < Math.Min(count, max); i++, row += l.size)
        {
            int pid = *(int*)(row + l.pid);
            if (pid == 0) continue; // TIME_WAIT and other orphaned rows
            ushort port = (ushort)((row[l.port] << 8) | row[l.port + 1]);
            UInt128 addr = v4
                ? MappedPrefix | BinaryPrimitives.ReadUInt32BigEndian(new ReadOnlySpan<byte>(row + l.addr, 4))
                : BinaryPrimitives.ReadUInt128BigEndian(new ReadOnlySpan<byte>(row + l.addr, 16));
            pids.Add(pid);
            if (addr == MappedPrefix || addr == UInt128.Zero)
                (v4 ? s.Any4 : s.Any6).TryAdd((proto, port), pid);
            else
                s.Exact.TryAdd(new SocketKey(proto, addr, port), pid);
        }
    }

    /// <summary>The full exe path of a process; works for most processes because the helper runs elevated.</summary>
    static ProcessOwner? QueryOwner(int pid)
    {
        if (pid == 4) return new ProcessOwner(pid, "System");
        using var h = LimiterNative.OpenProcess(LimiterNative.ProcessQueryLimitedInformation, false, pid);
        if (h.IsInvalid) return null;
        char* buffer = stackalloc char[1024];
        int len = 1024;
        return LimiterNative.QueryFullProcessImageName(h, 0, buffer, ref len) && len > 0 ? new ProcessOwner(pid, new string(buffer, 0, len)) : null;
    }

    public void Dispose()
    {
        running = false;
        missed.Set();
        if (thread == null || thread.Join(2000))
        {
            NativeMemory.Free(table);
            table = null;
            missed.Dispose();
        }
    }
}
