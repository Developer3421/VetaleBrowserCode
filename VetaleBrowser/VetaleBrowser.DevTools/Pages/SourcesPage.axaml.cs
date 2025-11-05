using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using VetaleBrowser.VetaleBrowser.Database.Services;
using VetaleBrowser.VetaleBrowser.Database.Models;
using Avalonia.Controls.ApplicationLifetimes;
using VetaleBrowser.VetaleBrowser.DevTools.Services;
using Avalonia.VisualTree;

namespace VetaleBrowser.VetaleBrowser.DevTools.Pages
{
    public partial class SourcesPage : UserControl
    {
        private TreeView? _fileTree;
        private TextBox? _codeViewer;
        private TextBlock? _fileNameText;
        private readonly DevToolsDataService _dataService = new();
        private readonly WebViewWorkerService _workerService;
        private readonly Dictionary<string, PageResource> _resources = new();

        public SourcesPage()
        {
            // Use singleton instance to prevent multiple Playwright windows
            _workerService = WebViewWorkerService.GetInstance(_dataService);
            InitializeComponent();
            InitializeControls();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void InitializeControls()
        {
            _fileTree = this.FindControl<TreeView>("FileTree");
            _codeViewer = this.FindControl<TextBox>("CodeViewer");
            _fileNameText = this.FindControl<TextBlock>("FileNameText");
        }

        private async void CaptureSourcesFromWebView(object? sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            var originalContent = button?.Content;
            
            try
            {
                // Show loading state
                if (button != null)
                {
                    button.IsEnabled = false;
                    button.Content = "⏳ Capturing...";
                }
                
                // Clear previous data
                if (_fileTree != null)
                {
                    _fileTree.Items.Clear();
                }
                if (_codeViewer != null)
                {
                    _codeViewer.Text = "Loading...";
                }
                
                // Automatically sync with current active tab
                _workerService?.SyncWithMainWindow();
                
                var url = _workerService?.CurrentUrl;
                if (string.IsNullOrEmpty(url))
                {
                    Debug.WriteLine("[SourcesPage] No URL available from current tab");
                    ShowMessage("No active tab found. Please open a webpage first.");
                    if (_codeViewer != null)
                    {
                        _codeViewer.Text = "No active tab";
                    }
                    if (_fileNameText != null)
                    {
                        _fileNameText.Text = "No active tab";
                    }
                    return;
                }
                
                Debug.WriteLine($"[SourcesPage] Capturing sources from URL: {url} (headless Playwright)");
                
                var list = await _workerService.CapturePageResourcesAsync();
                _resources.Clear();
                foreach (var res in list)
                {
                    var key = string.IsNullOrWhiteSpace(res.Url) ? res.Type : res.Url;
                    _resources[key] = res;
                }
                
                Debug.WriteLine($"[SourcesPage] Captured {_resources.Count} sources");
                
                // Update UI on UI thread
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    PopulateFileTree();
                });
                
                if (_codeViewer != null)
                {
                    _codeViewer.Text = $"Captured {_resources.Count} source files\nSelect a file to view its content";
                }
                if (_fileNameText != null)
                {
                    _fileNameText.Text = $"{_resources.Count} files";
                }
                
                ShowMessage($"✓ Captured {_resources.Count} sources from {url}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SourcesPage] Error capturing sources: {ex.Message}");
                ShowMessage($"❌ Error: {ex.Message}");
                if (_codeViewer != null)
                {
                    _codeViewer.Text = $"Error: {ex.Message}";
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
        
        private void ShowMessage(string message)
        {
            Debug.WriteLine($"[SourcesPage] {message}");
        }

        private void PopulateFileTree()
        {
            if (_fileTree == null) return;
            _fileTree.Items.Clear();
            foreach (var key in _resources.Keys.OrderBy(k => k))
            {
                _fileTree.Items.Add(new TreeViewItem { Header = key, Tag = key });
            }
        }

        private void OnFileSelected(object? sender, SelectionChangedEventArgs e)
        {
            if (_fileTree?.SelectedItem is TreeViewItem item && item.Tag is string key)
            {
                if (_resources.TryGetValue(key, out var res))
                {
                    _fileNameText!.Text = key;
                    _codeViewer!.Text = res.EncryptedContent ?? string.Empty;
                }
            }
        }


        private static TControl? FindSiblingOrParent<TControl>(Control start) where TControl : Control
        {
            var root = start.GetVisualRoot() as Window;
            if (root == null) return null;
            return FindControlRecursive<TControl>(root);
        }

        private static TControl? FindControlRecursive<TControl>(Control ctl) where TControl : Control
        {
            if (ctl is TControl m) return m;

            if (ctl is Panel pnl)
            {
                foreach (var ch in pnl.Children)
                {
                    if (ch is Control c)
                    {
                        var found = FindControlRecursive<TControl>(c);
                        if (found != null) return found;
                    }
                }
            }
            else if (ctl is ContentControl cc && cc.Content is Control inner)
            {
                var found = FindControlRecursive<TControl>(inner);
                if (found != null) return found;
            }
            else if (ctl is Decorator dec && dec.Child is Control child)
            {
                var found = FindControlRecursive<TControl>(child);
                if (found != null) return found;
            }

            return null;
        }

        private MainWindow? GetMainWindow()
        {
            if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                return desktop.Windows.FirstOrDefault(w => w is MainWindow) as MainWindow;
            }
            return null;
        }
    }
}
