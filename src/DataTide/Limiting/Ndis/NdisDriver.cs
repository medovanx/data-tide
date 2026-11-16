using System.ComponentModel;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace DataTide.Limiting.Ndis;

/// <summary>
/// Talks to the WinpkFilter driver (ndisrd.sys) through its device with the same IOCTLs ndisapi.dll sends, so we ship
/// no third-party DLL. Ported from wiresock/ndisapi tag v3.6.2 (commit ca8726c): include/Common.h for codes and layouts,
/// ndisapi/ndisapi.cpp for how each call is made (open at line 764, GetVersion 875, GetTcpipBoundAdaptersInfo 904,
/// SetAdapterMode 1245, GetAdapterMode 1303, FlushAdapterPacketQueue 1363, SetPacketEvent 1484, the unsorted calls 417 and 2756 to 2823).
/// 64-bit processes only: a 32-bit one would need the _WOW64 struct variants, and the unsorted IOCTLs refuse WOW64.
/// </summary>
/// <remarks>
/// What happens when our handle closes: the driver source is not public, but the SDK's lifecycle test
/// (tests/fastio_lifecycle/r5_driver_bridge.c, which links the driver's real LWF/lifecycle.c) has to stub
/// EXT_ResetRDForAllAdapters and FLT_ResetStaticFilters for the driver's close handler, and counts open clients.
/// So the driver resets every adapter to normal mode on close, most likely only when the LAST open handle closes.
/// This is inferred, not verified: if another WinpkFilter program keeps a handle open, a killed helper could leave
/// adapters tunnelled until that program closes. That is why the engine also resets modes itself on every exit path.
/// </remarks>
sealed unsafe class NdisDriver : IDisposable
{
    public const string DevicePath = @"\\.\NDISRD";

    /// <summary>NDISRD_VERSION in the v3.2.24 SDK, the oldest tagged release whose Common.h already defines the unsorted IOCTLs and today's INTERMEDIATE_BUFFER.</summary>
    public const uint MinVersion = 0x02143000;

    /// <summary>Every packet buffer is the header plus a jumbo frame, so a driver built with JUMBO_FRAME_SUPPORTED cannot overrun it.</summary>
    public const int BufferSize = IntermediateBufferHeader.Size + NdisConstants.MaxJumboFrame;

    readonly SafeFileHandle device;

    /// <summary>The raw value of IOCTL_NDISRD_GET_VERSION, e.g. 0x06023000 for 3.6.2.</summary>
    public uint RawVersion { get; }

    public string VersionText => Describe(RawVersion);

    /// <summary>The Win32 error of the last failed call, for logging.</summary>
    public int LastError { get; private set; }

    NdisDriver(SafeFileHandle device, uint version)
    {
        this.device = device;
        RawVersion = version;
    }

    /// <summary>True when the driver is loaded and new enough. Opening the device needs admin rights.</summary>
    public static bool IsAvailable
    {
        get
        {
            using var d = TryOpen(out _);
            return d != null;
        }
    }

    /// <summary>Opens the driver and checks its version; null with a reason when that is not possible.</summary>
    public static NdisDriver? TryOpen(out string? error)
    {
        error = null;
        CheckLayout();
        if (!Environment.Is64BitProcess)
        {
            error = "The speed limiter needs the 64-bit build of Data Tide.";
            return null;
        }
        // Same as the SDK (ndisapi.cpp line 764) but without FILE_FLAG_OVERLAPPED: none of the calls we use pend, so plain synchronous I/O is enough.
        var h = NdisNative.CreateFile(DevicePath, 0, NdisNative.FileShareRead | NdisNative.FileShareWrite, 0, NdisNative.OpenExisting, 0, 0);
        if (h.IsInvalid)
        {
            int e = Marshal.GetLastPInvokeError();
            h.Dispose();
            error = e is 2 or 3
                ? "The WinpkFilter driver is not installed or not running."
                : $"Could not open the WinpkFilter driver: {new Win32Exception(e).Message}";
            return null;
        }
        uint v = 0xFFFFFFFF;
        bool ok = NdisNative.DeviceIoControl(h, NdisIoctl.GetVersion, &v, sizeof(uint), &v, sizeof(uint), out _, 0);
        if (!ok || v == 0xFFFFFFFF)
        {
            h.Dispose();
            error = "The WinpkFilter driver did not report its version.";
            return null;
        }
        if (Order(v) < Order(MinVersion))
        {
            h.Dispose();
            error = $"WinpkFilter {Describe(v)} is too old; install 3.6.2 or newer.";
            return null;
        }
        return new NdisDriver(h, v);
    }

    /// <summary>The version keeps major in the low word (0x3000) and minor/patch in the high word, so swap them to compare.</summary>
    static ulong Order(uint v) => ((ulong)(v & 0xFFFF) << 16) | (v >> 16);

    static string Describe(uint v) => $"{(v >> 12) & 0xF}.{(v >> 24) & 0xFF}.{(v >> 16) & 0xFF}";

    /// <summary>A wrong struct size would corrupt the driver call silently, so refuse to run instead.</summary>
    static void CheckLayout()
    {
        if (sizeof(TcpAdapterList) != TcpAdapterList.Size || sizeof(IntermediateBufferHeader) != IntermediateBufferHeader.Size
            || sizeof(AdapterMode) != AdapterMode.Size || sizeof(AdapterEvent) != AdapterEvent.Size || sizeof(UnsortedRequest) != UnsortedRequest.Size)
            throw new InvalidOperationException("WinpkFilter struct layout mismatch.");
    }

    bool Call(uint code, void* input, int inSize, void* output, int outSize, out int returned)
    {
        if (NdisNative.DeviceIoControl(device, code, input, inSize, output, outSize, out returned, 0)) return true;
        LastError = Marshal.GetLastPInvokeError();
        return false;
    }

    /// <summary>The adapters bound to TCP/IP. Friendly names come from Windows by matching the GUID in the driver's adapter name.</summary>
    public List<NdisAdapter> GetAdapters(bool friendlyNames)
    {
        var list = new TcpAdapterList();
        if (!Call(NdisIoctl.GetTcpipInterfaces, &list, TcpAdapterList.Size, &list, TcpAdapterList.Size, out int got) || got != TcpAdapterList.Size)
            throw new Win32Exception(LastError, "Could not list the network adapters from WinpkFilter.");

        Dictionary<string, string>? names = null;
        if (friendlyNames)
        {
            names = new(StringComparer.OrdinalIgnoreCase);
            try
            {
                foreach (var ni in NetworkInterface.GetAllNetworkInterfaces()) names.TryAdd(ni.Id, ni.Name);
            }
            catch (NetworkInformationException) { }
        }

        var result = new List<NdisAdapter>();
        int count = (int)Math.Min(list.AdapterCount, (uint)NdisConstants.AdapterListSize);
        for (int i = 0; i < count; i++)
        {
            var raw = new ReadOnlySpan<byte>(list.AdapterNameList + i * NdisConstants.AdapterNameSize, NdisConstants.AdapterNameSize);
            int end = raw.IndexOf((byte)0);
            string name = Encoding.ASCII.GetString(end < 0 ? raw : raw[..end]);
            string id = name[(name.LastIndexOf('\\') + 1)..];
            string friendly = names != null && names.TryGetValue(id, out var f) ? f : name;
            var mac = new ReadOnlySpan<byte>(list.CurrentAddress + i * NdisConstants.EtherAddrLength, NdisConstants.EtherAddrLength);
            result.Add(new NdisAdapter((nint)list.AdapterHandle[i], name, friendly, list.AdapterMediumList[i], Convert.ToHexString(mac), list.Mtu[i]));
        }
        return result;
    }

    /// <summary>The adapter's current mode flags, or null when the call failed.</summary>
    public uint? GetMode(nint adapter)
    {
        var m = new AdapterMode { Adapter = adapter };
        return Call(NdisIoctl.GetAdapterMode, &m, AdapterMode.Size, &m, AdapterMode.Size, out _) ? m.Flags : null;
    }

    /// <summary>Sets the adapter's mode; 0 puts it back to normal, <see cref="NdisConstants.TunnelMode"/> hands us every packet.</summary>
    public bool SetMode(nint adapter, uint flags)
    {
        var m = new AdapterMode { Adapter = adapter, Flags = flags };
        return Call(NdisIoctl.SetAdapterMode, &m, AdapterMode.Size, null, 0, out _);
    }

    /// <summary>Registers (or with 0 removes) the event the driver signals when this adapter has packets for us. The event needs SYNCHRONIZE access.</summary>
    public bool SetPacketEvent(nint adapter, nint win32Event)
    {
        var e = new AdapterEvent { Adapter = adapter, Event = win32Event };
        return Call(NdisIoctl.SetEvent, &e, AdapterEvent.Size, null, 0, out _);
    }

    /// <summary>Drops whatever the driver still queues for this adapter.</summary>
    public bool FlushQueue(nint adapter) => Call(NdisIoctl.FlushAdapterQueue, &adapter, sizeof(nint), null, 0, out _);

    /// <summary>
    /// Reads up to <paramref name="capacity"/> queued packets from all adapters into the given buffers
    /// (each <see cref="BufferSize"/> bytes, not movable). Returns how many were filled, or -1 on failure.
    /// </summary>
    public int ReadUnsorted(nint* buffers, int capacity)
    {
        var r = new UnsortedRequest { Packets = (nint)buffers, Count = (uint)capacity };
        if (!Call(NdisIoctl.ReadPacketsUnsorted, &r, UnsortedRequest.Size, &r, UnsortedRequest.Size, out int got)) return -1;
        // The SDK's check (ndisapi.cpp line 441): the count must have been written back and fit the array.
        if (got < UnsortedRequest.Size || r.Count > (uint)capacity) { LastError = 13; return -1; }
        return (int)r.Count;
    }

    /// <summary>Sends packets out to the adapters named in their m_hAdapter. Returns the accepted tally or -1; a failed batch may be partly sent, so never retry it.</summary>
    public int SendToAdapters(nint* buffers, int count) => Send(NdisIoctl.SendPacketToAdapterUnsorted, buffers, count);

    /// <summary>Indicates packets up to the TCP/IP stack as if they had just arrived. Same tally rules as <see cref="SendToAdapters"/>.</summary>
    public int SendToStack(nint* buffers, int count) => Send(NdisIoctl.SendPacketToMstcpUnsorted, buffers, count);

    int Send(uint code, nint* buffers, int count)
    {
        if (count <= 0) return 0;
        var r = new UnsortedRequest { Packets = (nint)buffers, Count = (uint)count };
        return Call(code, &r, UnsortedRequest.Size, &r, UnsortedRequest.Size, out _) ? (int)Math.Min(r.Count, (uint)count) : -1;
    }

    public void Dispose() => device.Dispose();
}
