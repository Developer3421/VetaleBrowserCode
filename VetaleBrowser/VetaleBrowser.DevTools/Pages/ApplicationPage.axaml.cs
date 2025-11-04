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
    public partial class ApplicationPage : UserControl
    {
        private ListBox? _storageTypesList;
        private ListBox? _storageDataList;
        private TextBlock? _storageTypeTitle;
        private readonly DevToolsDataService _dataService = new();
        private readonly WebViewWorkerService _workerService;
        private readonly Dictionary<string, List<StorageItem>> _storageData = new();

        public ApplicationPage()
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
            _storageTypesList = this.FindControl<ListBox>("StorageTypesList");
            _storageDataList = this.FindControl<ListBox>("StorageDataList");
            _storageTypeTitle = this.FindControl<TextBlock>("StorageTypeTitle");
        }

        private async void CaptureStorageFromWebView(object? sender, RoutedEventArgs e)
        {
            try
            {
                EnsureLocalWebViewAttached();
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

                // Refresh UI if selected type exists
                if (_storageTypesList?.SelectedItem is ListBoxItem li)
                {
                    DisplayStorageType(li.Tag?.ToString() ?? string.Empty);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ApplicationPage] Error capturing storage: {ex.Message}");
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
                _storageTypeTitle.Text = storageType;

            if (_storageDataList != null)
            {
                _storageDataList.Items.Clear();
                if (_storageData.TryGetValue(storageType, out var items))
                {
                    foreach (var item in items)
                    {
                        _storageDataList.Items.Add($"{item.Key} = {item.EncryptedValue}");
                    }
                }
            }
        }

        private void ClearAllStorage(object? sender, RoutedEventArgs e)
        {
            _storageData.Clear();
            _storageDataList?.Items.Clear();
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
                Debug.WriteLine($"[ApplicationPage] EnsureLocalWebViewAttached error: {ex.Message}");
            }

            // Fallback: attach to active tab
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
                Debug.WriteLine($"[ApplicationPage] EnsureActiveTabSet error: {ex.Message}");
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
