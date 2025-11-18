using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media.Imaging;
using VetaleBrowser.VetaleBrowser.Core.Scripts.GlobalManagers;
using VetaleBrowser.VetaleBrowser.Core.Scripts.Services;
using VetaleBrowser.VetaleBrowser.Database.Models;
using VetaleBrowser.VetaleBrowser.UI.Services;
using System.Windows.Input;

namespace VetaleBrowser.VetaleBrowser.UI.Pages
{
    public partial class DownloadsHistoryPage : UserControl
    {
        private TextBox? _searchTextBox;
        private ItemsControl? _list;
        private readonly ObservableCollection<DownloadHistoryItemViewModel> _items = new();

        public DownloadsHistoryPage()
        {
            InitializeComponent();
            _searchTextBox = this.FindControl<TextBox>("SearchTextBox");
            _list = this.FindControl<ItemsControl>("DownloadsList");
            if (_list != null) _list.ItemsSource = _items;

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

            refreshButton!.Click += (_, __) => Reload();
            clearOldButton!.Click += (_, __) => ClearOld();

            // Лише завершені файли – немає підписок на прогрес
            DownloadManager.StatusChanged += OnStatus;
            DownloadManager.ProgressChanged += OnProgress;

            Reload();
        }

        private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

        private void Reload()
        {
            DownloadManager.Initialize();
            _items.Clear();
            var all = DownloadManager.GetRecent(2000); // беремо більше, показуємо тільки Completed
            var completed = all.Where(d => d.Status == "Completed");
            var term = _searchTextBox?.Text;
            if (!string.IsNullOrWhiteSpace(term))
            {
                term = term.Trim();
                completed = completed.Where(d => d.FileName.Contains(term, StringComparison.OrdinalIgnoreCase) || d.Url.Contains(term, StringComparison.OrdinalIgnoreCase));
            }
            foreach (var d in completed.OrderByDescending(x => x.EndTime ?? x.StartTime))
                _items.Add(new DownloadHistoryItemViewModel(d));
        }

        private void ClearOld()
        {
            DownloadManager.ArchiveOld(DateTime.UtcNow.AddDays(-30));
            Reload();
        }

        private void OnStatus(object? sender, DownloadItem e)
        {
            if (e.Status == "Completed" || e.Status == "Pending" || e.Status == "Downloading")
            {
                Upsert(e);
            }
        }
        private void OnProgress(object? sender, DownloadItem e)
        {
            Upsert(e);
        }
        private void Upsert(DownloadItem e)
        {
            if (e.Status == "Completed" || e.Status == "Pending" || e.Status == "Downloading")
            {
                var term = _searchTextBox?.Text;
                if (!string.IsNullOrWhiteSpace(term))
                {
                    if (!e.FileName.Contains(term, StringComparison.OrdinalIgnoreCase) && !e.Url.Contains(term, StringComparison.OrdinalIgnoreCase))
                        return;
                }
                var existing = _items.FirstOrDefault(x => x.Id == e.Id);
                if (existing == null)
                {
                    _items.Insert(0, new DownloadHistoryItemViewModel(e));
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
