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
        // Use singleton instance to prevent multiple Playwright windows
        private readonly WebViewWorkerService _workerService;

        public PerformancePage()
        {
            _devToolsDataService = new DevToolsDataService();
            _workerService = WebViewWorkerService.GetInstance(new DevToolsDataService());

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
            var button = sender as Button;
            var originalContent = button?.Content;
            
            try
            {
                // Show loading state
                if (button != null)
                {
                    button.IsEnabled = false;
                    button.Content = "⏳ Analyzing...";
                }
                
                // Clear previous data
                ClearPerformanceData();
                
                // Automatically sync with current active tab
                _workerService?.SyncWithMainWindow();
                
                var url = _workerService?.CurrentUrl;
                if (string.IsNullOrEmpty(url))
                {
                    Debug.WriteLine("[PerformancePage] No URL available from current tab");
                    ShowMessage("No active tab found. Please open a webpage first.");
                    if (_urlText != null)
                    {
                        _urlText.Text = "No active tab";
                    }
                    return;
                }
                
                Debug.WriteLine($"[PerformancePage] Capturing performance from URL: {url} (headless Playwright)");
                
                var snapshot = await _workerService.CapturePerformanceSnapshotAsync();
                if (snapshot != null)
                {
                    await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        DisplaySnapshot(snapshot);
                    });
                    ShowMessage($"✓ Performance analysis complete for {url}");
                }
                else
                {
                    ShowMessage("⚠ No performance data captured");
                    if (_urlText != null)
                    {
                        _urlText.Text = "Failed to capture performance data";
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PerformancePage] Error capturing performance: {ex.Message}");
                ShowMessage($"❌ Error: {ex.Message}");
                if (_urlText != null)
                {
                    _urlText.Text = $"Error: {ex.Message}";
                }
            }
            finally
            {
                // Restore button state
                if (button != null)
                {
                    button.IsEnabled = true;
                    button.Content = originalContent;
                }
            }
        }
        
        private void ClearPerformanceData()
        {
            if (_loadTimeText != null) _loadTimeText.Text = "---";
            if (_domLoadTimeText != null) _domLoadTimeText.Text = "---";
            if (_firstPaintText != null) _firstPaintText.Text = "---";
            if (_memoryText != null) _memoryText.Text = "---";
            if (_urlText != null) _urlText.Text = "Analyzing...";
            if (_resourceTimingsList != null) _resourceTimingsList.Items.Clear();
        }
        
        private void ShowMessage(string message)
        {
            Debug.WriteLine($"[PerformancePage] {message}");
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
