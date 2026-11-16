using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace DataTide.Limiting;

/// <summary>Win32 calls the limiter needs: connection tables, process image paths and tokens, and named pipe peers.</summary>
static unsafe class LimiterNative
{
    public const int AfInet = 2, AfInet6 = 23;
    /// <summary>TCP_TABLE_OWNER_PID_ALL: listeners and connections, with the owning process id.</summary>
    public const int TcpTableOwnerPidAll = 5;
    /// <summary>UDP_TABLE_OWNER_PID.</summary>
    public const int UdpTableOwnerPid = 1;
    public const uint ErrorInsufficientBuffer = 122;
    public const uint ProcessQueryLimitedInformation = 0x1000;
    public const uint TokenQuery = 0x0008;

    [DllImport("iphlpapi.dll")]
    public static extern uint GetExtendedTcpTable(void* table, ref int size, bool order, int af, int tableClass, uint reserved);

    [DllImport("iphlpapi.dll")]
    public static extern uint GetExtendedUdpTable(void* table, ref int size, bool order, int af, int tableClass, uint reserved);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern SafeProcessHandle OpenProcess(uint access, bool inherit, int pid);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "QueryFullProcessImageNameW")]
    public static extern bool QueryFullProcessImageName(SafeProcessHandle process, uint flags, char* name, ref int size);

    [DllImport("advapi32.dll", SetLastError = true)]
    public static extern bool OpenProcessToken(SafeProcessHandle process, uint access, out SafeAccessTokenHandle token);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool GetNamedPipeClientProcessId(SafeHandle pipe, out int pid);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool GetNamedPipeServerProcessId(SafeHandle pipe, out int pid);
}
