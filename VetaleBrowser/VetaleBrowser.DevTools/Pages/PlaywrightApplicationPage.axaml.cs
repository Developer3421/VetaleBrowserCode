using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using VetaleBrowser.VetaleBrowser.Database.Services;
using VetaleBrowser.VetaleBrowser.Database.Models;
using VetaleBrowser.VetaleBrowser.DevTools.Services;

namespace VetaleBrowser.VetaleBrowser.DevTools.Pages
{
    public partial class PlaywrightApplicationPage : UserControl
    {
        private ListBox? _storageTypesList;
        private ListBox? _storageDataList;
        private TextBlock? _storageTypeTitle;
        private TextBlock? _selectedKeyText;
        private TextBox? _selectedValueText;
        private Button? _btnCaptureStorage;
        private Button? _btnClearCookies;
        
        private readonly WebViewWorkerService _webViewService;
        private readonly PlaywrightDevToolsService _playwrightService;
        private readonly Dictionary<string, List<StorageItem>> _storageData = new();

        public PlaywrightApplicationPage()
        {
            // Use singleton instances to prevent multiple Chromium windows
            _webViewService = WebViewWorkerService.GetInstance(new DevToolsDataService());
            _playwrightService = PlaywrightDevToolsService.GetInstance(new DevToolsDataService());
            _playwrightService.StorageItemCaptured += OnStorageItemCaptured;

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
            _storageTypesList = this.FindControl<ListBox>("StorageTypesList");
            _storageDataList = this.FindControl<ListBox>("StorageDataList");
            _storageTypeTitle = this.FindControl<TextBlock>("StorageTypeTitle");
            _selectedKeyText = this.FindControl<TextBlock>("SelectedKeyText");
            _selectedValueText = this.FindControl<TextBox>("SelectedValueText");
            _btnCaptureStorage = this.FindControl<Button>("CaptureStorageButton");
            _btnClearCookies = this.FindControl<Button>("ClearCookiesButton");

            if (_btnCaptureStorage != null) _btnCaptureStorage.Click += OnCaptureStorage;
            if (_btnClearCookies != null) _btnClearCookies.Click += OnClearCookies;
        }

        private void AttachToLocalWebView()
        {
            // No longer needed - service automatically uses current tab URL
            Debug.WriteLine("[PlaywrightApplicationPage] Using current tab URL automatically via WebViewWorkerService");
        }

        private async void OnCaptureStorage(object? sender, RoutedEventArgs e)
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
                _webViewService?.SyncWithMainWindow();
                
                // Get URL from WebView Worker
                var url = _webViewService.CurrentUrl;
                if (string.IsNullOrEmpty(url))
                {
                    Debug.WriteLine("[PlaywrightApplicationPage] No URL available from current tab");
                    ShowMessage("No active tab found. Please open a webpage first.");
                    return;
                }

                Debug.WriteLine($"[PlaywrightApplicationPage] Capturing storage from URL: {url} (headless Playwright)");
                
                // Use Playwright to capture storage
                await _playwrightService.NavigateAsync(url);
                var items = await _playwrightService.CaptureStorageAsync();
                
                _storageData.Clear();
                
                foreach (var item in items)
                {
                    if (!_storageData.TryGetValue(item.StorageType, out var list))
                    {
                        list = new List<StorageItem>();
                        _storageData[item.StorageType] = list;
                    }
                    list.Add(item);
                }

                Debug.WriteLine($"[PlaywrightApplicationPage] Captured {items.Count} storage items - Types: {string.Join(", ", _storageData.Keys)}");

                // Auto-select localStorage if available, otherwise first item
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
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
                            DisplayStorageType(li.Tag?.ToString() ?? string.Empty);
                        }
                    }
                });
                
                ShowMessage($"✓ Captured {items.Count} storage items from {url}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PlaywrightApplicationPage] Error capturing storage: {ex.Message}");
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
            Debug.WriteLine($"[PlaywrightApplicationPage] {message}");
        }

        private async void OnClearCookies(object? sender, RoutedEventArgs e)
        {
            try
            {
                // Get URL from WebView Worker
                var url = _webViewService.CurrentUrl;
                if (string.IsNullOrEmpty(url))
                {
                    Debug.WriteLine("[PlaywrightApplicationPage] No URL available from WebView Worker");
                    return;
                }

                
                // Use Playwright to clear cookies
                await _playwrightService.NavigateAsync(url);
                await _playwrightService.ClearCookiesAsync();
                
                Debug.WriteLine("[PlaywrightApplicationPage] Cookies cleared");
                
                // Оновити відображення якщо cookies вибрані
                if (_storageTypesList?.SelectedItem is ListBoxItem li && li.Tag?.ToString() == "cookies")
                {
                    OnCaptureStorage(sender, e);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PlaywrightApplicationPage] Error clearing cookies: {ex.Message}");
            }
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
            {
                var count = _storageData.ContainsKey(storageType) ? _storageData[storageType].Count : 0;
                _storageTypeTitle.Text = $"{storageType} ({count} items)";
            }

            if (_storageDataList != null)
            {
                _storageDataList.Items.Clear();
                if (_storageData.TryGetValue(storageType, out var items))
                {
                    Debug.WriteLine($"[PlaywrightApplicationPage] Displaying {items.Count} items for {storageType}");
                    foreach (var item in items)
                    {
                        var displayText = $"{item.Key}";
                        if (item.EncryptedValue.Length > 50)
                            displayText += $" = {item.EncryptedValue.Substring(0, 50)}...";
                        else
                            displayText += $" = {item.EncryptedValue}";

                        var listItem = new ListBoxItem
                        {
                            Content = displayText,
                            Tag = item
                        };
                        _storageDataList.Items.Add(listItem);
                    }
                }
            }
        }

        private void OnStorageItemSelected(object? sender, SelectionChangedEventArgs e)
        {
            if (_storageDataList?.SelectedItem is ListBoxItem item && item.Tag is StorageItem storageItem)
            {
                if (_selectedKeyText != null)
                    _selectedKeyText.Text = storageItem.Key;

                if (_selectedValueText != null)
                    _selectedValueText.Text = storageItem.EncryptedValue;
            }
        }

        private void OnStorageItemCaptured(object? sender, StorageItem item)
        {
            Debug.WriteLine($"[PlaywrightApplicationPage] Storage item captured: {item.StorageType} - {item.Key}");
        }
    }
}
