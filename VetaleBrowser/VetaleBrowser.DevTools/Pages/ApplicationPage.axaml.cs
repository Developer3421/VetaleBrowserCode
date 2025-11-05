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
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.VisualTree;

namespace VetaleBrowser.VetaleBrowser.DevTools.Pages
{
    // DevTools Application Page
    public partial class ApplicationPage : UserControl
    {
        private ListBox? _storageTypesList;
        private ListBox? _storageDataList;
        private TextBlock? _storageTypeTitle;
        private readonly DevToolsDataService _dataService = new();
        private WebViewWorkerService? _workerService;
        private readonly Dictionary<string, List<StorageItem>> _storageData = new();

        public ApplicationPage()
        {
            // Use singleton instance to prevent multiple Playwright windows
            var dataService = _dataService;
            _workerService = WebViewWorkerService.GetInstance(dataService);
            InitializeComponent();
            InitializeControls();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void InitializeControls()
        {
            _storageTypesList = this.FindControl<ListBox>("StorageTypesList");
            _storageDataList = this.FindControl<ListBox>("StorageDataList");
            _storageTypeTitle = this.FindControl<TextBlock>("StorageTypeTitle");
        }

        private async void CaptureStorageFromWebView(object? sender, RoutedEventArgs e)
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
                
                // Automatically sync with current active tab
                _workerService?.SyncWithMainWindow();
                
                var url = _workerService?.CurrentUrl;
                if (string.IsNullOrEmpty(url))
                {
                    Debug.WriteLine("[ApplicationPage] No URL available from current tab");
                    ShowMessage("No active tab found. Please open a webpage first.");
                    return;
                }
                
                Debug.WriteLine($"[ApplicationPage] Capturing storage from URL: {url} (headless Playwright)");
                
                // Use headless Playwright via WebViewWorkerService
                var items = await _workerService.CaptureStorageAsync();
                _storageData.Clear();
                foreach (var it in items)
                {
                    if (!_storageData.TryGetValue(it.StorageType, out var list))
                    {
                        list = new List<StorageItem>();
                        _storageData[it.StorageType] = list;
                    }
                    list.Add(it);
                }

                Debug.WriteLine($"[ApplicationPage] Captured {items.Count} storage items - Types: {string.Join(", ", _storageData.Keys)}");

                // Auto-select localStorage if available, otherwise first item
                if (_storageTypesList != null)
                {
                    if (_storageData.ContainsKey("localStorage"))
                    {
                        // Find and select localStorage item
                        for (int i = 0; i < _storageTypesList.Items.Count; i++)
                        {
                            if (_storageTypesList.Items[i] is ListBoxItem item && 
                                item.Tag?.ToString() == "localStorage")
                            {
                                _storageTypesList.SelectedIndex = i;
                                break;
                            }
                        }
                    }
                    else if (_storageTypesList.SelectedIndex == -1 && _storageTypesList.Items.Count > 0)
                    {
                        _storageTypesList.SelectedIndex = 0;
                    }
                    else if (_storageTypesList.SelectedItem is ListBoxItem li)
                    {
                        // Refresh current selection
                        DisplayStorageType(li.Tag?.ToString() ?? string.Empty);
                    }
                }
                
                ShowMessage($"✓ Captured {items.Count} storage items from {url}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ApplicationPage] Error capturing storage: {ex.Message}");
                ShowMessage($"❌ Error: {ex.Message}");
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
            Debug.WriteLine($"[ApplicationPage] {message}");
            // Could also show in UI status bar if needed
        }

        private void OnStorageTypeSelected(object? sender, SelectionChangedEventArgs e)
        {
            if (_storageTypesList?.SelectedItem is ListBoxItem item)
            {
                var storageType = item.Tag?.ToString() ?? "";
                DisplayStorageType(storageType);
            }
        }

        private void DisplayStorageType(string storageType)
        {
            if (_storageTypeTitle != null)
                _storageTypeTitle.Text = $"{storageType} ({(_storageData.ContainsKey(storageType) ? _storageData[storageType].Count : 0)} items)";

            if (_storageDataList != null)
            {
                _storageDataList.Items.Clear();
                if (_storageData.TryGetValue(storageType, out var items))
                {
                    Debug.WriteLine($"[ApplicationPage] Displaying {items.Count} items for {storageType}");
                    foreach (var item in items)
                    {
                        var displayText = $"{item.Key} = {item.EncryptedValue}";
                        _storageDataList.Items.Add(displayText);
                        Debug.WriteLine($"[ApplicationPage] Added item: {displayText}");
                    }
                }
                else
                {
                    Debug.WriteLine($"[ApplicationPage] No items found for {storageType}");
                    _storageDataList.Items.Add("No data captured yet. Click 'Capture Storage' button.");
                }
            }
        }

        private void ClearAllStorage(object? sender, RoutedEventArgs e)
        {
            _storageData.Clear();
            _storageDataList?.Items.Clear();
        }


        private static TControl? FindSiblingOrParent<TControl>(Control start) where TControl : Control
        {
            var root = start.GetVisualRoot() as Window;
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
