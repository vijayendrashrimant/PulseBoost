using System.Diagnostics;
using System.Runtime.InteropServices;

namespace PulseBoost.Services;

public class MemoryBoostService
{
    private const int SystemMemoryListInformation = 80;
    private const int MemoryPurgeStandbyList = 4;

    public long OptimizeSystemRam()
    {
        long beforeAvailableBytes = GetAvailablePhysicalMemory();

        // 1. Trim working sets of running background processes
        foreach (var proc in Process.GetProcesses())
        {
            try
            {
                if (proc.Id <= 4 || proc.HasExited) continue;
                EmptyWorkingSet(proc.Handle);
            }
            catch { /* Skip protected system/anti-cheat processes */ }
            finally
            {
                proc.Dispose();
            }
        }

        // 2. Purge Windows Standby RAM Cache (requires Admin + Privilege)
        PurgeStandbyList();

        long afterAvailableBytes = GetAvailablePhysicalMemory();
        long freedBytes = Math.Max(0, afterAvailableBytes - beforeAvailableBytes);
        return freedBytes / (1024 * 1024);
    }

    private static void PurgeStandbyList()
    {
        try
        {
            if (EnablePrivilege("SeProfileSingleProcessPrivilege"))
            {
                int command = MemoryPurgeStandbyList;
                int size = Marshal.SizeOf(command);
                IntPtr ptr = Marshal.AllocHGlobal(size);
                try
                {
                    Marshal.WriteInt32(ptr, command);
                    NtSetSystemInformation(SystemMemoryListInformation, ptr, size);
                }
                finally
                {
                    Marshal.FreeHGlobal(ptr);
                }
            }
        }
        catch { }
    }

    private static bool EnablePrivilege(string privilegeName)
    {
        if (!OpenProcessToken(Process.GetCurrentProcess().Handle, 0x0020 | 0x0008, out IntPtr tokenHandle))
            return false;

        try
        {
            if (!LookupPrivilegeValue(null, privilegeName, out LUID luid))
                return false;

            var tp = new TOKEN_PRIVILEGES
            {
                PrivilegeCount = 1,
                Luid = luid,
                Attributes = 0x00000002 // SE_PRIVILEGE_ENABLED
            };

            return AdjustTokenPrivileges(tokenHandle, false, ref tp, 0, IntPtr.Zero, IntPtr.Zero);
        }
        finally
        {
            CloseHandle(tokenHandle);
        }
    }

    private static long GetAvailablePhysicalMemory()
    {
        var mem = new MEMORYSTATUSEX();
        return GlobalMemoryStatusEx(mem) ? (long)mem.ullAvailPhys : 0;
    }

    [DllImport("psapi.dll", SetLastError = true)]
    private static extern bool EmptyWorkingSet(IntPtr hProcess);

    [DllImport("ntdll.dll", SetLastError = true)]
    private static extern int NtSetSystemInformation(int SystemInformationClass, IntPtr SystemInformation, int SystemInformationLength);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool OpenProcessToken(IntPtr ProcessHandle, uint DesiredAccess, out IntPtr TokenHandle);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern bool LookupPrivilegeValue(string? lpSystemName, string lpName, out LUID lpLuid);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool AdjustTokenPrivileges(IntPtr TokenHandle, bool DisableAllPrivileges, ref TOKEN_PRIVILEGES NewState, int BufferLength, IntPtr PreviousState, IntPtr ReturnLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern bool GlobalMemoryStatusEx([In, Out] MEMORYSTATUSEX lpBuffer);

    [StructLayout(LayoutKind.Sequential)]
    private struct LUID { public uint LowPart; public int HighPart; }

    [StructLayout(LayoutKind.Sequential)]
    private struct TOKEN_PRIVILEGES
    {
        public int PrivilegeCount;
        public LUID Luid;
        public uint Attributes;
    }

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
}
