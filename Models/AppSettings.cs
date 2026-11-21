namespace PulseBoost.Models;

public enum BoostModeType
{
    Gaming,
    LocalAIAndWork
}

public class AppTargetItem
{
    public string ProcessName { get; set; } = string.Empty;
    public string FriendlyName { get; set; } = string.Empty;
    public bool ShouldCloseOnBoost { get; set; }
    public bool ShouldLowerPriority { get; set; } = true;
}

public class AppSettings
{
    public int SelectedModeIndex { get; set; } = 0;
    public bool IsAutoDetectEnabled { get; set; } = true;
    public bool MinimizeToTrayOnClose { get; set; } = true;
    public bool AutoCleanRamOnHighUsage { get; set; } = true;
    public int AutoCleanRamThresholdPercent { get; set; } = 80;
    public List<string> WatchedExecutables { get; set; } = new()
    {
        "ollama.exe",
        "LM Studio.exe",
        "Cyberpunk2077.exe",
        "VALORANT-Win64-Shipping.exe",
        "cs2.exe"
    };
    public List<AppTargetItem> GamingTargets { get; set; } = new();
    public List<AppTargetItem> LocalAITargets { get; set; } = new();
}
