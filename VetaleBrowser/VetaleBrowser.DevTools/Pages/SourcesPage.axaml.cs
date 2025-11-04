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
            _workerService = new WebViewWorkerService(_dataService);
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
            try
            {
                EnsureLocalWebViewAttached();
                var list = await _workerService.CapturePageResourcesAsync();
                _resources.Clear();
                foreach (var res in list)
                {
                    var key = string.IsNullOrWhiteSpace(res.Url) ? res.Type : res.Url;
                    _resources[key] = res;
                }
                PopulateFileTree();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SourcesPage] Error capturing sources: {ex.Message}");
            }
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

                var reg = DevToolsWebViewRegistry.CurrentWebView;
                if (reg != null)
                {
                    _workerService.AttachLocalWebView(reg);
                    return;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SourcesPage] EnsureLocalWebViewAttached error: {ex.Message}");
            }

            // Fallback: active tab
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
                Debug.WriteLine($"[SourcesPage] Fallback attach to ActiveTab error: {ex.Message}");
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
