using System.Management;

namespace PulseBoost.Services;

public class GameWatcherService : IDisposable
{
    private ManagementEventWatcher? _startWatcher;
    private ManagementEventWatcher? _stopWatcher;
    private readonly HashSet<string> _watchedExecutables;

    public event Action<string>? TargetProcessStarted;
    public event Action<string>? TargetProcessStopped;

    public GameWatcherService(IEnumerable<string> initialExecutables)
    {
        _watchedExecutables = new HashSet<string>(initialExecutables, StringComparer.OrdinalIgnoreCase);
    }

    public void AddWatchedExecutable(string exeName) => _watchedExecutables.Add(exeName);
    public void RemoveWatchedExecutable(string exeName) => _watchedExecutables.Remove(exeName);
    public IReadOnlyCollection<string> WatchedExecutables => _watchedExecutables;

    public void Start()
    {
        try
        {
            _startWatcher = new ManagementEventWatcher(new WqlEventQuery("SELECT * FROM Win32_ProcessStartTrace"));
            _startWatcher.EventArrived += (_, e) =>
            {
                string? procName = e.NewEvent.Properties["ProcessName"].Value?.ToString();
                if (!string.IsNullOrEmpty(procName) && _watchedExecutables.Contains(procName))
                    TargetProcessStarted?.Invoke(procName);
            };
            _startWatcher.Start();

            _stopWatcher = new ManagementEventWatcher(new WqlEventQuery("SELECT * FROM Win32_ProcessStopTrace"));
            _stopWatcher.EventArrived += (_, e) =>
            {
                string? procName = e.NewEvent.Properties["ProcessName"].Value?.ToString();
                if (!string.IsNullOrEmpty(procName) && _watchedExecutables.Contains(procName))
                    TargetProcessStopped?.Invoke(procName);
            };
            _stopWatcher.Start();
        }
        catch { }
    }

    public void Dispose()
    {
        _startWatcher?.Stop();
        _startWatcher?.Dispose();
        _stopWatcher?.Stop();
        _stopWatcher?.Dispose();
    }
}
