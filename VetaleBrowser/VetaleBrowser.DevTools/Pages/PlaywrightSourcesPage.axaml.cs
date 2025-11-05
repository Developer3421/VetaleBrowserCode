using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using VetaleBrowser.VetaleBrowser.Database.Services;
using VetaleBrowser.VetaleBrowser.Database.Models;
using VetaleBrowser.VetaleBrowser.DevTools.Services;


namespace VetaleBrowser.VetaleBrowser.DevTools.Pages
{
    public partial class PlaywrightSourcesPage : UserControl
    {
        private TreeView? _fileTree;
        private TextBox? _codeViewer;
        private TextBlock? _fileNameText;
        private TextBlock? _fileTypeText;
        private TextBlock? _fileSizeText;
        private ComboBox? _fileTypeFilter;
        private TextBox? _scriptInput;
        private TextBlock? _scriptResultText;
        private Button? _btnCaptureSources;
        private Button? _btnGetHtml;
        private Button? _btnExecute;
        
        private readonly WebViewWorkerService _webViewService;
        private readonly PlaywrightDevToolsService _playwrightService;
        private readonly Dictionary<string, PageResource> _resources = new();
        private string _currentFilter = "all";

        public PlaywrightSourcesPage()
        {
            // Use singleton instances to prevent multiple Chromium windows
            _webViewService = WebViewWorkerService.GetInstance(new DevToolsDataService());
            _playwrightService = PlaywrightDevToolsService.GetInstance(new DevToolsDataService());
            _playwrightService.ResourceCaptured += OnResourceCaptured;

            InitializeComponent();
            InitializeControls();
            AttachToLocalWebView();
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
            _fileTypeText = this.FindControl<TextBlock>("FileTypeText");
            _fileSizeText = this.FindControl<TextBlock>("FileSizeText");
            _fileTypeFilter = this.FindControl<ComboBox>("FileTypeFilter");
            _scriptInput = this.FindControl<TextBox>("ScriptInput");
            _scriptResultText = this.FindControl<TextBlock>("ScriptResultText");
            _btnCaptureSources = this.FindControl<Button>("CaptureSourcesButton");
            _btnGetHtml = this.FindControl<Button>("GetHtmlButton");
            _btnExecute = this.FindControl<Button>("ExecuteButton");

            // Explicitly wire button events for reliability
            if (_btnCaptureSources != null) _btnCaptureSources.Click += OnCaptureSources;
            if (_btnGetHtml != null) _btnGetHtml.Click += OnGetHtml;
            if (_btnExecute != null) _btnExecute.Click += OnExecuteScript;

            if (_fileTypeFilter != null) _fileTypeFilter.SelectionChanged += OnFilterChanged;
            if (_fileTree != null) _fileTree.SelectionChanged += OnFileSelected;
        }

        private void AttachToLocalWebView()
        {
            // No longer needed - service automatically uses current tab URL
            Debug.WriteLine("[PlaywrightSourcesPage] Using current tab URL automatically via WebViewWorkerService");
        }

        private async void OnCaptureSources(object? sender, RoutedEventArgs e)
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
                _webViewService?.SyncWithMainWindow();
                
                // Get URL from WebView Worker
                var url = _webViewService.CurrentUrl;
                if (string.IsNullOrEmpty(url))
                {
                    Debug.WriteLine("[PlaywrightSourcesPage] No URL available from current tab");
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

                Debug.WriteLine($"[PlaywrightSourcesPage] Capturing sources from URL: {url} (headless Playwright)");
                
                // Use Playwright to capture page resources
                await _playwrightService.NavigateAsync(url);
                var list = await _playwrightService.CapturePageResourcesAsync();
                
                _resources.Clear();
                
                foreach (var res in list)
                {
                    var key = string.IsNullOrWhiteSpace(res.Url) ? $"{res.Type}_{_resources.Count}" : res.Url;
                    _resources[key] = res;
                }
                
                Debug.WriteLine($"[PlaywrightSourcesPage] Captured {_resources.Count} resources");
                
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
                Debug.WriteLine($"[PlaywrightSourcesPage] Error capturing sources: {ex.Message}");
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
            Debug.WriteLine($"[PlaywrightSourcesPage] {message}");
        }

        private async void OnGetHtml(object? sender, RoutedEventArgs e)
        {
            try
            {
                // Get URL from WebView Worker
                var url = _webViewService.CurrentUrl;
                if (string.IsNullOrEmpty(url))
                {
                    Debug.WriteLine("[PlaywrightSourcesPage] No URL available from WebView Worker");
                    return;
                }

                
                // Use Playwright to get page HTML
                await _playwrightService.NavigateAsync(url);
                var html = await _playwrightService.GetPageHtmlAsync();
                
                if (_codeViewer != null)
                    _codeViewer.Text = html ?? "";
                
                if (_fileNameText != null)
                    _fileNameText.Text = "Page HTML";
                
                if (_fileTypeText != null)
                    _fileTypeText.Text = "text/html";
                
                if (_fileSizeText != null && html != null)
                    _fileSizeText.Text = $"{html.Length / 1024.0:F2} KB";
                
                Debug.WriteLine($"[PlaywrightSourcesPage] Retrieved HTML ({(html?.Length ?? 0)} bytes)");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PlaywrightSourcesPage] Error getting HTML: {ex.Message}");
            }
        }

        private void OnFilterChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (_fileTypeFilter?.SelectedItem is ComboBoxItem item)
            {
                _currentFilter = item.Tag?.ToString() ?? "all";
                PopulateFileTree();
            }
        }

        private void PopulateFileTree()
        {
            if (_fileTree == null) return;
            
            _fileTree.Items.Clear();

            var filtered = _currentFilter == "all" 
                ? _resources 
                : _resources.Where(kv => kv.Value.Type.Contains(_currentFilter)).ToDictionary(kv => kv.Key, kv => kv.Value);

            // Групуємо за типом
            var groups = filtered.GroupBy(kv => kv.Value.Type);

            foreach (var group in groups.OrderBy(g => g.Key))
            {
                var groupItem = new TreeViewItem
                {
                    Header = $"{group.Key} ({group.Count()})",
                    IsExpanded = true
                };

                foreach (var kvp in group.OrderBy(kv => kv.Key))
                {
                    var fileName = kvp.Key;
                    if (fileName.Length > 60)
                    {
                        var lastSlash = fileName.LastIndexOf('/');
                        if (lastSlash > 0)
                            fileName = ".../" + fileName.Substring(lastSlash + 1);
                        else
                            fileName = "..." + fileName.Substring(fileName.Length - 57);
                    }

                    var fileItem = new TreeViewItem
                    {
                        Header = fileName,
                        Tag = kvp.Key
                    };

                    groupItem.Items.Add(fileItem);
                }

                _fileTree.Items.Add(groupItem);
            }
        }

        private void OnFileSelected(object? sender, SelectionChangedEventArgs e)
        {
            if (_fileTree?.SelectedItem is TreeViewItem item && item.Tag is string key)
            {
                if (_resources.TryGetValue(key, out var res))
                {
                    DisplayResource(key, res);
                }
            }
        }

        private void DisplayResource(string key, PageResource resource)
        {
            if (_fileNameText != null)
                _fileNameText.Text = key;

            if (_fileTypeText != null)
                _fileTypeText.Text = resource.Type;

            var content = resource.EncryptedContent ?? "";
            if (_fileSizeText != null)
                _fileSizeText.Text = $"{content.Length / 1024.0:F2} KB";

            if (_codeViewer != null)
                _codeViewer.Text = content;
        }

        private async void OnExecuteScript(object? sender, RoutedEventArgs e)
        {
            if (_scriptInput == null || string.IsNullOrWhiteSpace(_scriptInput.Text))
                return;

            try
            {
                // Get URL from WebView Worker
                var url = _webViewService.CurrentUrl;
                if (string.IsNullOrEmpty(url))
                {
                    if (_scriptResultText != null)
                    {
                        _scriptResultText.Text = "Error: No URL available from WebView Worker";
                        _scriptResultText.Foreground = Avalonia.Media.Brushes.Red;
                    }
                    return;
                }

                
                // Ensure we're on the right page
                await _playwrightService.NavigateAsync(url);
                var result = await _playwrightService.ExecuteScriptAsync(_scriptInput.Text);

                if (_scriptResultText != null)
                {
                    _scriptResultText.Text = result != null && result.Length > 200
                        ? result.Substring(0, 200) + "..."
                        : result ?? "";
                    _scriptResultText.Foreground = Avalonia.Media.Brushes.Green;
                }

                Debug.WriteLine($"[PlaywrightSourcesPage] Script executed successfully");
            }
            catch (Exception ex)
            {
                if (_scriptResultText != null)
                {
                    _scriptResultText.Text = $"Error: {ex.Message}";
                    _scriptResultText.Foreground = Avalonia.Media.Brushes.Red;
                }
                
                Debug.WriteLine($"[PlaywrightSourcesPage] Script execution error: {ex.Message}");
            }
        }

        private void OnResourceCaptured(object? sender, PageResource resource)
        {
            Debug.WriteLine($"[PlaywrightSourcesPage] Resource captured: {resource.Type} - {resource.Url}");
        }
    }
}
