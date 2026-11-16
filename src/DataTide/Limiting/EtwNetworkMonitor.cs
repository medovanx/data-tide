using System.Diagnostics;
using System.Runtime.InteropServices;

namespace DataTide.Limiting;

/// <summary>
/// Per-program network speeds from Windows' own kernel network events (ETW provider Microsoft-Windows-Kernel-Network),
/// the same source Task Manager and Resource Monitor use. Opening that real-time session needs administrator rights,
/// so it runs in the elevated helper. Unlike the packet limiter it needs no driver and touches no packet.
/// Loopback traffic (a program talking to itself or to a local proxy) is left out.
/// </summary>
sealed unsafe class EtwNetworkMonitor : IDisposable
{
    const string SessionName = "DataTide-Network";
    static readonly Guid KernelNetwork = new("7DD42A49-5329-4832-8DFD-43D979153A88");
    const ulong KeywordIPv4 = 0x10, KeywordIPv6 = 0x20;

    // Event ids of Microsoft-Windows-Kernel-Network: data sent and received, per protocol and IP version.
    // The payload of each starts with the process id (UInt32) and the byte count (UInt32), then the addresses.
    const ushort TcpV4Send = 10, TcpV4Recv = 11, TcpV6Send = 26, TcpV6Recv = 27,
                 UdpV4Send = 42, UdpV4Recv = 43, UdpV6Send = 58, UdpV6Recv = 59;

    readonly TrafficMeter meter = new();
    readonly Stopwatch clock = Stopwatch.StartNew();
    readonly Dictionary<int, (string path, double at)> paths = [];
    readonly EventRecordCallback callback;
    ulong session, trace = InvalidTrace;
    nint props, logfile;
    Thread? pump;
    const ulong InvalidTrace = ulong.MaxValue;

    public bool Running { get; private set; }
    public string? Error { get; private set; }

    public EtwNetworkMonitor() => callback = OnEvent;

    /// <summary>Starts the session and its event pump. False (with <see cref="Error"/>) when Windows refused.</summary>
    public bool Start()
    {
        if (Running) return true;
        try
        {
            props = NewProperties();
            int r = StartTraceW(out session, SessionName, props);
            if (r == 183) // ERROR_ALREADY_EXISTS: a session left behind by a helper that was killed
            {
                ControlTraceW(0, SessionName, props, 1 /* STOP */);
                Marshal.FreeHGlobal(props);
                props = NewProperties();
                r = StartTraceW(out session, SessionName, props);
            }
            if (r != 0) return Fail($"Windows refused the network event session (error {r}).");

            var provider = KernelNetwork;
            r = EnableTraceEx2(session, ref provider, 1 /* ENABLE */, 4 /* informational */, KeywordIPv4 | KeywordIPv6, 0, 0, 0);
            if (r != 0) return Fail($"Windows refused the kernel network events (error {r}).");

            // EVENT_TRACE_LOGFILEW, 448 bytes on x64: only LoggerName, ProcessTraceMode and EventRecordCallback are set.
            logfile = Marshal.AllocHGlobal(448);
            new Span<byte>((void*)logfile, 448).Clear();
            *(nint*)(logfile + 8) = Marshal.StringToHGlobalUni(SessionName);
            *(uint*)(logfile + 28) = 0x100 | 0x10000000; // PROCESS_TRACE_MODE_REAL_TIME | PROCESS_TRACE_MODE_EVENT_RECORD
            *(nint*)(logfile + 424) = Marshal.GetFunctionPointerForDelegate(callback);
            trace = OpenTraceW(logfile);
            if (trace == InvalidTrace) return Fail($"Windows refused to open the network event session (error {Marshal.GetLastPInvokeError()}).");

            pump = new Thread(() =>
            {
                ulong h = trace;
                ProcessTrace(&h, 1, 0, 0); // blocks, calling OnEvent, until the session stops
            }) { IsBackground = true, Name = "Network events" };
            pump.Start();
            Running = true;
            Error = null;
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex);
            return Fail(ex.Message);
        }
    }

    bool Fail(string message)
    {
        Error = message;
        Stop();
        return false;
    }

    /// <summary>EVENT_TRACE_PROPERTIES (120 bytes on x64) followed by room for the session name.</summary>
    static nint NewProperties()
    {
        const int size = 120 + 1024 * 2;
        nint p = Marshal.AllocHGlobal(size);
        new Span<byte>((void*)p, size).Clear();
        *(uint*)p = size;                       // Wnode.BufferSize
        *(uint*)(p + 40) = 1;                   // Wnode.ClientContext: QPC timestamps
        *(uint*)(p + 44) = 0x20000;             // Wnode.Flags: WNODE_FLAG_TRACED_GUID
        *(uint*)(p + 48) = 64;                  // BufferSize (KB)
        *(uint*)(p + 64) = 0x100;               // LogFileMode: EVENT_TRACE_REAL_TIME_MODE
        *(uint*)(p + 68) = 1;                   // FlushTimer (s), so speeds stay live
        *(uint*)(p + 116) = 120;                // LoggerNameOffset
        return p;
    }

    void OnEvent(byte* record)
    {
        try
        {
            ushort id = *(ushort*)(record + 40);              // EVENT_HEADER.EventDescriptor.Id
            ushort len = *(ushort*)(record + 86);             // UserDataLength
            byte* data = *(byte**)(record + 96);              // UserData
            bool v6 = id is TcpV6Send or TcpV6Recv or UdpV6Send or UdpV6Recv;
            bool send = id is TcpV4Send or TcpV6Send or UdpV4Send or UdpV6Send;
            bool recv = id is TcpV4Recv or TcpV6Recv or UdpV4Recv or UdpV6Recv;
            if (!send && !recv || data == null || len < (v6 ? 40 : 16)) return;

            int pid = *(int*)data;
            int size = *(int*)(data + 4);
            if (size <= 0 || pid <= 0) return;
            if (v6 ? IsLoopback6(data + 8) || IsLoopback6(data + 24) : data[8] == 127 || data[12] == 127) return;

            double now = clock.Elapsed.TotalSeconds;
            string? path = PathOf(pid, now);
            if (path != null) meter.Add(path, send ? Direction.Upload : Direction.Download, size, now);
        }
        catch { /* never let an exception escape into ETW's thread */ }
    }

    static bool IsLoopback6(byte* a)
    {
        for (int i = 0; i < 15; i++) if (a[i] != 0) return false;
        return a[15] == 1;
    }

    /// <summary>The exe behind a process id, cached for a minute (process ids get reused).</summary>
    string? PathOf(int pid, double now)
    {
        lock (paths)
        {
            if (paths.TryGetValue(pid, out var c) && now - c.at < 60) return c.path;
            string? path = null;
            using (var h = LimiterNative.OpenProcess(0x1000 /* QUERY_LIMITED_INFORMATION */, false, pid))
                if (!h.IsInvalid)
                {
                    char* buf = stackalloc char[1024];
                    int n = 1024;
                    if (LimiterNative.QueryFullProcessImageName(h, 0, buf, ref n)) path = new string(buf, 0, n);
                }
            path ??= pid == 4 ? "System" : null;
            if (path != null) paths[pid] = (path, now);
            return path;
        }
    }

    /// <summary>Speeds of every program seen in the last two minutes, busiest first.</summary>
    public IReadOnlyList<ProcessTraffic> Snapshot()
    {
        meter.Roll(clock.Elapsed.TotalSeconds);
        return meter.Snapshot();
    }

    public void Stop()
    {
        Running = false;
        if (props != 0)
        {
            ControlTraceW(session, null, props, 1 /* STOP: also ends ProcessTrace */);
        }
        if (trace != InvalidTrace) { CloseTrace(trace); trace = InvalidTrace; }
        pump?.Join(2000);
        pump = null;
        if (logfile != 0) { Marshal.FreeHGlobal(*(nint*)(logfile + 8)); Marshal.FreeHGlobal(logfile); logfile = 0; }
        if (props != 0) { Marshal.FreeHGlobal(props); props = 0; }
    }

    public void Dispose() => Stop();

    delegate void EventRecordCallback(byte* record);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode)] static extern int StartTraceW(out ulong handle, string name, nint properties);
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode)] static extern int ControlTraceW(ulong handle, string? name, nint properties, uint code);
    [DllImport("advapi32.dll")] static extern int EnableTraceEx2(ulong handle, ref Guid provider, uint control, byte level, ulong matchAny, ulong matchAll, uint timeout, nint parameters);
    [DllImport("advapi32.dll", SetLastError = true)] static extern ulong OpenTraceW(nint logfile);
    [DllImport("advapi32.dll")] static extern int ProcessTrace(ulong* handles, uint count, nint start, nint end);
    [DllImport("advapi32.dll")] static extern int CloseTrace(ulong handle);
}
