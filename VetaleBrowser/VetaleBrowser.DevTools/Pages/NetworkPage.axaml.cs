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
    public partial class NetworkPage : UserControl
    {
        private ListBox? _networkResourcesList;
        private TextBlock? _resourceDetailsTitle;
        private TextBox? _resourceDetailsText;
        private readonly DevToolsDataService _dataService = new();
        private readonly WebViewWorkerService _workerService;
        private List<PageResource> _networkResources = new();

        public NetworkPage()
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
            _networkResourcesList = this.FindControl<ListBox>("NetworkResourcesList");
            _resourceDetailsTitle = this.FindControl<TextBlock>("ResourceDetailsTitle");
            _resourceDetailsText = this.FindControl<TextBox>("ResourceDetailsText");
        }

        private async void CaptureNetworkFromWebView(object? sender, RoutedEventArgs e)
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
                if (_networkResourcesList != null)
                {
                    _networkResourcesList.Items.Clear();
                }
                if (_resourceDetailsText != null)
                {
                    _resourceDetailsText.Text = "Loading...";
                }
                
                // Automatically sync with current active tab
                _workerService?.SyncWithMainWindow();
                
                var url = _workerService?.CurrentUrl;
                if (string.IsNullOrEmpty(url))
                {
                    Debug.WriteLine("[NetworkPage] No URL available from current tab");
                    ShowMessage("No active tab found. Please open a webpage first.");
                    if (_resourceDetailsText != null)
                    {
                        _resourceDetailsText.Text = "No active tab";
                    }
                    return;
                }
                
                Debug.WriteLine($"[NetworkPage] Capturing network resources from URL: {url} (headless Playwright)");
                
                _networkResources = await _workerService.CapturePageResourcesAsync();
                
                Debug.WriteLine($"[NetworkPage] Captured {_networkResources.Count} network resources");
                
                // Update UI on UI thread
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    DisplayNetworkResources();
                });
                
                if (_resourceDetailsText != null)
                {
                    _resourceDetailsText.Text = $"Captured {_networkResources.Count} resources\nSelect a resource to see details";
                }
                
                ShowMessage($"✓ Captured {_networkResources.Count} network resources from {url}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[NetworkPage] Error capturing network: {ex.Message}");
                ShowMessage($"❌ Error: {ex.Message}");
                if (_resourceDetailsText != null)
                {
                    _resourceDetailsText.Text = $"Error: {ex.Message}";
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
            Debug.WriteLine($"[NetworkPage] {message}");
        }

        private void DisplayNetworkResources()
        {
            if (_networkResourcesList == null) return;
            
            _networkResourcesList.Items.Clear();
            
            foreach (var resource in _networkResources.OrderBy(r => r.Type))
            {
                var item = CreateNetworkResourceItem(resource);
                _networkResourcesList.Items.Add(item);
            }
            
            Debug.WriteLine($"[NetworkPage] Displayed {_networkResources.Count} resources in UI");
        }

        private Grid CreateNetworkResourceItem(PageResource resource)
        {
            var grid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("50,*,100,100,100"),
                Margin = new Avalonia.Thickness(0, 2)
            };

            // Type icon
            var typeText = new TextBlock
            {
                Text = GetTypeIcon(resource.Type),
                FontSize = 12,
                Margin = new Avalonia.Thickness(5, 0)
            };
            Grid.SetColumn(typeText, 0);

            // URL
            var urlText = new TextBlock
            {
                Text = TruncateUrl(resource.Url ?? ""),
                FontSize = 11,
                TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis,
                Margin = new Avalonia.Thickness(5, 0)
            };
            Grid.SetColumn(urlText, 1);

            // Size
            var sizeText = new TextBlock
            {
                Text = FormatSize(resource.Size),
                FontSize = 11,
                Margin = new Avalonia.Thickness(5, 0)
            };
            Grid.SetColumn(sizeText, 2);

            // Duration (placeholder for now)
            var durationText = new TextBlock
            {
                Text = "-",
                FontSize = 11,
                Margin = new Avalonia.Thickness(5, 0)
            };
            Grid.SetColumn(durationText, 3);

            // Status
            var statusText = new TextBlock
            {
                Text = "✓",
                FontSize = 11,
                Foreground = Avalonia.Media.Brushes.Green,
                Margin = new Avalonia.Thickness(5, 0)
            };
            Grid.SetColumn(statusText, 4);

            grid.Children.Add(typeText);
            grid.Children.Add(urlText);
            grid.Children.Add(sizeText);
            grid.Children.Add(durationText);
            grid.Children.Add(statusText);

            grid.Tag = resource;
            
            return grid;
        }

        private void OnNetworkResourceSelected(object? sender, SelectionChangedEventArgs e)
        {
            if (_networkResourcesList?.SelectedItem is Grid grid && grid.Tag is PageResource resource)
            {
                DisplayResourceDetails(resource);
            }
        }

        private void DisplayResourceDetails(PageResource resource)
        {
            if (_resourceDetailsTitle != null)
                _resourceDetailsTitle.Text = $"Resource Details: {resource.Type}";

            if (_resourceDetailsText != null)
            {
                var details = $"URL: {resource.Url}\n" +
                             $"Type: {resource.Type}\n" +
                             $"Size: {FormatSize(resource.Size)}\n" +
                             $"Captured: {resource.CapturedAt:yyyy-MM-dd HH:mm:ss}\n" +
                             $"\nContent Preview:\n" +
                             $"{GetContentPreview(resource.EncryptedContent, 500)}";
                
                _resourceDetailsText.Text = details;
            }
        }

        private void ClearNetworkData(object? sender, RoutedEventArgs e)
        {
            _networkResources.Clear();
            _networkResourcesList?.Items.Clear();
            if (_resourceDetailsTitle != null)
                _resourceDetailsTitle.Text = "Select a resource to view details";
            if (_resourceDetailsText != null)
                _resourceDetailsText.Text = "";
        }

        private string GetTypeIcon(string type)
        {
            return type.ToLower() switch
            {
                "script" => "📜 JS",
                "inline-script" => "📝 JS",
                "stylesheet" => "🎨 CSS",
                "inline-style" => "🎨 CSS",
                "image" => "🖼️ IMG",
                _ => "📄"
            };
        }

        private string TruncateUrl(string url)
        {
            if (url.Length <= 80) return url;
            return url.Substring(0, 77) + "...";
        }

        private string FormatSize(long bytes)
        {
            if (bytes == 0) return "-";
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
            return $"{bytes / (1024.0 * 1024.0):F1} MB";
        }

        private string GetContentPreview(string? content, int maxLength)
        {
            if (string.IsNullOrEmpty(content)) return "(No content)";
            if (content.Length <= maxLength) return content;
            return content.Substring(0, maxLength) + "\n... (truncated)";
        }


        private static TControl? FindSiblingOrParent<TControl>(Control start) where TControl : Control
        {
            var root = TopLevel.GetTopLevel(start) as Window;
            if (root == null) return null;
            return FindControlRecursive<TControl>(root);
        }

        private static TControl? FindControlRecursive<TControl>(Control ctl) where TControl : Control
        {
            if (ctl is TControl match) return match;

            if (ctl is Panel panel)
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
            else if (ctl is Decorator decorator && decorator.Child is Control decoratorChild)
            {
                var result = FindControlRecursive<TControl>(decoratorChild);
                if (result != null) return result;
            }
            else if (ctl is ContentControl contentControl && contentControl.Content is Control contentChild)
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
                return desktop.Windows.FirstOrDefault(w => w is MainWindow) as MainWindow;
            }
            return null;
        }
    }
}


