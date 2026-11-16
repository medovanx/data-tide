using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using DataTide.Limiting.Ndis;

namespace DataTide.Limiting;

/// <summary>
/// The limiter itself, run inside the elevated helper. While at least one rule limits something it puts every
/// TCP/IP adapter into tunnel mode, so the driver hands us each packet instead of delivering it; one worker thread
/// reads them in batches, finds the owning program, asks the <see cref="Shaper{TPacket}"/> whether the packet may go,
/// and sends it on (outgoing to the adapter, incoming to the TCP/IP stack), holds it, or drops it.
/// In tunnel mode a stalled worker stalls the whole network, so anything we cannot attribute passes at once,
/// and adapter modes are put back to normal on every way out: Stop, Dispose, a worker crash, an unhandled exception and process exit.
/// </summary>
sealed unsafe class LimiterEngine : IDisposable
{
    const int Batch = 256;
    const int MaxBuffers = 16384;
    const double AdapterRescanSeconds = 5;
    const int MaxReadFailures = 5;

    readonly Stopwatch clock = Stopwatch.StartNew();
    readonly Shaper<nint> shaper = new();
    readonly ProcessResolver resolver = new();
    readonly TrafficMeter meter = new();
    readonly ManualResetEvent packetEvent = new(false);
    readonly ManualResetEvent wake = new(false);
    readonly object rulesGate = new();
    readonly object ioGate = new();
    readonly Dictionary<nint, NdisAdapter> tunnelled = new();
    readonly HashSet<nint> foreign = new();

    NdisDriver? driver;
    PacketPool? pool;
    nint* readSlots;
    nint* toAdapter;
    nint* toStack;
    int adapterCount, stackCount;
    List<LimitRule>? pendingRules;
    int activeRules;
    volatile nint[] tunnelledSnapshot = [];
    Thread? worker;
    volatile bool running;
    volatile string? error;
    volatile string? warning;
    double lastScan = double.NegativeInfinity;
    int readFailures;
    int stopped;
    bool hooked;

    double Now => clock.Elapsed.TotalSeconds;

    /// <summary>True while the driver is open and the worker runs.</summary>
    public bool Running => running;

    /// <summary>True when the last applied rules limit at least one program (used by the helper's idle exit).</summary>
    public bool HasActiveRules => Volatile.Read(ref activeRules) > 0;

    public string? DriverVersion => driver?.VersionText;

    public LimiterStatus Status() => new(running, error ?? warning, meter.Snapshot());

    /// <summary>Opens the driver and starts the worker. Nothing is tunnelled until a rule arrives. False (see <see cref="Status"/>) when the driver is missing.</summary>
    public bool Start()
    {
        if (running) return true;
        driver = NdisDriver.TryOpen(out var err);
        if (driver == null)
        {
            error = err;
            return false;
        }
        error = null;
        pool = new PacketPool(NdisDriver.BufferSize, MaxBuffers);
        readSlots = (nint*)NativeMemory.AllocZeroed(Batch, (nuint)sizeof(nint));
        toAdapter = (nint*)NativeMemory.AllocZeroed(Batch, (nuint)sizeof(nint));
        toStack = (nint*)NativeMemory.AllocZeroed(Batch, (nuint)sizeof(nint));
        AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandled;
        hooked = true;
        resolver.Start();
        running = true;
        worker = new Thread(Work) { IsBackground = true, Name = "Limiter", Priority = ThreadPriority.AboveNormal };
        worker.Start();
        return true;
    }

    /// <summary>Replaces all rules. Takes effect on the worker within a moment; packets held for a cleared limit are sent on.</summary>
    public void Apply(IReadOnlyList<LimitRule> rules)
    {
        var copy = rules.Where(r => r != null).Select(r => r.Clone()).ToList();
        lock (rulesGate) pendingRules = copy;
        Volatile.Write(ref activeRules, copy.Count(r => r.Limits(Direction.Download) || r.Limits(Direction.Upload)));
        wake.Set();
    }

    void Work()
    {
        var waits = new WaitHandle[] { wake, packetEvent };
        try
        {
            while (running)
            {
                int timeout;
                lock (ioGate)
                {
                    if (!running) break;
                    timeout = Step();
                }
                WaitHandle.WaitAny(waits, timeout);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex);
            error = "The speed limiter stopped after an error: " + ex.Message;
            running = false;
            EmergencyRestore();
        }
    }

    /// <summary>One pass of the worker; returns how long to wait for the next packet or due time.</summary>
    int Step()
    {
        double now = Now;
        wake.Reset();
        TakeRules(now);
        bool limiting = shaper.Limited().Count > 0;
        if (limiting && (tunnelled.Count == 0 || now - lastScan >= AdapterRescanSeconds)) ScanAdapters(now);
        else if (!limiting && tunnelled.Count > 0) Untunnel();

        if (tunnelled.Count > 0)
        {
            // Reset before reading: a packet that queues while we read signals again.
            packetEvent.Reset();
            ReadAll(now, shape: true);
        }
        SendDue(Now);
        meter.Roll(Now);

        if (tunnelled.Count == 0) return 1000;
        double? due = shaper.NextDue(Now);
        return due is double d ? Math.Clamp((int)Math.Ceiling(d * 1000), 1, 250) : 250;
    }

    void TakeRules(double now)
    {
        List<LimitRule>? rules;
        lock (rulesGate)
        {
            rules = pendingRules;
            pendingRules = null;
        }
        if (rules == null) return;

        var wanted = new Dictionary<string, LimitRule>(StringComparer.OrdinalIgnoreCase);
        foreach (var r in rules)
            if (r.Enabled && !string.IsNullOrWhiteSpace(r.ExePath)) wanted[NormalizePath(r.ExePath)] = r;

        foreach (var (key, dir) in shaper.Limited())
            if (!wanted.TryGetValue(key, out var r) || !r.Limits(dir)) Forward(shaper.SetLimit(key, dir, 0, now));
        foreach (var (path, r) in wanted)
            foreach (var dir in (ReadOnlySpan<Direction>)[Direction.Download, Direction.Upload])
                if (r.Limits(dir)) shaper.SetLimit(path, dir, r.RateFor(dir), now);
        FlushSends();
    }

    static string NormalizePath(string path)
    {
        path = path.Trim().Trim('"');
        try { return Path.GetFullPath(path); }
        catch (Exception) { return path; }
    }

    /// <summary>Tunnels every TCP/IP adapter we do not have yet (a Wi-Fi reconnect or a VPN brings new ones) and forgets vanished ones.</summary>
    void ScanAdapters(double now)
    {
        lastScan = now;
        var d = driver!;
        var adapters = d.GetAdapters(friendlyNames: false);
        var seen = new HashSet<nint>();
        string? busy = null;
        foreach (var a in adapters)
        {
            seen.Add(a.Handle);
            if (tunnelled.ContainsKey(a.Handle)) continue;
            if (foreign.Contains(a.Handle)) { busy ??= a.Name; continue; }
            // Another WinpkFilter program (a VPN, say) owns this adapter; taking it over would break that program.
            uint? mode = d.GetMode(a.Handle);
            if (mode == null) continue;
            if (mode != 0)
            {
                foreign.Add(a.Handle);
                busy ??= a.Name;
                continue;
            }
            if (!d.SetPacketEvent(a.Handle, packetEvent.SafeWaitHandle.DangerousGetHandle())) continue;
            if (!d.SetMode(a.Handle, NdisConstants.TunnelMode))
            {
                d.SetPacketEvent(a.Handle, 0);
                continue;
            }
            tunnelled[a.Handle] = a;
        }
        foreach (var h in tunnelled.Keys.Where(h => !seen.Contains(h)).ToList()) tunnelled.Remove(h);
        foreign.RemoveWhere(h => !seen.Contains(h));
        tunnelledSnapshot = [.. tunnelled.Keys];

        if (busy != null)
        {
            string friendly = d.GetAdapters(friendlyNames: true).FirstOrDefault(a => a.Name == busy)?.FriendlyName ?? busy;
            warning = $"Another WinpkFilter program is using \"{friendly}\"; traffic there is not limited.";
        }
        else warning = tunnelled.Count == 0 && adapters.Count > 0 ? "No network adapter could be put under the limiter." : null;
    }

    /// <summary>Back to normal mode: no new packets come to us, and everything already queued in the driver or the shaper is sent on.</summary>
    void Untunnel()
    {
        var d = driver!;
        foreach (var h in tunnelled.Keys) d.SetMode(h, 0);
        ReadAll(Now, shape: false);
        Forward(shaper.ClearAll());
        FlushSends();
        foreach (var h in tunnelled.Keys)
        {
            d.SetPacketEvent(h, 0);
            d.FlushQueue(h);
        }
        tunnelled.Clear();
        foreign.Clear();
        tunnelledSnapshot = [];
        warning = null;
    }

    void ReadAll(double now, bool shape)
    {
        var d = driver!;
        while (true)
        {
            int cap = FillReadSlots();
            if (cap == 0)
            {
                // Every buffer is waiting in the shaper; the driver keeps the rest queued until some drain.
                warning = "The limiter is holding too many packets; some are waiting in the driver.";
                return;
            }
            int n = d.ReadUnsorted(readSlots, cap);
            if (n < 0)
            {
                if (++readFailures >= MaxReadFailures)
                    throw new Win32Exception(d.LastError, "Reading packets from WinpkFilter keeps failing.");
                return;
            }
            readFailures = 0;
            for (int i = 0; i < n; i++)
            {
                nint p = readSlots[i];
                readSlots[i] = 0;
                Handle(p, now, shape);
            }
            FlushSends();
            if (n < cap) return;
        }
    }

    /// <summary>Packs the buffers still in the read array to the front and tops it up from the pool; returns how many are ready.</summary>
    int FillReadSlots()
    {
        int k = 0;
        for (int i = 0; i < Batch; i++)
        {
            nint p = readSlots[i];
            if (p == 0) continue;
            readSlots[i] = 0;
            readSlots[k++] = p;
        }
        while (k < Batch)
        {
            nint p = pool!.Rent();
            if (p == 0) break;
            readSlots[k++] = p;
        }
        return k;
    }

    void Handle(nint p, double now, bool shape)
    {
        var h = (IntermediateBufferHeader*)p;
        int len = (int)h->Length;
        if (!shape || len <= 0 || len > NdisConstants.MaxJumboFrame)
        {
            Forward(p);
            return;
        }
        var dir = (h->DeviceFlags & NdisConstants.PacketFlagOnSend) != 0 ? Direction.Upload : Direction.Download;
        var frame = new ReadOnlySpan<byte>((byte*)p + IntermediateBufferHeader.Size, len);
        var owner = PacketParser.TryGetLocalEnd(frame, dir, out var key) ? resolver.Find(key) : null;
        if (owner == null)
        {
            Forward(p);
            return;
        }
        meter.Add(owner.Path, dir, len, now);
        switch (shaper.Offer(owner.Key, dir, p, len, now))
        {
            case ShapeDecision.Pass:
                Forward(p);
                break;
            case ShapeDecision.Dropped:
                meter.Drop(owner.Path, len);
                pool!.Return(p);
                break;
            // Queued: the shaper holds the buffer until Drain or a cleared limit gives it back.
        }
    }

    void SendDue(double now)
    {
        if (shaper.NextDue(now) is not double d || d > 0) return;
        Forward(shaper.Drain(now));
        FlushSends();
    }

    void Forward(List<(nint packet, Direction dir)> packets)
    {
        foreach (var (p, _) in packets) Forward(p);
    }

    /// <summary>Queues a packet to go on its way: one the stack sent goes out to the adapter, one that arrived goes up to the stack.</summary>
    void Forward(nint p)
    {
        var h = (IntermediateBufferHeader*)p;
        if ((h->DeviceFlags & NdisConstants.PacketFlagOnSend) != 0)
        {
            toAdapter[adapterCount++] = p;
            if (adapterCount == Batch) FlushSends();
        }
        else
        {
            toStack[stackCount++] = p;
            if (stackCount == Batch) FlushSends();
        }
    }

    /// <summary>Hands the queued packets to the driver. A failed batch may be partly sent already, so it is never retried; TCP recovers.</summary>
    void FlushSends()
    {
        var d = driver!;
        if (adapterCount > 0)
        {
            if (d.SendToAdapters(toAdapter, adapterCount) < 0) Debug.WriteLine($"Limiter send to adapter failed: {d.LastError}");
            for (int i = 0; i < adapterCount; i++) pool!.Return(toAdapter[i]);
            adapterCount = 0;
        }
        if (stackCount > 0)
        {
            if (d.SendToStack(toStack, stackCount) < 0) Debug.WriteLine($"Limiter send to stack failed: {d.LastError}");
            for (int i = 0; i < stackCount; i++) pool!.Return(toStack[i]);
            stackCount = 0;
        }
    }

    /// <summary>
    /// Last resort from any thread: puts every adapter we tunnelled back to normal mode with no locks.
    /// Packets still held are lost, which TCP retransmits; the point is that the network never stays stuck.
    /// </summary>
    void EmergencyRestore()
    {
        var d = driver;
        if (d == null) return;
        foreach (var h in tunnelledSnapshot)
        {
            try
            {
                d.SetMode(h, 0);
                d.SetPacketEvent(h, 0);
            }
            catch (Exception) { }
        }
    }

    void OnProcessExit(object? sender, EventArgs e) => Stop();

    void OnUnhandled(object? sender, UnhandledExceptionEventArgs e) => EmergencyRestore();

    /// <summary>Stops limiting, sends on every held packet, restores adapters and closes the driver. Safe to call more than once, from any thread.</summary>
    public void Stop()
    {
        if (Interlocked.Exchange(ref stopped, 1) != 0) return;
        if (hooked)
        {
            AppDomain.CurrentDomain.ProcessExit -= OnProcessExit;
            AppDomain.CurrentDomain.UnhandledException -= OnUnhandled;
        }
        bool clean = true;
        if (worker != null)
        {
            running = false;
            wake.Set();
            if (Thread.CurrentThread != worker && !worker.Join(3000)) clean = false;
            if (clean && driver != null)
            {
                lock (ioGate)
                {
                    try
                    {
                        if (tunnelled.Count > 0) Untunnel();
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex);
                        EmergencyRestore();
                    }
                }
            }
            else EmergencyRestore();
        }
        resolver.Dispose();
        driver?.Dispose();
        if (!clean) return; // the worker may still touch the buffers; the process is going away anyway
        pool?.Dispose();
        NativeMemory.Free(readSlots);
        NativeMemory.Free(toAdapter);
        NativeMemory.Free(toStack);
        readSlots = toAdapter = toStack = null;
        packetEvent.Dispose();
        wake.Dispose();
    }

    public void Dispose() => Stop();
}
