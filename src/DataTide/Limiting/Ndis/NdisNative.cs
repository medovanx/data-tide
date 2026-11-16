using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace DataTide.Limiting.Ndis;

/// <summary>The two Win32 calls the driver needs: open its device and send it IOCTLs.</summary>
static unsafe class NdisNative
{
    public const uint FileShareRead = 0x1, FileShareWrite = 0x2, OpenExisting = 3;

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "CreateFileW")]
    public static extern SafeFileHandle CreateFile(string name, uint access, uint share, nint security, uint disposition, uint flags, nint template);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool DeviceIoControl(SafeFileHandle device, uint code, void* inBuffer, int inSize,
        void* outBuffer, int outSize, out int returned, nint overlapped);
}
