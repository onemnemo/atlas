using System.Globalization;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Atlas.Hardware;

/// <summary>
/// Physical-memory readings used for tier classification.
/// </summary>
/// <param name="TotalBytes">Total physical RAM in bytes.</param>
/// <param name="AvailableBytes">Currently available physical RAM in bytes.</param>
public readonly record struct MemoryReading(long TotalBytes, long AvailableBytes);

/// <summary>
/// Best-effort, cross-platform physical-memory detection.
/// </summary>
/// <remarks>
/// Each platform has a dedicated probe; anything unrecognised falls back to the
/// runtime's GC memory info. Probes never throw — on any failure they return the
/// conservative fallback, because under-reporting memory only makes Atlas behave
/// more cautiously (arch §23).
/// </remarks>
internal static partial class SystemMemoryProbe
{
    public static MemoryReading Read()
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                return ReadWindows();
            }

            if (OperatingSystem.IsLinux())
            {
                return ReadLinux();
            }
        }
        catch
        {
            // Fall through to the runtime fallback below; detection must not throw.
        }

        return ReadFallback();
    }

    private static MemoryReading ReadFallback()
    {
        // GCMemoryInfo.TotalAvailableMemoryBytes reflects the physical memory
        // limit (or container/cgroup limit) the runtime sees. It is a coarse but
        // safe source when no OS-specific probe is available.
        long total = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
        return new MemoryReading(total, total);
    }

    [SupportedOSPlatform("windows")]
    private static MemoryReading ReadWindows()
    {
        var status = new MemoryStatusEx { Length = (uint)Marshal.SizeOf<MemoryStatusEx>() };
        if (NativeMethods.GlobalMemoryStatusEx(ref status))
        {
            return new MemoryReading((long)status.TotalPhys, (long)status.AvailPhys);
        }

        return ReadFallback();
    }

    [SupportedOSPlatform("linux")]
    private static MemoryReading ReadLinux()
    {
        long totalKb = 0;
        long availableKb = 0;

        foreach (string line in File.ReadLines("/proc/meminfo"))
        {
            if (TryParseMemInfoLine(line, "MemTotal:", out long total))
            {
                totalKb = total;
            }
            else if (TryParseMemInfoLine(line, "MemAvailable:", out long available))
            {
                availableKb = available;
            }

            if (totalKb > 0 && availableKb > 0)
            {
                break;
            }
        }

        return totalKb > 0
            ? new MemoryReading(totalKb * 1024, availableKb * 1024)
            : ReadFallback();
    }

    private static bool TryParseMemInfoLine(string line, string prefix, out long valueKb)
    {
        valueKb = 0;
        if (!line.StartsWith(prefix, StringComparison.Ordinal))
        {
            return false;
        }

        // Format: "MemTotal:       16384256 kB"
        ReadOnlySpan<char> rest = line.AsSpan(prefix.Length).Trim();
        int spaceIndex = rest.IndexOf(' ');
        if (spaceIndex > 0)
        {
            rest = rest[..spaceIndex];
        }

        return long.TryParse(rest, NumberStyles.Integer, CultureInfo.InvariantCulture, out valueKb);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatusEx
    {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhys;
        public ulong AvailPhys;
        public ulong TotalPageFile;
        public ulong AvailPageFile;
        public ulong TotalVirtual;
        public ulong AvailVirtual;
        public ulong AvailExtendedVirtual;
    }

    private static partial class NativeMethods
    {
        [SupportedOSPlatform("windows")]
        [LibraryImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool GlobalMemoryStatusEx(ref MemoryStatusEx lpBuffer);
    }
}
