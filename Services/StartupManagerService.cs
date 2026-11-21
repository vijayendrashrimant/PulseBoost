using Microsoft.Win32;

namespace PulseBoost.Services;

public class StartupAppItem
{
    public string Name { get; set; } = string.Empty;
    public string Command { get; set; } = string.Empty;
    public string FriendlyImpactLabel { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
}

public class StartupManagerService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string DisabledKeyPath = @"Software\PulseBoost\DisabledStartup";

    public List<StartupAppItem> GetStartupItems()
    {
        var items = new List<StartupAppItem>();

        using (var runKey = Registry.CurrentUser.OpenSubKey(RunKeyPath, false))
        {
            if (runKey != null)
            {
                foreach (string name in runKey.GetValueNames())
                {
                    string cmd = runKey.GetValue(name)?.ToString() ?? string.Empty;
                    items.Add(new StartupAppItem
                    {
                        Name = name,
                        Command = cmd,
                        FriendlyImpactLabel = GetPlainEnglishAdvice(name),
                        IsEnabled = true
                    });
                }
            }
        }

        using (var disabledKey = Registry.CurrentUser.OpenSubKey(DisabledKeyPath, false))
        {
            if (disabledKey != null)
            {
                foreach (string name in disabledKey.GetValueNames())
                {
                    string cmd = disabledKey.GetValue(name)?.ToString() ?? string.Empty;
                    items.Add(new StartupAppItem
                    {
                        Name = name,
                        Command = cmd,
                        FriendlyImpactLabel = GetPlainEnglishAdvice(name),
                        IsEnabled = false
                    });
                }
            }
        }

        return items.OrderBy(i => i.Name).ToList();
    }

    public void SetStartupEnabled(StartupAppItem item, bool enable)
    {
        if (enable)
        {
            using var runKey = Registry.CurrentUser.CreateSubKey(RunKeyPath);
            runKey.SetValue(item.Name, item.Command);

            using var disabledKey = Registry.CurrentUser.OpenSubKey(DisabledKeyPath, true);
            disabledKey?.DeleteValue(item.Name, false);
        }
        else
        {
            using var disabledKey = Registry.CurrentUser.CreateSubKey(DisabledKeyPath);
            disabledKey.SetValue(item.Name, item.Command);

            using var runKey = Registry.CurrentUser.OpenSubKey(RunKeyPath, true);
            runKey?.DeleteValue(item.Name, false);
        }
        item.IsEnabled = enable;
    }

    private static string GetPlainEnglishAdvice(string appName)
    {
        string lower = appName.ToLowerInvariant();
        if (lower.Contains("security") || lower.Contains("defender") || lower.Contains("audio") || lower.Contains("realtek"))
            return "Recommended to keep enabled (System / Hardware)";
        if (lower.Contains("steam") || lower.Contains("epic") || lower.Contains("discord") || lower.Contains("spotify") || lower.Contains("teams"))
            return "Safe to disable — slows down boot & uses background RAM";
        return "Optional background app — safe to toggle anytime";
    }
}
