using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using PulseBoost.Models;
using PulseBoost.Services;
using Application = System.Windows.Application;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;

namespace PulseBoost.ViewModels;

public partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly SettingsService _settingsService = new();
    private readonly TelemetryService _telemetryService = new();
    private readonly MemoryBoostService _memoryService = new();
    private readonly PowerPlanService _powerService = new();
    private readonly ProcessBoostService _processService = new();
    private readonly DiskCleanerService _cleanerService = new();
    private readonly StartupManagerService _startupService = new();
    private readonly GameWatcherService _watcherService;
    private readonly DispatcherTimer _telemetryTimer;
    private readonly AppSettings _settings;
    private DateTime _lastAutoRamCleanTime = DateTime.MinValue;

    [ObservableProperty] private bool _isBoostActive;
    [ObservableProperty] private bool _isAutoDetectEnabled;
    [ObservableProperty] private bool _minimizeToTrayOnClose;
    [ObservableProperty] private bool _autoCleanRamOnHighUsage;
    [ObservableProperty] private int _autoCleanRamThresholdPercent;
    [ObservableProperty] private int _selectedModeIndex;

    [ObservableProperty] private string _statusHeadline = "System Ready";
    [ObservableProperty] private string _statusSubtitle = "Click Activate Boost, Boost RAM, or launch a watched Game / AI tool.";
    [ObservableProperty] private string _boostButtonText = "Activate Boost";
    [ObservableProperty] private string _metricsSummary = "Power Plan: Balanced • Auto-Watcher: Active";

    // Live Telemetry Properties
    [ObservableProperty] private double _cpuUsagePercent;
    [ObservableProperty] private string _cpuUsageText = "0%";
    [ObservableProperty] private double _ramUsagePercent;
    [ObservableProperty] private string _ramUsageText = "0 / 0 GB";

    // Cleaner & Startup Status
    [ObservableProperty] private string _cleanerStatusText = "Click 'Scan Junk' to safely calculate recoverable space.";
    [ObservableProperty] private bool _isCleanerBusy;

    public ObservableCollection<AppTargetItem> BackgroundTargets { get; } = new();
    public ObservableCollection<string> WatchedApps { get; } = new();
    public ObservableCollection<CleanTargetItem> CleanTargets { get; } = new();
    public ObservableCollection<StartupAppItem> StartupItems { get; } = new();

    public event Action<bool>? BoostStateChanged;

    public MainViewModel()
    {
        _settings = _settingsService.Load();
        _selectedModeIndex = _settings.SelectedModeIndex;
        _isAutoDetectEnabled = _settings.IsAutoDetectEnabled;
        _minimizeToTrayOnClose = _settings.MinimizeToTrayOnClose;
        _autoCleanRamOnHighUsage = _settings.AutoCleanRamOnHighUsage;
        _autoCleanRamThresholdPercent = _settings.AutoCleanRamThresholdPercent;

        _watcherService = new GameWatcherService(_settings.WatchedExecutables);
        foreach (var app in _settings.WatchedExecutables)
            WatchedApps.Add(app);

        LoadTargetsForCurrentMode();
        foreach (var item in _cleanerService.GetDefaultCleanTargets())
            CleanTargets.Add(item);
        RefreshStartupItems();

        _watcherService.TargetProcessStarted += OnTargetDetected;
        _watcherService.TargetProcessStopped += OnTargetExited;
        _watcherService.Start();

        _telemetryTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.5) };
        _telemetryTimer.Tick += (_, _) => UpdateTelemetry();
        _telemetryTimer.Start();
        UpdateTelemetry();
    }

    public void SetTelemetryActive(bool isVisible)
    {
        if (isVisible)
        {
            UpdateTelemetry();
            _telemetryTimer.Start();
        }
        else if (!AutoCleanRamOnHighUsage)
        {
            _telemetryTimer.Stop();
        }
    }

    private void UpdateTelemetry()
    {
        var (cpu, usedRam, totalRam, ramPct) = _telemetryService.GetSystemMetrics();
        CpuUsagePercent = cpu;
        CpuUsageText = $"{cpu:0.0}%";
        RamUsagePercent = ramPct;
        RamUsageText = $"{usedRam:0.0} / {totalRam:0.0} GB ({ramPct:0}%)";

        // Auto-RAM cleaner threshold check (with 45-second cooldown to prevent thrashing)
        if (AutoCleanRamOnHighUsage && ramPct >= AutoCleanRamThresholdPercent)
        {
            if ((DateTime.UtcNow - _lastAutoRamCleanTime).TotalSeconds >= 45)
            {
                _lastAutoRamCleanTime = DateTime.UtcNow;
                long freedMb = _memoryService.OptimizeSystemRam();
                StatusSubtitle = $"Auto-RAM Guard triggered at {ramPct:0}% usage • Reclaimed {freedMb} MB RAM";
                var (_, newUsed, newTotal, newPct) = _telemetryService.GetSystemMetrics();
                RamUsagePercent = newPct;
                RamUsageText = $"{newUsed:0.0} / {newTotal:0.0} GB ({newPct:0}%)";
            }
        }
    }

    partial void OnSelectedModeIndexChanged(int value)
    {
        _settings.SelectedModeIndex = value;
        LoadTargetsForCurrentMode();
        SaveSettings();
    }

    partial void OnIsAutoDetectEnabledChanged(bool value)
    {
        _settings.IsAutoDetectEnabled = value;
        MetricsSummary = $"Power Plan: {(IsBoostActive ? "High Performance" : "Balanced")} • Auto-Watcher: {(value ? "Active" : "Paused")}";
        SaveSettings();
    }

    partial void OnMinimizeToTrayOnCloseChanged(bool value)
    {
        _settings.MinimizeToTrayOnClose = value;
        SaveSettings();
    }

    partial void OnAutoCleanRamOnHighUsageChanged(bool value)
    {
        _settings.AutoCleanRamOnHighUsage = value;
        if (value && !_telemetryTimer.IsEnabled)
            _telemetryTimer.Start();
        SaveSettings();
    }

    partial void OnAutoCleanRamThresholdPercentChanged(int value)
    {
        _settings.AutoCleanRamThresholdPercent = value;
        SaveSettings();
    }

    private void LoadTargetsForCurrentMode()
    {
        BackgroundTargets.Clear();
        var list = SelectedModeIndex == 0 ? _settings.GamingTargets : _settings.LocalAITargets;
        foreach (var item in list)
            BackgroundTargets.Add(item);
    }

    [RelayCommand]
    public void SaveSettings() => _settingsService.Save(_settings);

    [RelayCommand]
    public void ToggleBoost()
    {
        if (!IsBoostActive)
            ActivateBoost(null);
        else
            DeactivateBoost();
    }

    [RelayCommand]
    public void CleanRamOnly()
    {
        long freedMb = _memoryService.OptimizeSystemRam();
        StatusHeadline = IsBoostActive ? StatusHeadline : "RAM Boost Complete";
        StatusSubtitle = $"Trimmed working sets & purged Standby RAM • Reclaimed {freedMb} MB memory.";
        UpdateTelemetry();
    }

    [RelayCommand]
    private void AddWatchedApp()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Applications (*.exe)|*.exe",
            Title = "Select Game or Local AI Executable"
        };

        if (dialog.ShowDialog() == true)
        {
            string exeName = System.IO.Path.GetFileName(dialog.FileName);
            if (!WatchedApps.Contains(exeName, StringComparer.OrdinalIgnoreCase))
            {
                WatchedApps.Add(exeName);
                _watcherService.AddWatchedExecutable(exeName);
                _settings.WatchedExecutables.Add(exeName);
                SaveSettings();
            }
        }
    }

    [RelayCommand]
    private void RemoveWatchedApp(string? exeName)
    {
        if (string.IsNullOrWhiteSpace(exeName)) return;
        WatchedApps.Remove(exeName);
        _watcherService.RemoveWatchedExecutable(exeName);
        _settings.WatchedExecutables.Remove(exeName);
        SaveSettings();
    }

    [RelayCommand]
    private async Task ScanJunkAsync()
    {
        IsCleanerBusy = true;
        CleanerStatusText = "Scanning temporary and cache folders...";
        await _cleanerService.ScanAsync(CleanTargets);

        var snapshot = CleanTargets.ToList();
        CleanTargets.Clear();
        long totalBytes = 0;
        foreach (var item in snapshot)
        {
            totalBytes += item.SizeBytes;
            CleanTargets.Add(item);
        }

        CleanerStatusText = $"Scan complete: {totalBytes / (1024.0 * 1024.0):0.0} MB safe to clean.";
        IsCleanerBusy = false;
    }

    [RelayCommand]
    private async Task CleanJunkAsync()
    {
        IsCleanerBusy = true;
        CleanerStatusText = "Cleaning selected cache folders...";
        long freedBytes = await _cleanerService.CleanAsync(CleanTargets);

        var snapshot = CleanTargets.ToList();
        CleanTargets.Clear();
        foreach (var item in snapshot) CleanTargets.Add(item);

        CleanerStatusText = $"Cleaned! Recovered {freedBytes / (1024.0 * 1024.0):0.0} MB of disk space.";
        IsCleanerBusy = false;
    }

    [RelayCommand]
    private void RefreshStartupItems()
    {
        StartupItems.Clear();
        foreach (var item in _startupService.GetStartupItems())
            StartupItems.Add(item);
    }

    [RelayCommand]
    private void ToggleStartupItem(StartupAppItem? item)
    {
        if (item == null) return;
        _startupService.SetStartupEnabled(item, item.IsEnabled);
    }

    private void ActivateBoost(string? triggerExe)
    {
        _powerService.ActivateHighPerformance();
        var (appsCount, closedAppsMb) = _processService.ApplyBoost(BackgroundTargets, triggerExe);
        long trimmedRamMb = _memoryService.OptimizeSystemRam();
        long totalFreedMb = closedAppsMb + trimmedRamMb;

        IsBoostActive = true;
        BoostButtonText = "Restore Normal Mode";
        StatusHeadline = triggerExe is null ? "Boost Mode Active" : $"Boosted for {triggerExe}";
        StatusSubtitle = $"Optimized {appsCount} background apps • Reclaimed {totalFreedMb} MB RAM";
        MetricsSummary = "Power Plan: High Performance • RAM Standby Cache Purged";
        UpdateTelemetry();
        BoostStateChanged?.Invoke(true);
    }

    private void DeactivateBoost()
    {
        _processService.RestoreAll();
        _powerService.RestorePreviousPlan();

        IsBoostActive = false;
        BoostButtonText = "Activate Boost";
        StatusHeadline = "System Restored";
        StatusSubtitle = "Original power plan and background apps restored.";
        MetricsSummary = $"Power Plan: Restored • Auto-Watcher: {(IsAutoDetectEnabled ? "Active" : "Paused")}";
        UpdateTelemetry();
        BoostStateChanged?.Invoke(false);
    }

    private void OnTargetDetected(string exeName)
    {
        if (!IsAutoDetectEnabled || IsBoostActive) return;
        Application.Current.Dispatcher.Invoke(() => ActivateBoost(exeName));
    }

    private void OnTargetExited(string exeName)
    {
        if (!IsAutoDetectEnabled || !IsBoostActive) return;
        Application.Current.Dispatcher.Invoke(DeactivateBoost);
    }

    public void Dispose()
    {
        if (IsBoostActive) DeactivateBoost();
        _telemetryTimer.Stop();
        _telemetryService.Dispose();
        _watcherService.Dispose();
    }
}
