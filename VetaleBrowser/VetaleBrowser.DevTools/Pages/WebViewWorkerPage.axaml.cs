using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using Avalonia.Controls.ApplicationLifetimes;
using VetaleBrowser.VetaleBrowser.Core.Scripts.Models;
using VetaleBrowser.VetaleBrowser.Core.Scripts.GlobalManagers;
using VetaleBrowser.VetaleBrowser.DevTools.Services;
using VetaleBrowser.VetaleBrowser.Database.Services;
using WebViewControl;

namespace VetaleBrowser.VetaleBrowser.DevTools.Pages
{
    public partial class WebViewWorkerPage : UserControl
    {
        private WebViewWorkerService? _webViewWorkerService;
        private TabWorker? _currentActiveTab;
        
        // WebView Control
        private WebView? _webView;
        private bool _webViewInitialized = false;
        
        // Public properties
        public string? CurrentUrl => _webView?.Address;
        public WebView? WebView => _webView;
        
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
            InitializeWebViewWorkerService();
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
                    [!IsVisibleProperty] = this[!IsVisibleProperty]
                };

                // Subscribe to WebView events
                _webView.PropertyChanged += OnWebViewPropertyChanged;
                
                // Add WebView to container
                if (_webViewContainer != null)
                {
                    _webViewContainer.Children.Add(_webView);
                    _webViewInitialized = true;
                    
                    // Hide placeholder when WebView is initialized
                    if (_placeholderText != null)
                    {
                        _placeholderText.IsVisible = false;
                    }
                }

                UpdateStatus("✅", "WebView готовий");
                Debug.WriteLine("[WebViewWorkerPage] WebView initialized successfully");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebViewWorkerPage] Error initializing WebView: {ex}");
                UpdateStatus("❌", $"Error: {ex.Message}");
                
                if (_placeholderText != null)
                {
                    _placeholderText.Text = $"❌ Помилка ініціалізації WebView:\n{ex.Message}";
                    _placeholderText.IsVisible = true;
                }
            }
        }

        private void InitializeWebViewWorkerService()
        {
            try
            {
                _webViewWorkerService = WebViewWorkerService.GetInstance(new DevToolsDataService());
                
                // Attach our local WebView to the service
                if (_webView != null)
                {
                    _webViewWorkerService.AttachLocalWebView(_webView);
                }
                
                Debug.WriteLine("[WebViewWorkerPage] WebViewWorkerService initialized");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebViewWorkerPage] Error initializing WebViewWorkerService: {ex}");
            }
        }

        private void OnWebViewPropertyChanged(object? sender, Avalonia.AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property.Name == "Address")
            {
                Dispatcher.UIThread.Post(() =>
                {
                    if (_webView != null && _urlTextBox != null)
                    {
                        _urlTextBox.Text = _webView.Address;
                    }
                    UpdateNavigationButtons();
                });
            }
            else if (e.Property.Name == "Title")
            {
                Dispatcher.UIThread.Post(() =>
                {
                    UpdateStatus("✅", _webView?.Title ?? "Loaded");
                });
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
                
                // If active tab changed, update UI only (NO auto-sync)
                if (activeTab != _currentActiveTab)
                {
                    UnsubscribeFromCurrentTab();
                    _currentActiveTab = activeTab;
                    SubscribeToCurrentTab();
                    UpdateCurrentTabInfo();
                    // Removed auto-sync - user must click button to load
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
            Dispatcher.UIThread.Post(() =>
            {
                UpdateCurrentTabInfo();
                // Removed auto-sync - user must click button to navigate
            });
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

        private void UpdateNavigationButtons()
        {
            if (_webView != null)
            {
                if (_backButton != null)
                    _backButton.IsEnabled = _webView.CanGoBack;

                if (_forwardButton != null)
                    _forwardButton.IsEnabled = _webView.CanGoForward;
            }
            else
            {
                if (_backButton != null)
                    _backButton.IsEnabled = false;

                if (_forwardButton != null)
                    _forwardButton.IsEnabled = false;
            }
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
            // Playwright doesn't support back navigation in this context
            UpdateStatus("⚠️", "Back navigation not available in Playwright mode");
        }

        private void OnForwardClick(object? sender, RoutedEventArgs e)
        {
            // Playwright doesn't support forward navigation in this context
            UpdateStatus("⚠️", "Forward navigation not available in Playwright mode");
        }

        private async void OnRefreshClick(object? sender, RoutedEventArgs e)
        {
            try
            {
                if (_playwrightService != null && !string.IsNullOrWhiteSpace(_currentPlaywrightUrl))
                {
                    UpdateStatus("🔄", "Refreshing...");
                    await _playwrightService.NavigateAsync(_currentPlaywrightUrl);
                    UpdateStatus("✅", "Refreshed");
                }
                else
                {
                    UpdateStatus("⚠️", "No URL to refresh");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebViewWorkerPage] Error refreshing: {ex}");
                UpdateStatus("❌", $"Error: {ex.Message}");
            }
        }

        private async void OnLoadCurrentTabUrl(object? sender, RoutedEventArgs e)
        {
            try
            {
                if (_currentActiveTab == null || string.IsNullOrWhiteSpace(_currentActiveTab.Address))
                {
                    UpdateStatus("⚠️", "DevTools.WebViewWorker.NoUrlToLoad");
                    return;
                }

                var url = _currentActiveTab.Address;
                await NavigatePlaywrightToUrl(url);
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
                _ = NavigatePlaywrightToUrl(url);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebViewWorkerPage] Error navigating: {ex}");
                UpdateStatus("❌", $"Error: {ex.Message}");
            }
        }

        private async Task NavigatePlaywrightToUrl(string url)
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

            try
            {
                // Initialize Playwright on first use (lazy initialization)
                if (_playwrightService == null)
                {
                    UpdateStatus("⏳", "Initializing Playwright Chromium...");
                    _playwrightService = PlaywrightDevToolsService.GetInstance(new Database.Services.DevToolsDataService());
                    await _playwrightService.InitializeAsync();
                    
                    if (_placeholderText != null)
                    {
                        _placeholderText.Text = "✅ Playwright Chromium готовий";
                    }
                }

                UpdateStatus("🔄", "Navigating...");
                await _playwrightService.NavigateAsync(url);
                _currentPlaywrightUrl = url;
                
                if (_urlTextBox != null)
                    _urlTextBox.Text = url;
                
                UpdateStatus("✅", "Loaded successfully");
                Debug.WriteLine($"[WebViewWorkerPage] Playwright navigated to: {url}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebViewWorkerPage] Playwright error: {ex}");
                UpdateStatus("❌", $"Error: {ex.Message}");
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

            _playwrightService?.Dispose();
            _playwrightService = null;
        }
    }
}

