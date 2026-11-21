using System.Diagnostics;
using System.Runtime.InteropServices;

namespace PulseBoost.Services;

public class TelemetryService : IDisposable
{
    private readonly PerformanceCounter? _cpuCounter;

    public TelemetryService()
    {
        try
        {
            _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
            _cpuCounter.NextValue();
        }
        catch { }
    }

    public (double cpuPercent, double usedRamGb, double totalRamGb, double ramPercent) GetSystemMetrics()
    {
        double cpu = 0;
        try { cpu = Math.Round(_cpuCounter?.NextValue() ?? 0, 1); } catch { }

        var memStatus = new MEMORYSTATUSEX();
        if (GlobalMemoryStatusEx(memStatus))
        {
            double totalGb = memStatus.ullTotalPhys / (1024.0 * 1024 * 1024);
            double availGb = memStatus.ullAvailPhys / (1024.0 * 1024 * 1024);
            double usedGb = totalGb - availGb;
            double percent = (usedGb / totalGb) * 100.0;
            return (cpu, Math.Round(usedGb, 1), Math.Round(totalGb, 1), Math.Round(percent, 1));
        }

        return (cpu, 0, 0, 0);
    }

    public void Dispose() => _cpuCounter?.Dispose();

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private class MEMORYSTATUSEX
    {
        public uint dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [return: MarshalAs(UnmanagedType.Bool)]
    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern bool GlobalMemoryStatusEx([In, Out] MEMORYSTATUSEX lpBuffer);
}
