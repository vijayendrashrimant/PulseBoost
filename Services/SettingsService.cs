using System.IO;
using System.Text.Json;
using PulseBoost.Models;

namespace PulseBoost.Services;

public class SettingsService
{
    private readonly string _settingsFilePath;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public SettingsService()
    {
        string folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "PulseBoost");
        Directory.CreateDirectory(folder);
        _settingsFilePath = Path.Combine(folder, "settings.json");
    }

    public AppSettings Load()
    {
        if (!File.Exists(_settingsFilePath))
        {
            var defaults = CreateDefaultSettings();
            Save(defaults);
            return defaults;
        }

        try
        {
            string json = File.ReadAllText(_settingsFilePath);
            return JsonSerializer.Deserialize<AppSettings>(json) ?? CreateDefaultSettings();
        }
        catch
        {
            return CreateDefaultSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        try
        {
            string json = JsonSerializer.Serialize(settings, JsonOptions);
            File.WriteAllText(_settingsFilePath, json);
        }
        catch { }
    }

    private static AppSettings CreateDefaultSettings() => new()
    {
        GamingTargets = new()
        {
            new() { ProcessName = "chrome", FriendlyName = "Google Chrome (Lower Priority)", ShouldCloseOnBoost = false, ShouldLowerPriority = true },
            new() { ProcessName = "OneDrive", FriendlyName = "OneDrive Cloud Sync", ShouldCloseOnBoost = false, ShouldLowerPriority = true },
            new() { ProcessName = "Teams", FriendlyName = "Microsoft Teams (Close & Restore)", ShouldCloseOnBoost = true }
        },
        LocalAITargets = new()
        {
            new() { ProcessName = "chrome", FriendlyName = "Google Chrome (Close to Free RAM/VRAM)", ShouldCloseOnBoost = true },
            new() { ProcessName = "msedge", FriendlyName = "Microsoft Edge (Close to Free RAM/VRAM)", ShouldCloseOnBoost = true },
            new() { ProcessName = "Discord", FriendlyName = "Discord Hardware Overlay", ShouldCloseOnBoost = true },
            new() { ProcessName = "OneDrive", FriendlyName = "OneDrive Cloud Sync", ShouldCloseOnBoost = false, ShouldLowerPriority = true }
        }
    };
}
