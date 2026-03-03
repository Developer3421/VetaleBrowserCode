using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using VetaleBrowser.VetaleBrowser.Core.Scripts.Services;
using VetaleBrowser.VetaleBrowser.Database.Models;
using VetaleBrowser.VetaleBrowser.Database.Services;

namespace VetaleBrowser.VetaleBrowser.Core.Scripts.GlobalManagers;

/// <summary>
/// Global download manager: queue, progress, cancel, retry.
/// </summary>
public static class DownloadManager
{
    private static readonly object _initLock = new();
    private static DownloadDatabaseService? _db;
    private static readonly ObservableCollection<DownloadItem> _active = new();
    private static readonly HttpClient _http = new HttpClient { Timeout = TimeSpan.FromMinutes(30) };
    private static bool _initialized;
    private static readonly SemaphoreSlim _semaphore = new(3); // concurrent downloads

    // Events
    public static event EventHandler<DownloadItem>? ProgressChanged;
    public static event EventHandler<DownloadItem>? StatusChanged;
    public static event EventHandler? FirstDownloadStarted;

    // Speed averaging
    private const int SpeedSamples = 8;

    public static ReadOnlyObservableCollection<DownloadItem> Active { get; } = new(_active);

    public static void Initialize(string? customPath = null, string? customKey = null)
    {
        if (_initialized) return;
        lock (_initLock)
        {
            if (_initialized) return;
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var dataDir = Path.Combine(appData, "VetaleBrowser", "Data");
            if (!Directory.Exists(dataDir)) Directory.CreateDirectory(dataDir);
            var dbPath = customPath ?? Path.Combine(dataDir, "downloads.db");
            var key = customKey ?? GenerateEncryptionKey();
            _db = new DownloadDatabaseService(dbPath, key);

            _initialized = true;
        }
    }

    private static string GenerateEncryptionKey()
    {
        var machine = Environment.MachineName;
        var user = Environment.UserName;
        return $"VetaleBrowser_Downloads_{machine}_{user}_AES256";
    }

    public static async Task<int> StartDownloadAsync(string url, string? suggestedFileName = null)
    {
        Initialize();
        if (string.IsNullOrWhiteSpace(url)) throw new ArgumentException("URL is empty", nameof(url));

        var downloadsFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "VetaleBrowser");
        if (!Directory.Exists(downloadsFolder)) Directory.CreateDirectory(downloadsFolder);

        var fileName = suggestedFileName ?? TryExtractFileName(url) ?? "download.bin";
        fileName = SanitizeFileName(fileName);
        var targetPath = EnsureUniquePath(Path.Combine(downloadsFolder, fileName));

        var item = new DownloadItem
        {
            Url = url,
            FileName = fileName,
            TargetPath = targetPath,
            Status = "Pending",
            StartTime = DateTime.UtcNow,
            TotalBytes = -1
        };

        var id = _db!.AddOrUpdate(item);
        item.Id = id;
        _active.Add(item);
        StatusChanged?.Invoke(null, item);
        if (_active.Count == 1) FirstDownloadStarted?.Invoke(null, EventArgs.Empty);

        _ = Task.Run(() => ProcessDownloadAsync(item));
        return id;
    }

    public static void Cancel(int id)
    {
        var item = _active.FirstOrDefault(x => x.Id == id);
        if (item == null) return;
        item.Status = "Cancelled";
        item.EndTime = DateTime.UtcNow;
        StatusChanged?.Invoke(null, item);
        _db?.SetStatus(id, "Cancelled", null);
        _active.Remove(item);
    }

    public static async Task<int> RetryAsync(int id)
    {
        var old = _db?.GetById(id);
        if (old == null) return 0;
        if (old.Status == "Downloading") return id;
        return await StartDownloadAsync(old.Url, old.FileName);
    }

    public static void OpenFile(int id)
    {
        var item = _db?.GetById(id);
        if (item == null) return;
        var path = item.TargetPath;
        if (File.Exists(path))
        {
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true }); } catch { }
        }
    }

    public static void OpenFolder(int id)
    {
        var item = _db?.GetById(id);
        if (item == null) return;
        var folder = Path.GetDirectoryName(item.TargetPath);
        if (folder != null && Directory.Exists(folder))
        {
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(folder) { UseShellExecute = true }); } catch { }
        }
    }

    public static System.Collections.Generic.List<DownloadItem> GetRecent(int limit = 200)
    {
        Initialize();
        return _db?.GetRecent(limit) ?? new System.Collections.Generic.List<DownloadItem>();
    }

    public static System.Collections.Generic.List<DownloadItem> Search(string term)
    {
        Initialize();
        return _db?.Search(term) ?? new System.Collections.Generic.List<DownloadItem>();
    }

    public static void ArchiveOld(DateTime olderThan)
    {
        Initialize();
        _db?.ArchiveOld(olderThan);
    }

    private static async Task ProcessDownloadAsync(DownloadItem item)
    {
        await _semaphore.WaitAsync();
        var speedBuffer = new double[SpeedSamples];
        var speedIdx = 0;
        var lastBytes = 0L;
        var lastMeasure = DateTime.UtcNow;
        var cts = new CancellationTokenSource();

        try
        {
            item.Status = "Downloading";
            StatusChanged?.Invoke(null, item);
            _db?.SetStatus(item.Id, "Downloading");

            using var request = new HttpRequestMessage(HttpMethod.Get, item.Url);
            using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token);
            response.EnsureSuccessStatusCode();
            var total = response.Content.Headers.ContentLength ?? -1;
            item.TotalBytes = total;
            var contentType = response.Content.Headers.ContentType?.MediaType ?? string.Empty;
            if (!string.IsNullOrEmpty(contentType))
            {
                item.ContentType = contentType;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cts.Token);
            await using var fs = new FileStream(item.TargetPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);

            var buffer = new byte[81920];
            int read;
            while ((read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cts.Token)) > 0)
            {
                await fs.WriteAsync(buffer.AsMemory(0, read), cts.Token);
                item.BytesReceived += read;

                var now = DateTime.UtcNow;
                var deltaSec = (now - lastMeasure).TotalSeconds;
                if (deltaSec >= 1.0)
                {
                    var intervalBytes = item.BytesReceived - lastBytes;
                    var speed = intervalBytes / deltaSec;
                    speedBuffer[speedIdx] = speed;
                    speedIdx = (speedIdx + 1) % SpeedSamples;
                    lastBytes = item.BytesReceived;
                    lastMeasure = now;
                    var avgSpeed = speedBuffer.Where(x => x > 0).DefaultIfEmpty(0).Average();
                    item.LastMeasuredSpeedBytesPerSec = speed;
                    item.AverageSpeedBytesPerSec = avgSpeed;
                    item.EstimatedRemainingSeconds = (item.TotalBytes > 0 && avgSpeed > 0) ? (item.TotalBytes - item.BytesReceived) / avgSpeed : -1;

                    _db?.UpdateProgress(item.Id, item.BytesReceived, item.TotalBytes, speed, avgSpeed, item.EstimatedRemainingSeconds);
                    ProgressChanged?.Invoke(null, item);
                }
            }

            item.Status = "Completed";
            item.EndTime = DateTime.UtcNow;
            _db?.SetStatus(item.Id, "Completed");
            StatusChanged?.Invoke(null, item);
        }
        catch (Exception ex)
        {
            item.Status = "Error";
            item.ErrorMessage = ex.Message;
            item.EndTime = DateTime.UtcNow;
            _db?.SetStatus(item.Id, "Error", ex.Message);
            StatusChanged?.Invoke(null, item);
        }
        finally
        {
            _active.Remove(item);
            _semaphore.Release();
        }
    }

    private static string? TryExtractFileName(string url)
    {
        try
        {
            var uri = new Uri(url);
            var name = Path.GetFileName(uri.LocalPath);
            if (string.IsNullOrWhiteSpace(name)) return null;
            return name;
        }
        catch { return null; }
    }

    private static string SanitizeFileName(string input)
    {
        var invalid = Path.GetInvalidFileNameChars();
        foreach (var ch in invalid) input = input.Replace(ch, '_');
        return input.Trim();
    }

    private static string EnsureUniquePath(string path)
    {
        if (!File.Exists(path)) return path;
        var dir = Path.GetDirectoryName(path)!;
        var name = Path.GetFileNameWithoutExtension(path);
        var ext = Path.GetExtension(path);
        var i = 1;
        string candidate;
        do
        {
            candidate = Path.Combine(dir, $"{name} ({i}){ext}");
            i++;
        } while (File.Exists(candidate));
        return candidate;
    }

    private static readonly Dictionary<string,int> _externalByPath = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string,int> _pendingByName = new(StringComparer.OrdinalIgnoreCase);

    public static async Task<List<DownloadItem>> ScanDownloadsFolderAsync(DateTime fromUtc)
    {
        Initialize();
        var result = new List<DownloadItem>();
        if (_db == null) return result;

        try
        {
            var downloadsPath = DownloadFolderWatcherService.MonitoredPath;
            if (string.IsNullOrWhiteSpace(downloadsPath) || !Directory.Exists(downloadsPath))
            {
                var userDownloads = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
                if (!Directory.Exists(userDownloads)) return result;
                downloadsPath = userDownloads;
            }

            // Efficient start: no earlier than 3 months ago
            var threeMonthsAgo = DateTime.UtcNow.AddMonths(-3);
            var effectiveFrom = fromUtc < threeMonthsAgo ? threeMonthsAgo : fromUtc;

            var files = Directory.EnumerateFiles(downloadsPath, "*", SearchOption.TopDirectoryOnly)
                .Select(path => new FileInfo(path))
                .Where(fi => !fi.Name.Equals("desktop.ini", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(fi => fi.LastWriteTimeUtc);

            foreach (var fi in files)
            {
                var modified = fi.LastWriteTimeUtc;
                if (modified <= effectiveFrom) continue;

                var existing = _db.GetByTargetPath(fi.FullName);
                if (existing != null)
                {
                    var beforeBytes = existing.BytesReceived;
                    existing.TargetPath = fi.FullName;
                    existing.BytesReceived = fi.Length;
                    if (existing.TotalBytes <= 0) existing.TotalBytes = fi.Length;
                    if (existing.Status != "Completed")
                    {
                        existing.Status = "Completed";
                        existing.EndTime ??= fi.LastWriteTimeUtc;
                    }
                    existing.ImportedAt ??= DateTime.UtcNow;
                    _db.AddOrUpdate(existing);
                    if (existing.BytesReceived != beforeBytes || existing.Status == "Completed")
                        result.Add(existing);
                }
                else
                {
                    var item = new DownloadItem
                    {
                        Url = string.Empty,
                        FileName = fi.Name,
                        TargetPath = fi.FullName,
                        Status = "Completed",
                        BytesReceived = fi.Length,
                        TotalBytes = fi.Length,
                        StartTime = fi.CreationTimeUtc,
                        EndTime = fi.LastWriteTimeUtc,
                        ImportedAt = DateTime.UtcNow
                    };
                    _db.AddOrUpdate(item);
                    result.Add(item);
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DownloadManager] ScanDownloadsFolderAsync error: {ex.Message}");
        }

        // Return in order from newest to oldest
        return result.OrderByDescending(x => x.EndTime ?? x.StartTime).ToList();
    }

    public static async Task<List<DownloadItem>> ScanAndSyncDownloadsAsync(SettingsService settings)
    {
        Initialize();
        var lastScan = await settings.GetLastDownloadsScanUtcAsync() ?? DateTime.UtcNow.AddDays(-30);
        // Small adjustment to avoid missing files at the time boundary
        var adjustedFrom = lastScan.AddSeconds(-5);
        var list = await ScanDownloadsFolderAsync(adjustedFrom);
        await settings.SetLastDownloadsScanUtcAsync(DateTime.UtcNow);
        return list;
    }

    public static int RegisterPendingExternal(string url, string fileName)
    {
        Initialize();
        if (fileName.Equals("desktop.ini", StringComparison.OrdinalIgnoreCase)) return 0;
        fileName = SanitizeFileName(fileName);
        if (_pendingByName.ContainsKey(fileName)) return _pendingByName[fileName];
        var item = new DownloadItem
        {
            Url = url,
            FileName = fileName,
            TargetPath = string.Empty,
            Status = "Pending",
            StartTime = DateTime.UtcNow,
            TotalBytes = -1,
            BytesReceived = 0
        };
        var id = _db!.AddOrUpdate(item);
        item.Id = id;
        _pendingByName[fileName] = id;
        StatusChanged?.Invoke(null, item);
        return id;
    }

    public static void MatchPendingFile(string fullPath)
    {
        Initialize();
        var name = Path.GetFileName(fullPath);
        if (name.Equals("desktop.ini", StringComparison.OrdinalIgnoreCase)) return;
        if (!_pendingByName.TryGetValue(name, out var id)) return;
        var item = _db?.GetById(id);
        if (item == null) return;
        if (string.IsNullOrEmpty(item.TargetPath))
        {
            item.TargetPath = fullPath;
        }
        item.Status = "Downloading"; // already started writing
        _db?.SetStatus(item.Id, item.Status);
        StatusChanged?.Invoke(null, item);
        // start monitoring as external
        _ = RegisterExternalFileDownload(fullPath); // will create a separate record if not exists - so better to update existing
    }

    public static int RegisterExternalFileDownload(string fullPath)
    {
        Initialize();
        var name = Path.GetFileName(fullPath);
        if (name.Equals("desktop.ini", StringComparison.OrdinalIgnoreCase)) return 0;
        if (_pendingByName.TryGetValue(name, out var pendingId))
        {
            // update existing pending instead of creating a new one
            var existing = _db?.GetById(pendingId);
            if (existing != null)
            {
                var fi = new FileInfo(fullPath);
                existing.TargetPath = fullPath;
                existing.Status = "Downloading";
                existing.BytesReceived = fi.Exists ? fi.Length : 0L;
                _db!.AddOrUpdate(existing);
                _db.UpdateProgress(existing.Id, existing.BytesReceived, existing.TotalBytes, 0,0,-1);
                StatusChanged?.Invoke(null, existing);
                _ = Task.Run(() => MonitorExternalFileAsync(existing));
                return existing.Id;
            }
        }
        var item = new DownloadItem
        {
            Url = string.Empty,
            FileName = name,
            TargetPath = fullPath,
            Status = "Downloading",
            StartTime = DateTime.UtcNow,
            TotalBytes = -1,
            BytesReceived = 0
        };
        var id = _db!.AddOrUpdate(item);
        item.Id = id;
        StatusChanged?.Invoke(null, item);
        _ = Task.Run(() => MonitorExternalFileAsync(item));
        return id;
    }

    private static async Task MonitorExternalFileAsync(DownloadItem item)
    {
        try
        {
            long last = item.BytesReceived;
            var stableTicks = 0;
            while (true)
            {
                long size = 0;
                bool locked = false;
                try
                {
                    var fi = new FileInfo(item.TargetPath);
                    size = fi.Exists ? fi.Length : 0;
                    using var fs = new FileStream(item.TargetPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                }
                catch { locked = true; }

                if (size != last)
                {
                    last = size;
                    item.BytesReceived = size;
                    _db?.UpdateProgress(item.Id, item.BytesReceived, item.TotalBytes, 0, 0, -1);
                    ProgressChanged?.Invoke(null, item);
                    stableTicks = 0;
                }
                else
                {
                    stableTicks++;
                }

                if (!locked && stableTicks >= 6)
                {
                    // File has stabilized — consider it completed
                    item.TotalBytes = item.BytesReceived; // fix final size
                    item.Status = "Completed";
                    item.EndTime = DateTime.UtcNow;
                    _db?.UpdateProgress(item.Id, item.BytesReceived, item.TotalBytes, 0, 0, -1);
                    _db?.SetStatus(item.Id, "Completed");
                    StatusChanged?.Invoke(null, item);
                    break;
                }

                await Task.Delay(500);
            }
        }
        catch (Exception ex)
        {
            item.Status = "Error";
            item.ErrorMessage = ex.Message;
            item.EndTime = DateTime.UtcNow;
            _db?.SetStatus(item.Id, "Error", ex.Message);
            StatusChanged?.Invoke(null, item);
        }
        finally
        {
            _externalByPath.Remove(item.TargetPath);
        }
    }
}
