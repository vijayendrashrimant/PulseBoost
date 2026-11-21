using System.Diagnostics;
using System.IO;
using PulseBoost.Models;

namespace PulseBoost.Services;

public class ProcessBoostService
{
    private readonly List<string> _closedAppPaths = new();
    private readonly Dictionary<int, ProcessPriorityClass> _originalPriorities = new();

    public (int appsOptimized, long freedMegabytes) ApplyBoost(IEnumerable<AppTargetItem> targets, string? activeTargetExe = null)
    {
        int count = 0;
        long freedBytes = 0;

        foreach (var target in targets)
        {
            foreach (var proc in Process.GetProcessesByName(target.ProcessName))
            {
                try
                {
                    if (target.ShouldCloseOnBoost)
                    {
                        freedBytes += proc.WorkingSet64;
                        string? exePath = proc.MainModule?.FileName;
                        if (!string.IsNullOrEmpty(exePath) && !_closedAppPaths.Contains(exePath))
                            _closedAppPaths.Add(exePath);

                        proc.CloseMainWindow();
                        count++;
                    }
                    else if (target.ShouldLowerPriority)
                    {
                        _originalPriorities[proc.Id] = proc.PriorityClass;
                        proc.PriorityClass = ProcessPriorityClass.BelowNormal;
                        count++;
                    }
                }
                catch { }
            }
        }

        if (!string.IsNullOrWhiteSpace(activeTargetExe))
        {
            string cleanName = Path.GetFileNameWithoutExtension(activeTargetExe);
            foreach (var proc in Process.GetProcessesByName(cleanName))
            {
                try
                {
                    _originalPriorities[proc.Id] = proc.PriorityClass;
                    proc.PriorityClass = ProcessPriorityClass.High;
                }
                catch { }
            }
        }

        return (count, freedBytes / (1024 * 1024));
    }

    public void RestoreAll()
    {
        foreach (var kvp in _originalPriorities)
        {
            try
            {
                var proc = Process.GetProcessById(kvp.Key);
                proc.PriorityClass = kvp.Value;
            }
            catch { }
        }
        _originalPriorities.Clear();

        foreach (var path in _closedAppPaths)
        {
            try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); }
            catch { }
        }
        _closedAppPaths.Clear();
    }
}
