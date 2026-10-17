using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace OmsiLaunch.Modules.Prefetch;

// The Win32 calls the preloader needs beyond the framework: read-only file
// mappings whose file handle is closed at once (the section keeps the data;
// the game's own opens then never meet a sharing conflict), page prefetch,
// memory status, background I/O priority and the pipe client's process id.
internal static unsafe class Native
{
    public const uint GenericRead = 0x80000000;
    public const uint FileShareAll = 0x7;   // read | write | delete
    public const uint OpenExisting = 3;
    public const uint FileAttributeNormal = 0x80;
    public const uint FileAttributeDirectory = 0x10;
    public const uint PageReadonly = 0x02;
    public const uint FileMapRead = 0x0004;
    public const int ThreadModeBackgroundBegin = 0x00010000, ThreadModeBackgroundEnd = 0x00020000;

    [StructLayout(LayoutKind.Sequential)]
    public struct MemoryStatusEx
    {
        public uint Length, MemoryLoad;
        public ulong TotalPhys, AvailPhys, TotalPageFile, AvailPageFile, TotalVirtual, AvailVirtual, AvailExtendedVirtual;
    }

    // FILETIME is two DWORDs: 4-byte packing keeps the native layout.
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct FileAttributeData
    {
        public uint FileAttributes;
        public long CreationTime, LastAccessTime, LastWriteTime;
        public uint FileSizeHigh, FileSizeLow;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryRangeEntry { public void* VirtualAddress; public nuint NumberOfBytes; }

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern SafeFileHandle CreateFileW(string name, uint access, uint share, nint security, uint disposition, uint flags, nint template);
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern nint CreateFileMappingW(SafeFileHandle file, nint security, uint protect, uint sizeHigh, uint sizeLow, string? name);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern void* MapViewOfFile(nint mapping, uint access, uint offsetHigh, uint offsetLow, nuint bytes);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool UnmapViewOfFile(void* address);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(nint handle);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool PrefetchVirtualMemory(nint process, nuint count, MemoryRangeEntry* ranges, uint flags);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx status);
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool GetFileAttributesExW(string name, int level, out FileAttributeData data);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetProcessWorkingSetSizeEx(nint process, nuint minimum, nuint maximum, uint flags);
    [DllImport("kernel32.dll")]
    private static extern nint GetCurrentProcess();
    [DllImport("kernel32.dll")]
    private static extern nint GetCurrentThread();
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetThreadPriority(nint thread, int priority);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetNamedPipeClientProcessId(SafePipeHandle pipe, out uint processId);

    public static MemoryStatusEx Memory()
    {
        var status = new MemoryStatusEx { Length = (uint)sizeof(MemoryStatusEx) };
        GlobalMemoryStatusEx(ref status);
        return status;
    }

    public static long AvailableMb() => (long)(Memory().AvailPhys >> 20);

    // Size of a file, or -1 when the path is not a file.
    public static long FileSize(string path)
    {
        if (!GetFileAttributesExW(path, 0, out var data) || (data.FileAttributes & FileAttributeDirectory) != 0) return -1;
        return ((long)data.FileSizeHigh << 32) | data.FileSizeLow;
    }

    // Asks Windows to keep what the process holds resident (best effort: the
    // minimum is soft, so memory pressure can still take pages back).
    public static bool KeepResident(long bytes)
    {
        const uint minimumDisabled = 0x2;   // QUOTA_LIMITS_HARDWS_MIN_DISABLE
        var minimum = (nuint)(bytes + (256L << 20));
        return SetProcessWorkingSetSizeEx(GetCurrentProcess(), minimum, minimum + (256u << 20), minimumDisabled);
    }

    public static void BackgroundIo(bool on) => SetThreadPriority(GetCurrentThread(), on ? ThreadModeBackgroundBegin : ThreadModeBackgroundEnd);

    public static int? PipeClientProcessId(SafePipeHandle pipe) => GetNamedPipeClientProcessId(pipe, out var id) ? (int)id : null;

    // A file mapped read-only and touched page by page.
    public sealed class View
    {
        private void* address;
        private nint mapping;
        public long Size { get; }
        private View(void* address, nint mapping, long size) { this.address = address; this.mapping = mapping; Size = size; }

        // Null when the file cannot be opened or mapped (error says why).
        public static View? Map(string path, out long size, out int error)
        {
            size = -1; error = 0;
            using var file = CreateFileW(path, GenericRead, FileShareAll, 0, OpenExisting, FileAttributeNormal, 0);
            if (file.IsInvalid) { error = Marshal.GetLastWin32Error(); return null; }
            if (!GetFileSizeEx(file, out size) || size <= 0) { error = size == 0 ? 0 : Marshal.GetLastWin32Error(); return null; }
            var mapping = CreateFileMappingW(file, 0, PageReadonly, 0, 0, null);
            if (mapping == 0) { error = Marshal.GetLastWin32Error(); return null; }
            var address = MapViewOfFile(mapping, FileMapRead, 0, 0, 0);
            if (address == null) { error = Marshal.GetLastWin32Error(); CloseHandle(mapping); return null; }
            return new View(address, mapping, size);
        }

        // Brings every page in: one prefetch request, then one read per page.
        public void Touch()
        {
            var range = new MemoryRangeEntry { VirtualAddress = address, NumberOfBytes = (nuint)Size };
            PrefetchVirtualMemory(GetCurrentProcess(), 1, &range, 0);
            var bytes = (byte*)address;
            byte sink = 0;
            for (long offset = 0; offset < Size; offset += 4096) sink ^= bytes[offset];
            GC.KeepAlive(sink);
        }

        public void Release()
        {
            if (address != null) { UnmapViewOfFile(address); address = null; }
            if (mapping != 0) { CloseHandle(mapping); mapping = 0; }
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetFileSizeEx(SafeFileHandle file, out long size);

    public static Exception LastError(string what) => new Win32Exception(Marshal.GetLastWin32Error(), what);
}
