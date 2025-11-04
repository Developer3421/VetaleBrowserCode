using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using System;
using System.Diagnostics;
using VetaleBrowser.VetaleBrowser.Database.Services;
using Avalonia.Controls.ApplicationLifetimes;
using VetaleBrowser.VetaleBrowser.DevTools.Services;
using Avalonia.VisualTree;

namespace VetaleBrowser.VetaleBrowser.DevTools.Pages
{
    public partial class PerformancePage : UserControl
    {
        private TextBlock? _loadTimeText;
        private TextBlock? _domLoadTimeText;
        private TextBlock? _firstPaintText;
        private TextBlock? _memoryText;
        private TextBlock? _urlText;
        private ListBox? _resourceTimingsList;
 
        private readonly IDevToolsDataService _devToolsDataService;
        private readonly WebViewWorkerService _workerService = new(new DevToolsDataService());

        public PerformancePage()
        {
            _devToolsDataService = new DevToolsDataService();

            InitializeComponent();
            InitializeControls();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void InitializeControls()
        {
            _loadTimeText = this.FindControl<TextBlock>("LoadTimeText");
            _domLoadTimeText = this.FindControl<TextBlock>("DomLoadTimeText");
            _firstPaintText = this.FindControl<TextBlock>("FirstPaintText");
            _memoryText = this.FindControl<TextBlock>("MemoryText");
            _urlText = this.FindControl<TextBlock>("UrlText");
            _resourceTimingsList = this.FindControl<ListBox>("ResourceTimingsList");
        }

        private async void CapturePerformanceFromWebView(object? sender, RoutedEventArgs e)
        {
            try
            {
                EnsureLocalWebViewAttached();
                var snapshot = await _workerService.CapturePerformanceSnapshotAsync();
                if (snapshot != null)
                {
                    DisplaySnapshot(snapshot);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PerformancePage] Error capturing performance: {ex.Message}");
            }
        }

        private void DisplaySnapshot(VetaleBrowser.Database.Models.PerformanceSnapshot snapshot)
        {
            if (_loadTimeText != null)
                _loadTimeText.Text = $"{snapshot.LoadTime} ms";

            if (_domLoadTimeText != null)
                _domLoadTimeText.Text = $"{snapshot.DomContentLoadedTime} ms";

            if (_firstPaintText != null)
                _firstPaintText.Text = $"{snapshot.FirstPaintTime} ms";

            if (_memoryText != null)
            {
                var memoryMb = snapshot.MemoryUsed / (1024.0 * 1024.0);
                _memoryText.Text = $"{memoryMb:F2} MB";
            }

            if (_urlText != null)
                _urlText.Text = snapshot.Url;

            if (_resourceTimingsList != null)
            {
                _resourceTimingsList.Items.Clear();
                if (!string.IsNullOrWhiteSpace(snapshot.ResourceTimings))
                {
                    _resourceTimingsList.Items.Add(snapshot.ResourceTimings);
                }
            }
        }

        private void EnsureLocalWebViewAttached()
        {
            try
            {
                var workerPage = FindSiblingOrParent<WebViewWorkerPage>(this);
                if (workerPage?.DevToolsLocalWebView != null)
                {
                    _workerService.AttachLocalWebView(workerPage.DevToolsLocalWebView);
                    return;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PerformancePage] EnsureLocalWebViewAttached error: {ex.Message}");
            }

            // Fallback to ActiveTab
            try
            {
                var main = GetMainWindow();
                if (main?.TabsManager?.Active != null)
                {
                    _workerService.ActiveTab = main.TabsManager.Active;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PerformancePage] EnsureActiveTabSet error: {ex.Message}");
            }
        }

        private static TControl? FindSiblingOrParent<TControl>(Control start) where TControl : Control
        {
            var root = start.GetVisualRoot() as Window;
            if (root == null) return null;
            return FindControlRecursive<TControl>(root);
        }

        private static TControl? FindControlRecursive<TControl>(Control control) where TControl : Control
        {
            if (control is TControl target) return target;

            if (control is Panel panel)
            {
                foreach (var child in panel.Children)
                {
                    if (child is Control childControl)
                    {
                        var result = FindControlRecursive<TControl>(childControl);
                        if (result != null) return result;
                    }
                }
            }
            else if (control is Decorator decorator && decorator.Child is Control decoratorChild)
            {
                var result = FindControlRecursive<TControl>(decoratorChild);
                if (result != null) return result;
            }
            else if (control is ContentControl contentControl && contentControl.Content is Control contentChild)
            {
                var result = FindControlRecursive<TControl>(contentChild);
                if (result != null) return result;
            }

            return null;
        }

        private MainWindow? GetMainWindow()
        {
            if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                foreach (var w in desktop.Windows)
                {
                    if (w is MainWindow mw) return mw;
                }
            }
            return null;
        }
    }
}
