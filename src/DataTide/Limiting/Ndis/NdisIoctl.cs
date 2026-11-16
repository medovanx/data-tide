namespace DataTide.Limiting.Ndis;

/// <summary>
/// The driver's IOCTL codes, built with CTL_CODE exactly like include/Common.h lines 908 to 993 (v3.6.2):
/// device type FILE_DEVICE_NDISRD 0x8300, function NDISRD_IOCTL_INDEX 0x830 + n, METHOD_BUFFERED, FILE_ANY_ACCESS.
/// GET_VERSION comes out as 0x830020C0 and each later index adds 4.
/// </summary>
static class NdisIoctl
{
    const uint FileDeviceNdisrd = 0x00008300;
    const uint IoctlIndex = 0x830;
    const uint MethodBuffered = 0;
    const uint FileAnyAccess = 0;

    static uint Code(uint n) => (FileDeviceNdisrd << 16) | (FileAnyAccess << 14) | ((IoctlIndex + n) << 2) | MethodBuffered;

    /// <summary>IOCTL_NDISRD_GET_VERSION, index +0 (Common.h line 911).</summary>
    public static readonly uint GetVersion = Code(0);
    /// <summary>IOCTL_NDISRD_GET_TCPIP_INTERFACES, index +1 (Common.h line 914).</summary>
    public static readonly uint GetTcpipInterfaces = Code(1);
    /// <summary>IOCTL_NDISRD_SET_ADAPTER_MODE, index +5 (Common.h line 926).</summary>
    public static readonly uint SetAdapterMode = Code(5);
    /// <summary>IOCTL_NDISRD_FLUSH_ADAPTER_QUEUE, index +6 (Common.h line 929).</summary>
    public static readonly uint FlushAdapterQueue = Code(6);
    /// <summary>IOCTL_NDISRD_SET_EVENT, index +7 (Common.h line 932).</summary>
    public static readonly uint SetEvent = Code(7);
    /// <summary>IOCTL_NDISRD_GET_ADAPTER_MODE, index +13 (Common.h line 950).</summary>
    public static readonly uint GetAdapterMode = Code(13);
    /// <summary>IOCTL_NDISRD_READ_PACKETS_UNSORTED, index +25 (Common.h line 986).</summary>
    public static readonly uint ReadPacketsUnsorted = Code(25);
    /// <summary>IOCTL_NDISRD_SEND_PACKET_TO_ADAPTER_UNSORTED, index +26 (Common.h line 989).</summary>
    public static readonly uint SendPacketToAdapterUnsorted = Code(26);
    /// <summary>IOCTL_NDISRD_SEND_PACKET_TO_MSTCP_UNSORTED, index +27 (Common.h line 992).</summary>
    public static readonly uint SendPacketToMstcpUnsorted = Code(27);
}
