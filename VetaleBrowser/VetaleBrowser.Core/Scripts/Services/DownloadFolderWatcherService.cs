using System;
using System.Diagnostics;
using System.IO;
using VetaleBrowser.VetaleBrowser.Core.Scripts.GlobalManagers;

namespace VetaleBrowser.VetaleBrowser.Core.Scripts.Services
{
    /// <summary>
    /// Мониторит папку Загрузки пользователя и регистрирует внешние загрузки в DownloadManager
    /// </summary>
    public static class DownloadFolderWatcherService
    {
        private static FileSystemWatcher? _watcher;
        public static string MonitoredPath { get; private set; } = string.Empty;
        private static bool _initialized;

        public static void Initialize(string? customPath = null)
        {
            if (_initialized) return;
            try
            {
                var downloads = customPath ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
                if (!Directory.Exists(downloads)) Directory.CreateDirectory(downloads);
                MonitoredPath = downloads;

                _watcher = new FileSystemWatcher(MonitoredPath)
                {
                    IncludeSubdirectories = false,
                    EnableRaisingEvents = true,
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.Size | NotifyFilters.CreationTime
                };

                _watcher.Created += OnCreated;
                _watcher.Renamed += OnRenamed;
                _watcher.Error += (_, e) => Debug.WriteLine($"[DownloadFolderWatcher] Error: {e.GetException().Message}");
                _initialized = true;
                Debug.WriteLine($"[DownloadFolderWatcher] Watching: {MonitoredPath}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DownloadFolderWatcher] Init failed: {ex.Message}");
            }
        }

        private static void OnCreated(object sender, FileSystemEventArgs e)
        {
            try
            {
                if (Directory.Exists(e.FullPath)) return;
                var ext = Path.GetExtension(e.FullPath);
                if (string.IsNullOrEmpty(ext)) return;
                // Спочатку пробуємо співставити pending по імені
                DownloadManager.MatchPendingFile(e.FullPath);
                // Якщо файл не був pending, реєструємо як зовнішнє завантаження
                DownloadManager.RegisterExternalFileDownload(e.FullPath);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DownloadFolderWatcher] OnCreated error: {ex.Message}");
            }
        }

        private static void OnRenamed(object sender, RenamedEventArgs e)
        {
            try
            {
                // Можна оновлювати ім'я/шлях в БД, але DownloadManager моніторить по TargetPath
                // і з новим записом все одно буде коректно відображатися.
            }
            catch { }
        }
    }
}
