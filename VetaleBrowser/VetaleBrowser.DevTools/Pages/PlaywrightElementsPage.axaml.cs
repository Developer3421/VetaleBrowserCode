using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using VetaleBrowser.VetaleBrowser.Database.Models;
using VetaleBrowser.VetaleBrowser.Database.Services;
using VetaleBrowser.VetaleBrowser.DevTools.Services;

namespace VetaleBrowser.VetaleBrowser.DevTools.Pages
{
    public partial class PlaywrightElementsPage : UserControl
    {
        private TreeView? _domTree;
        private TextBox? _attributesViewer;
        private TextBox? _stylesViewer;
        private TextBox? _selectorInput;
        private TextBlock? _elementTitle;
        private Button? _btnQuery;
        private Button? _btnCaptureDom;
        private Button? _btnRefresh;
        
        private readonly WebViewWorkerService _webViewService;
        private readonly PlaywrightDevToolsService _playwrightService;
        private List<DomElement> _domElements = new();

        public PlaywrightElementsPage()
        {
            // Use singleton instances to prevent multiple Chromium windows
            _webViewService = WebViewWorkerService.GetInstance(new DevToolsDataService());
            _playwrightService = PlaywrightDevToolsService.GetInstance(new DevToolsDataService());
            _playwrightService.DomElementCaptured += OnDomElementCaptured;
            
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
            _domTree = this.FindControl<TreeView>("DomTree");
            _attributesViewer = this.FindControl<TextBox>("AttributesViewer");
            _stylesViewer = this.FindControl<TextBox>("StylesViewer");
            _selectorInput = this.FindControl<TextBox>("SelectorInput");
            _elementTitle = this.FindControl<TextBlock>("ElementTitle");
            _btnQuery = this.FindControl<Button>("QueryButton");
            _btnCaptureDom = this.FindControl<Button>("CaptureDomButton");
            _btnRefresh = this.FindControl<Button>("RefreshButton");

            if (_btnQuery != null) _btnQuery.Click += OnQuery;
            if (_btnCaptureDom != null) _btnCaptureDom.Click += OnCaptureDom;
            if (_btnRefresh != null) _btnRefresh.Click += OnRefresh;
        }

        private void AttachToLocalWebView()
        {
            // No longer needed - service automatically uses current tab URL
            Debug.WriteLine("[PlaywrightElementsPage] Using current tab URL automatically via WebViewWorkerService");
        }

        private async void OnCaptureDom(object? sender, RoutedEventArgs e)
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
                if (_domTree != null)
                {
                    _domTree.Items.Clear();
                }
                if (_attributesViewer != null)
                {
                    _attributesViewer.Text = "Loading...";
                }
                
                // Automatically sync with current active tab
                _webViewService?.SyncWithMainWindow();
                
                // Get URL from WebView Worker
                var url = _webViewService.CurrentUrl;
                if (string.IsNullOrEmpty(url))
                {
                    Debug.WriteLine("[PlaywrightElementsPage] No URL available from current tab");
                    ShowMessage("No active tab found. Please open a webpage first.");
                    if (_attributesViewer != null)
                    {
                        _attributesViewer.Text = "No active tab";
                    }
                    return;
                }

                Debug.WriteLine($"[PlaywrightElementsPage] Capturing DOM from URL: {url} (headless Playwright)");
                
                // Use Playwright to capture DOM
                await _playwrightService.NavigateAsync(url);
                _domElements = await _playwrightService.CaptureDomStructureAsync();
                
                Debug.WriteLine($"[PlaywrightElementsPage] Captured {_domElements.Count} DOM elements");
                
                // Update UI on UI thread
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    PopulateDomTree();
                    
                    // Auto-expand first level
                    if (_domTree != null && _domTree.Items.Count > 0)
                    {
                        if (_domTree.Items[0] is TreeViewItem firstItem)
                        {
                            firstItem.IsExpanded = true;
                        }
                    }
                });
                
                if (_attributesViewer != null)
                {
                    _attributesViewer.Text = $"Captured {_domElements.Count} elements\nSelect an element to see details";
                }
                
                ShowMessage($"✓ Captured {_domElements.Count} DOM elements from {url}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PlaywrightElementsPage] Error capturing DOM: {ex.Message}");
                ShowMessage($"❌ Error: {ex.Message}");
                if (_attributesViewer != null)
                {
                    _attributesViewer.Text = $"Error: {ex.Message}";
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
            Debug.WriteLine($"[PlaywrightElementsPage] {message}");
        }

        private async void OnRefresh(object? sender, RoutedEventArgs e)
        {
            OnCaptureDom(sender, e);
        }

        private async void OnQuery(object? sender, RoutedEventArgs e)
        {
            if (_selectorInput == null || string.IsNullOrWhiteSpace(_selectorInput.Text))
                return;

            try
            {
                // Get URL from WebView Worker
                var url = _webViewService.CurrentUrl;
                if (string.IsNullOrEmpty(url))
                {
                    Debug.WriteLine("[PlaywrightElementsPage] No URL available from WebView Worker");
                    return;
                }

                Debug.WriteLine($"[PlaywrightElementsPage] Querying selector with Playwright: {_selectorInput.Text}");
                
                // Use Playwright to query selector
                await _playwrightService.NavigateAsync(url);
                var results = await _playwrightService.QuerySelectorAllAsync(_selectorInput.Text);
                
                _domElements = results;
                PopulateDomTree();
                Debug.WriteLine($"[PlaywrightElementsPage] Query found {_domElements.Count} elements");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PlaywrightElementsPage] Query error: {ex.Message}");
            }
        }

        private void PopulateDomTree()
        {
            if (_domTree == null) return;

            var rootItems = new Dictionary<string, TreeViewItem>();
            _domTree.Items.Clear();

            foreach (var element in _domElements.OrderBy(e => e.ElementPath))
            {
                var displayText = $"<{element.TagName}>";
                
                // Extract id and className from Attributes JSON
                try
                {
                    var attrs = JsonSerializer.Deserialize<Dictionary<string, string>>(element.Attributes ?? "{}");
                    if (attrs != null)
                    {
                        if (attrs.TryGetValue("id", out var id) && !string.IsNullOrEmpty(id))
                            displayText += $" #{id}";
                        if (attrs.TryGetValue("class", out var className) && !string.IsNullOrEmpty(className))
                            displayText += $" .{className.Split(' ').FirstOrDefault()}";
                    }
                }
                catch { }

                var item = new TreeViewItem
                {
                    Header = displayText,
                    Tag = element
                };

                var path = element.ElementPath ?? string.Empty;
                if (string.IsNullOrEmpty(path))
                {
                    _domTree.Items.Add(item);
                    continue;
                }

                var segments = path.Split('.', StringSplitOptions.RemoveEmptyEntries);
                string cumulative = string.Empty;
                TreeViewItem? parentItem = null;

                for (int i = 0; i < segments.Length; i++)
                {
                    cumulative = i == 0 ? segments[i] : $"{cumulative}.{segments[i]}";
                    if (!rootItems.TryGetValue(cumulative, out var node))
                    {
                        node = i == segments.Length - 1 ? item : new TreeViewItem { Header = "…" };
                        rootItems[cumulative] = node;

                        if (i == 0)
                        {
                            _domTree.Items.Add(node);
                        }
                        else if (parentItem != null)
                        {
                            if (parentItem.Items is System.Collections.IList list)
                                list.Add(node);
                        }
                    }
                    parentItem = node;
                }
            }
        }

        private void OnElementSelected(object? sender, SelectionChangedEventArgs e)
        {
            if (_domTree?.SelectedItem is not TreeViewItem item || item.Tag is not DomElement element)
                return;

            DisplayElementDetails(element);
        }

        private void DisplayElementDetails(DomElement element)
        {
            if (_elementTitle != null)
            {
                var title = $"<{element.TagName}>";
                try
                {
                    var attrs = JsonSerializer.Deserialize<Dictionary<string, string>>(element.Attributes ?? "{}");
                    if (attrs != null && attrs.TryGetValue("id", out var id) && !string.IsNullOrEmpty(id))
                    {
                        title += $" #{id}";
                    }
                }
                catch { }
                _elementTitle.Text = title;
            }

            // Відображення атрибутів
            if (_attributesViewer != null)
            {
                try
                {
                    var attrs = JsonSerializer.Deserialize<Dictionary<string, string>>(element.Attributes ?? "{}");
                    if (attrs != null && attrs.Count > 0)
                    {
                        _attributesViewer.Text = string.Join("\n", attrs.Select(kv => $"{kv.Key}: {kv.Value}"));
                    }
                    else
                    {
                        _attributesViewer.Text = "No attributes";
                    }
                }
                catch
                {
                    _attributesViewer.Text = element.Attributes ?? "No attributes";
                }
            }

            // Відображення стилів
            if (_stylesViewer != null)
            {
                try
                {
                    var styles = JsonSerializer.Deserialize<Dictionary<string, string>>(element.ComputedStyles ?? "{}");
                    if (styles != null && styles.Count > 0)
                    {
                        _stylesViewer.Text = string.Join("\n", styles
                            .OrderBy(kv => kv.Key)
                            .Select(kv => $"{kv.Key}: {kv.Value}"));
                    }
                    else
                    {
                        _stylesViewer.Text = "No styles";
                    }
                }
                catch
                {
                    _stylesViewer.Text = element.ComputedStyles ?? "No styles";
                }
            }
        }

        private void OnDomElementCaptured(object? sender, DomElement element)
        {
            Debug.WriteLine($"[PlaywrightElementsPage] Element captured: <{element.TagName}>");
        }
    }
}
