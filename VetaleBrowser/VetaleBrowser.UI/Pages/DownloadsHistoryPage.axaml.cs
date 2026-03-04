using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media.Imaging;
using VetaleBrowser.VetaleBrowser.Core.Scripts.GlobalManagers;
using VetaleBrowser.VetaleBrowser.Core.Scripts.Services;
using VetaleBrowser.VetaleBrowser.Database.Models;
using VetaleBrowser.VetaleBrowser.Database.Services;
using VetaleBrowser.VetaleBrowser.UI.Services;
using System.Windows.Input;

namespace VetaleBrowser.VetaleBrowser.UI.Pages
{
    public partial class DownloadsHistoryPage : UserControl
    {
        private TextBox? _searchTextBox;
        private readonly ObservableCollection<DownloadHistoryItemViewModel> _items = new();

        public DownloadsHistoryPage()
        {
            InitializeComponent();
            _searchTextBox = this.FindControl<TextBox>("SearchTextBox");
            var list = this.FindControl<ItemsControl>("DownloadsList");
            if (list != null) list.ItemsSource = _items;

            var refreshButton = this.FindControl<Button>("RefreshButton");
            var clearOldButton = this.FindControl<Button>("ClearOldButton");
            var downloadsPathText = this.FindControl<TextBlock>("DownloadsPathText");
            var openDownloadsButton = this.FindControl<Button>("OpenDownloadsFolderButton");

            if (downloadsPathText != null)
                downloadsPathText.Text = DownloadFolderWatcherService.MonitoredPath;
            if (openDownloadsButton != null)
                openDownloadsButton.Click += (_, __) =>
                {
                    try
                    {
                        var p = DownloadFolderWatcherService.MonitoredPath;
                        if (!string.IsNullOrWhiteSpace(p) && Directory.Exists(p))
                        {
                            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(p) { UseShellExecute = true });
                        }
                    }
                    catch { }
                };

            if (refreshButton != null)
                refreshButton.Click += async (_, __) => await ReloadAsync(forceFullRescan: true);
            if (clearOldButton != null)
                clearOldButton.Click += async (_, __) =>
                {
                    DownloadManager.ArchiveOld(DateTime.UtcNow.AddDays(-30));
                    await ReloadAsync(forceFullRescan: true);
                };

            if (_searchTextBox != null)
                _searchTextBox.TextChanged += async (_, __) => await ReloadAsync(forceFullRescan: false);

            DownloadManager.StatusChanged += OnStatus;

            _ = ReloadAsync(forceFullRescan: false);
        }

        private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

        private async Task ReloadAsync(bool forceFullRescan)
        {
            DownloadManager.Initialize();
            _items.Clear();

            // 1. Show everything already in the DB
            var all = DownloadManager.GetRecent(2000);
            var completed = all.Where(d => d.Status == "Completed");
            var term = _searchTextBox?.Text;
            if (!string.IsNullOrWhiteSpace(term))
            {
                term = term.Trim();
                completed = completed.Where(d => d.FileName.Contains(term, StringComparison.OrdinalIgnoreCase)
                    || (!string.IsNullOrEmpty(d.Url) && d.Url.Contains(term, StringComparison.OrdinalIgnoreCase)));
            }

            foreach (var d in completed.OrderByDescending(x => x.EndTime ?? x.StartTime))
                _items.Add(new DownloadHistoryItemViewModel(d));

                    // 2. Asynchronously scan the main downloads folder (no subfolders)
            try
            {
                var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                var dataDir = System.IO.Path.Combine(appData, "VetaleBrowser", "Data");
                var settingsPath = System.IO.Path.Combine(dataDir, "settings.db");
                var key = $"VetaleBrowser_Settings_{Environment.MachineName}_{Environment.UserName}_AES256";
                using var settings = new SettingsService(settingsPath, key);

                if (forceFullRescan)
                {
                    // Forced full scan: use a very old date as fromUtc
                    var newItems = await DownloadManager.ScanDownloadsFolderAsync(DateTime.UtcNow.AddYears(-5));
                    foreach (var e in newItems)
                        Upsert(e);
                    await settings.SetLastDownloadsScanUtcAsync(DateTime.UtcNow);
                }
                else
                {
                    var newItems = await DownloadManager.ScanAndSyncDownloadsAsync(settings);
                    foreach (var e in newItems)
                        Upsert(e);
                }
            }
            catch
            {
                // If SettingsService could not be initialized or scan failed, just leave the list as is
            }
        }

        private void OnStatus(object? sender, DownloadItem e)
        {
            if (e.Status == "Completed")
            {
                Upsert(e);
            }
        }

        private void Upsert(DownloadItem e)
        {
            var term = _searchTextBox?.Text;
            if (!string.IsNullOrWhiteSpace(term))
            {
                if (!e.FileName.Contains(term, StringComparison.OrdinalIgnoreCase) && !(!string.IsNullOrEmpty(e.Url) && e.Url.Contains(term, StringComparison.OrdinalIgnoreCase)))
                    return;
            }
            var existing = _items.FirstOrDefault(x => x.Id == e.Id);
            if (existing == null)
            {
                _items.Insert(0, new DownloadHistoryItemViewModel(e));
            }
            else
            {
                var index = _items.IndexOf(existing);
            // If the path or name changed — replace the element to update the icon/text
                if (!string.Equals(existing.TargetPath, e.TargetPath, StringComparison.OrdinalIgnoreCase)
                    || !string.Equals(existing.FileName, e.FileName, StringComparison.OrdinalIgnoreCase))
                {
                    _items[index] = new DownloadHistoryItemViewModel(e);
                }
                else
                {
                    existing.UpdateFrom(e);
                }
            }
        }
    }

    public class DownloadHistoryItemViewModel
    {
        private readonly DownloadItem _item;
        public Bitmap? IconBitmap { get; }
        public int Id => _item.Id;
        public string FileName => _item.FileName;
        public string TargetPath => _item.TargetPath;
        public bool CanOpen => File.Exists(_item.TargetPath);
        public string SizeDisplay => _item.TotalBytes > 0 ? FormatSize(_item.TotalBytes) : (_item.BytesReceived > 0 ? FormatSize(_item.BytesReceived) : "");
        public string CompletedAtDisplay => _item.EndTime.HasValue ? _item.EndTime.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm") : "";
        public ICommand OpenFileCommand => new RelayCommand(_ => DownloadManager.OpenFile(_item.Id), _ => CanOpen);
        public ICommand OpenFolderCommand => new RelayCommand(_ => DownloadManager.OpenFolder(_item.Id), _ => CanOpen);
        public string Status => _item.Status;
        public void UpdateFrom(DownloadItem updated)
        {
            _item.Status = updated.Status;
            _item.BytesReceived = updated.BytesReceived;
            _item.TotalBytes = updated.TotalBytes;
            _item.EndTime = updated.EndTime;
            _item.FileName = updated.FileName;
            _item.TargetPath = updated.TargetPath;
        }
        public DownloadHistoryItemViewModel(DownloadItem item) { _item = item; IconBitmap = FileIconService.GetFileIcon(item.TargetPath); }

        private static string FormatSize(long bytes)
        {
            if (bytes < 1024) return bytes + " B";
            double kb = bytes / 1024.0;
            if (kb < 1024) return kb.ToString("F1") + " KB";
            double mb = kb / 1024.0;
            if (mb < 1024) return mb.ToString("F1") + " MB";
            double gb = mb / 1024.0;
            return gb.ToString("F2") + " GB";
        }
    }

    public class RelayCommand : ICommand
    {
        private readonly Action<object?> _execute;
        private readonly Func<object?, bool>? _canExecute;
        public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null) { _execute = execute; _canExecute = canExecute; }
        public event EventHandler? CanExecuteChanged;
        public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;
        public void Execute(object? parameter) => _execute(parameter);

        public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
