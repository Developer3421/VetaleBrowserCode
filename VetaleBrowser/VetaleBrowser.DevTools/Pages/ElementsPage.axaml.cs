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
using Avalonia.Controls.ApplicationLifetimes;
using VetaleBrowser.VetaleBrowser.DevTools.Services;
using Avalonia.VisualTree;

namespace VetaleBrowser.VetaleBrowser.DevTools.Pages
{
    public partial class ElementsPage : UserControl
    {
        private TreeView? _domTree;
        private TextBox? _attributesViewer;
        private TextBox? _stylesViewer;
        private readonly DevToolsDataService _dataService = new();
        private readonly WebViewWorkerService _workerService;
        private List<DomElement> _domElements = new();

        public ElementsPage()
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
            _domTree = this.FindControl<TreeView>("DomTree");
            _attributesViewer = this.FindControl<TextBox>("AttributesViewer");
            _stylesViewer = this.FindControl<TextBox>("StylesViewer");
        }

        private async void CaptureDomFromWebView(object? sender, RoutedEventArgs e)
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
                _workerService?.SyncWithMainWindow();
                
                var url = _workerService?.CurrentUrl;
                if (string.IsNullOrEmpty(url))
                {
                    Debug.WriteLine("[ElementsPage] No URL available from current tab");
                    ShowMessage("No active tab found. Please open a webpage first.");
                    if (_attributesViewer != null)
                    {
                        _attributesViewer.Text = "No active tab";
                    }
                    return;
                }
                
                Debug.WriteLine($"[ElementsPage] Capturing DOM from URL: {url} (headless Playwright)");
                
                _domElements = await _workerService.CaptureDomStructureAsync();
                
                Debug.WriteLine($"[ElementsPage] Captured {_domElements.Count} DOM elements");
                
                // Populate tree on UI thread
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
                Debug.WriteLine($"[ElementsPage] Error capturing DOM: {ex.Message}");
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
            Debug.WriteLine($"[ElementsPage] {message}");
        }

        private void PopulateDomTree()
        {
            if (_domTree == null) return;

            var rootItems = new Dictionary<string, TreeViewItem>();
            _domTree.Items.Clear();

            foreach (var element in _domElements.OrderBy(e => e.ElementPath))
            {
                var item = new TreeViewItem
                {
                    Header = $"<{element.TagName}>",
                    Tag = element
                };

                var path = element.ElementPath ?? string.Empty;
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
            if (_domTree?.SelectedItem is TreeViewItem item && item.Tag is DomElement element)
            {
                DisplayElementDetails(element);
            }
        }

        private void DisplayElementDetails(DomElement element)
        {
            // Display attributes
            if (_attributesViewer != null)
            {
                try
                {
                    var attrs = JsonSerializer.Deserialize<Dictionary<string, string>>(element.Attributes ?? "{}");
                    if (attrs != null)
                    {
                        _attributesViewer.Text = string.Join("\n",
                            attrs.Select(kvp => $"{kvp.Key} = \"{kvp.Value}\""));
                    }
                }
                catch
                {
                    _attributesViewer.Text = element.Attributes ?? string.Empty;
                }
            }

            // Display computed styles
            if (_stylesViewer != null)
            {
                try
                {
                    var styles = JsonSerializer.Deserialize<Dictionary<string, string>>(element.ComputedStyles ?? "{}");
                    if (styles != null)
                    {
                        _stylesViewer.Text = string.Join("\n",
                            styles.Select(kvp => $"{kvp.Key}: {kvp.Value};"));
                    }
                }
                catch
                {
                    _stylesViewer.Text = element.ComputedStyles ?? string.Empty;
                }
            }
        }

        private void ClearDomTree(object? sender, RoutedEventArgs e)
        {
            _domElements.Clear();
            _domTree?.Items.Clear();
            if (_attributesViewer != null)
                _attributesViewer.Text = string.Empty;
            if (_stylesViewer != null)
                _stylesViewer.Text = string.Empty;
            Debug.WriteLine("[ElementsPage] DOM tree cleared");
        }


        private static TControl? FindSiblingOrParent<TControl>(Control start) where TControl : Control
        {
            // Walk up to window
            var root = TopLevel.GetTopLevel(start) as Window;
            if (root == null) return null;

            return FindControlRecursive<TControl>(root);
        }

        private static TControl? FindControlRecursive<TControl>(Control ctl) where TControl : Control
        {
            if (ctl is TControl match) return match;

            if (ctl is Panel pnl)
            {
                foreach (var child in pnl.Children)
                {
                    if (child is Control c)
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
            else if (ctl is Decorator dec && dec.Child is Control dchild)
            {
                var found = FindControlRecursive<TControl>(dchild);
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

