using System.Diagnostics;
using System.Text.RegularExpressions;

namespace PulseBoost.Services;

public class PowerPlanService
{
    private string? _previousPowerPlanGuid;
    private const string HighPerformanceGuid = "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c";

    public void ActivateHighPerformance()
    {
        _previousPowerPlanGuid = GetCurrentPowerPlanGuid();
        RunPowerCfg($"/setactive {HighPerformanceGuid}");
    }

    public void RestorePreviousPlan()
    {
        if (!string.IsNullOrWhiteSpace(_previousPowerPlanGuid))
        {
            RunPowerCfg($"/setactive {_previousPowerPlanGuid}");
        }
    }

    private string? GetCurrentPowerPlanGuid()
    {
        var output = RunPowerCfg("/getactivescheme");
        var match = Regex.Match(output, @"([a-fA-F0-9]{8}-[a-fA-F0-9]{4}-[a-fA-F0-9]{4}-[a-fA-F0-9]{4}-[a-fA-F0-9]{12})");
        return match.Success ? match.Groups[1].Value : null;
    }

    private static string RunPowerCfg(string arguments)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "powercfg.exe",
                Arguments = arguments,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };
        process.Start();
        string result = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        return result;
    }
}
