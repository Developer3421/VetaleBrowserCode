using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using System;
using System.Diagnostics;
using System.Linq;
using Avalonia.Controls.ApplicationLifetimes;
using VetaleBrowser.VetaleBrowser.Core.Scripts.Models;
using VetaleBrowser.VetaleBrowser.Core.Scripts.GlobalManagers;
using WebViewControl;
using VetaleBrowser.VetaleBrowser.DevTools.Services; // registry

namespace VetaleBrowser.VetaleBrowser.DevTools.Pages
{
    public partial class WebViewWorkerPage : UserControl
    {
        private WebView? _webView;
        private WebViewManager? _webViewManager;
        private TabWorker? _currentActiveTab;
        
        // Public property to access WebViewManager (for DevTools)
        public WebViewManager? WebViewManager => _webViewManager;
        // New: expose local WebView so other DevTools pages can attach to it
        public WebView? DevToolsLocalWebView => _webView;
        
        // UI Controls
        private Grid? _webViewContainer;
        private TextBlock? _placeholderText;
        private TextBox? _urlTextBox;
        private TextBlock? _currentTabTitle;
        private TextBlock? _currentTabUrl;
        private TextBlock? _statusIcon;
        private TextBlock? _statusText;
        private TextBlock? _loadingProgress;
        private Button? _backButton;
        private Button? _forwardButton;
        private Button? _loadCurrentButton;

        // Timer for monitoring active tab
        private readonly DispatcherTimer _monitorTimer = new() { Interval = TimeSpan.FromMilliseconds(500) };

        public WebViewWorkerPage()
        {
            InitializeComponent();
            InitializeControls();
            InitializeWebView();
            StartMonitoring();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void InitializeControls()
        {
            _webViewContainer = this.FindControl<Grid>("WebViewContainer");
            _placeholderText = this.FindControl<TextBlock>("PlaceholderText");
            _urlTextBox = this.FindControl<TextBox>("UrlTextBox");
            _currentTabTitle = this.FindControl<TextBlock>("CurrentTabTitle");
            _currentTabUrl = this.FindControl<TextBlock>("CurrentTabUrl");
            _statusIcon = this.FindControl<TextBlock>("StatusIcon");
            _statusText = this.FindControl<TextBlock>("StatusText");
            _loadingProgress = this.FindControl<TextBlock>("LoadingProgress");
            _backButton = this.FindControl<Button>("BackButton");
            _forwardButton = this.FindControl<Button>("ForwardButton");
            _loadCurrentButton = this.FindControl<Button>("LoadCurrentButton");
        }

        private void InitializeWebView()
        {
            try
            {
                Debug.WriteLine("[WebViewWorkerPage] Initializing WebView...");

                _webView = new WebView
                {
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch,
                };

                _webViewManager = new WebViewManager();
                _webViewManager.Initialize(_webView);

                // Subscribe to navigation events
                _webViewManager.Navigated += OnWebViewNavigated;

                // Subscribe to WebView property changes for detecting navigation
                if (_webView != null)
                {
                    _webView.PropertyChanged += OnWebViewPropertyChanged;
                    // Register to registry
                    DevToolsWebViewRegistry.CurrentWebView = _webView;
                }

                // Add WebView to container
                if (_webViewContainer != null && _placeholderText != null)
                {
                    _webViewContainer.Children.Add(_webView);
                    _placeholderText.IsVisible = false;
                }

                UpdateStatus("✅", "DevTools.WebViewWorker.WebViewReady");
                Debug.WriteLine("[WebViewWorkerPage] WebView initialized successfully");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebViewWorkerPage] Error initializing WebView: {ex}");
                UpdateStatus("❌", $"Error: {ex.Message}");
            }
        }

        private void StartMonitoring()
        {
            _monitorTimer.Tick += OnMonitorTick;
            _monitorTimer.Start();
        }

        private void OnMonitorTick(object? sender, EventArgs e)
        {
            try
            {
                var mainWindow = GetMainWindow();
                if (mainWindow == null) return;

                var tabsManager = GetTabsManager(mainWindow);
                if (tabsManager == null) return;

                var activeTab = tabsManager.Active;
                
                // If active tab changed, update UI and subscribe to new tab
                if (activeTab != _currentActiveTab)
                {
                    UnsubscribeFromCurrentTab();
                    _currentActiveTab = activeTab;
                    SubscribeToCurrentTab();
                    UpdateCurrentTabInfo();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebViewWorkerPage] Error in monitor tick: {ex}");
            }
        }

        private void SubscribeToCurrentTab()
        {
            if (_currentActiveTab == null) return;

            _currentActiveTab.TitleChanged += OnCurrentTabTitleChanged;
            _currentActiveTab.AddressChanged += OnCurrentTabAddressChanged;
        }

        private void UnsubscribeFromCurrentTab()
        {
            if (_currentActiveTab == null) return;

            _currentActiveTab.TitleChanged -= OnCurrentTabTitleChanged;
            _currentActiveTab.AddressChanged -= OnCurrentTabAddressChanged;
        }

        private void OnCurrentTabTitleChanged(object? sender, string? title)
        {
            Dispatcher.UIThread.Post(() => UpdateCurrentTabInfo());
        }

        private void OnCurrentTabAddressChanged(object? sender, string? address)
        {
            Dispatcher.UIThread.Post(() => UpdateCurrentTabInfo());
        }

        private void UpdateCurrentTabInfo()
        {
            if (_currentActiveTab == null)
            {
                if (_currentTabTitle != null)
                    _currentTabTitle.Text = Application.Current?.FindResource("DevTools.WebViewWorker.NoActiveTab")?.ToString() ?? "No active tab";
                
                if (_currentTabUrl != null)
                    _currentTabUrl.Text = "";

                if (_loadCurrentButton != null)
                    _loadCurrentButton.IsEnabled = false;

                return;
            }

            if (_currentTabTitle != null)
                _currentTabTitle.Text = _currentActiveTab.Title ?? "Untitled";

            if (_currentTabUrl != null)
                _currentTabUrl.Text = _currentActiveTab.Address ?? "";

            if (_loadCurrentButton != null)
                _loadCurrentButton.IsEnabled = !string.IsNullOrWhiteSpace(_currentActiveTab.Address);
        }

        private void OnWebViewPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            try
            {
                if (e.Property.Name == "CanGoBack" && _backButton != null)
                {
                    var canGoBack = _webView?.CanGoBack ?? false;
                    _backButton.IsEnabled = canGoBack;
                }
                else if (e.Property.Name == "CanGoForward" && _forwardButton != null)
                {
                    var canGoForward = _webView?.CanGoForward ?? false;
                    _forwardButton.IsEnabled = canGoForward;
                }
                else if (e.Property.Name == "Address")
                {
                    // Update UI when address changes
                    var newAddress = _webView?.Address;
                    if (!string.IsNullOrEmpty(newAddress) && _urlTextBox != null)
                    {
                        _urlTextBox.Text = newAddress;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebViewWorkerPage] Error in property changed: {ex}");
            }
        }

        private void OnWebViewNavigated(object? sender, string url)
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (_urlTextBox != null)
                    _urlTextBox.Text = url;
                
                UpdateNavigationButtons();
                UpdateStatus("✅", "DevTools.WebViewWorker.Loaded");
            });
        }

        private void UpdateNavigationButtons()
        {
            if (_backButton != null)
                _backButton.IsEnabled = _webView?.CanGoBack ?? false;

            if (_forwardButton != null)
                _forwardButton.IsEnabled = _webView?.CanGoForward ?? false;
        }

        private void UpdateStatus(string icon, string messageKey)
        {
            if (_statusIcon != null)
                _statusIcon.Text = icon;

            if (_statusText != null)
            {
                var message = Application.Current?.FindResource(messageKey)?.ToString() ?? messageKey;
                _statusText.Text = message;
            }
        }

        #region Event Handlers

        private void OnBackClick(object? sender, RoutedEventArgs e)
        {
            try
            {
                if (_webView?.CanGoBack == true)
                {
                    _webView.GoBack();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebViewWorkerPage] Error going back: {ex}");
                UpdateStatus("❌", $"Error: {ex.Message}");
            }
        }

        private void OnForwardClick(object? sender, RoutedEventArgs e)
        {
            try
            {
                if (_webView?.CanGoForward == true)
                {
                    _webView.GoForward();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebViewWorkerPage] Error going forward: {ex}");
                UpdateStatus("❌", $"Error: {ex.Message}");
            }
        }

        private void OnRefreshClick(object? sender, RoutedEventArgs e)
        {
            try
            {
                _webViewManager?.Reload();
                UpdateStatus("🔄", "DevTools.WebViewWorker.Refreshing");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebViewWorkerPage] Error refreshing: {ex}");
                UpdateStatus("❌", $"Error: {ex.Message}");
            }
        }

        private void OnLoadCurrentTabUrl(object? sender, RoutedEventArgs e)
        {
            try
            {
                if (_currentActiveTab == null || string.IsNullOrWhiteSpace(_currentActiveTab.Address))
                {
                    UpdateStatus("⚠️", "DevTools.WebViewWorker.NoUrlToLoad");
                    return;
                }

                var url = _currentActiveTab.Address;
                NavigateToUrl(url);
                UpdateStatus("🔄", "DevTools.WebViewWorker.LoadingFromTab");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebViewWorkerPage] Error loading current tab URL: {ex}");
                UpdateStatus("❌", $"Error: {ex.Message}");
            }
        }

        private void OnGoClick(object? sender, RoutedEventArgs e)
        {
            NavigateToInputUrl();
        }

        private void OnUrlKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                NavigateToInputUrl();
            }
        }

        private void NavigateToInputUrl()
        {
            try
            {
                if (_urlTextBox == null || string.IsNullOrWhiteSpace(_urlTextBox.Text))
                {
                    UpdateStatus("⚠️", "DevTools.WebViewWorker.EnterUrl");
                    return;
                }

                var url = _urlTextBox.Text.Trim();
                NavigateToUrl(url);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebViewWorkerPage] Error navigating: {ex}");
                UpdateStatus("❌", $"Error: {ex.Message}");
            }
        }

        private async void NavigateToUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return;

            // Add protocol if missing
            if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase) &&
                !url.StartsWith("file://", StringComparison.OrdinalIgnoreCase))
            {
                // Check if it looks like a URL or a search query
                if (url.Contains(".") && !url.Contains(" "))
                {
                    url = "https://" + url;
                }
                else
                {
                    // Treat as search query
                    url = "https://www.google.com/search?q=" + Uri.EscapeDataString(url);
                }
            }

            if (_webViewManager != null)
            {
                await _webViewManager.NavigateAsync(url);
                UpdateStatus("🔄", "DevTools.WebViewWorker.Navigating");
            }
        }

        #endregion

        #region Helpers

        private MainWindow? GetMainWindow()
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                return desktop.Windows.FirstOrDefault(w => w is MainWindow) as MainWindow;
            }
            return null;
        }
        
        private TabsManager? GetTabsManager(MainWindow mainWindow)
        {
            try
            {
                return mainWindow.TabsManager;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebViewWorkerPage] Error getting TabsManager: {ex}");
                return null;
            }
        }

        #endregion

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnDetachedFromVisualTree(e);

            // Cleanup
            _monitorTimer.Stop();
            UnsubscribeFromCurrentTab();

            if (_webViewManager != null)
            {
                _webViewManager.Navigated -= OnWebViewNavigated;
            }

            if (_webView != null)
            {
                _webView.PropertyChanged -= OnWebViewPropertyChanged;
                if (ReferenceEquals(DevToolsWebViewRegistry.CurrentWebView, _webView))
                    DevToolsWebViewRegistry.CurrentWebView = null;
            }
        }
    }
}

