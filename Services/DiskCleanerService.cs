using System.IO;

namespace PulseBoost.Services;

public class CleanTargetItem
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<string> FolderPaths { get; set; } = new();
    public bool IsSelected { get; set; } = true;
    public long SizeBytes { get; set; }
    public string FormattedSize => $"{SizeBytes / (1024.0 * 1024.0):0.0} MB";
}

public class DiskCleanerService
{
    public List<CleanTargetItem> GetDefaultCleanTargets()
    {
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);

        return new List<CleanTargetItem>
        {
            new()
            {
                Name = "Windows & User Temporary Files",
                Description = "Safe leftover installer and app temp files (%TEMP%)",
                FolderPaths = new() { Path.GetTempPath(), Path.Combine(winDir, "Temp") }
            },
            new()
            {
                Name = "Google Chrome Cache",
                Description = "Cached web images and scripts (keeps passwords & history)",
                FolderPaths = new()
                {
                    Path.Combine(localAppData, @"Google\Chrome\User Data\Default\Cache"),
                    Path.Combine(localAppData, @"Google\Chrome\User Data\Default\Code Cache")
                }
            },
            new()
            {
                Name = "Microsoft Edge Cache",
                Description = "Cached browser files (keeps bookmarks & logins)",
                FolderPaths = new()
                {
                    Path.Combine(localAppData, @"Microsoft\Edge\User Data\Default\Cache"),
                    Path.Combine(localAppData, @"Microsoft\Edge\User Data\Default\Code Cache")
                }
            },
            new()
            {
                Name = "DirectX & GPU Shader Cache",
                Description = "Old compiled shader files (rebuilds cleanly when needed)",
                FolderPaths = new()
                {
                    Path.Combine(localAppData, "D3DSCache")
                }
            }
        };
    }

    public async Task ScanAsync(IEnumerable<CleanTargetItem> targets)
    {
        await Task.Run(() =>
        {
            foreach (var target in targets)
            {
                long total = 0;
                foreach (var folder in target.FolderPaths)
                {
                    if (!Directory.Exists(folder)) continue;
                    total += CalculateDirectorySizeSafe(new DirectoryInfo(folder));
                }
                target.SizeBytes = total;
            }
        });
    }

    public async Task<long> CleanAsync(IEnumerable<CleanTargetItem> targets)
    {
        long totalFreed = 0;
        await Task.Run(() =>
        {
            foreach (var target in targets.Where(t => t.IsSelected))
            {
                foreach (var folder in target.FolderPaths)
                {
                    if (!Directory.Exists(folder)) continue;
                    totalFreed += DeleteDirectoryContentsSafe(new DirectoryInfo(folder));
                }
                target.SizeBytes = 0;
            }
        });
        return totalFreed;
    }

    private static long CalculateDirectorySizeSafe(DirectoryInfo dir)
    {
        long size = 0;
        try
        {
            foreach (var file in dir.EnumerateFiles())
            {
                try { size += file.Length; } catch { }
            }
            foreach (var subDir in dir.EnumerateDirectories())
            {
                size += CalculateDirectorySizeSafe(subDir);
            }
        }
        catch { }
        return size;
    }

    private static long DeleteDirectoryContentsSafe(DirectoryInfo dir)
    {
        long freed = 0;
        try
        {
            foreach (var file in dir.EnumerateFiles())
            {
                try
                {
                    long len = file.Length;
                    file.Delete();
                    freed += len;
                }
                catch { }
            }
            foreach (var subDir in dir.EnumerateDirectories())
            {
                freed += DeleteDirectoryContentsSafe(subDir);
                try { subDir.Delete(); } catch { }
            }
        }
        catch { }
        return freed;
    }
}
