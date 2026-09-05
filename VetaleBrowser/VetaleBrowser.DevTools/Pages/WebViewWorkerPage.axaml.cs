using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using Avalonia.Controls.ApplicationLifetimes;
using VetaleBrowser.VetaleBrowser.Core.Scripts.Models;
using VetaleBrowser.VetaleBrowser.Core.Scripts.GlobalManagers;
using VetaleBrowser.VetaleBrowser.DevTools.Services;
using VetaleBrowser.VetaleBrowser.Database.Services;
using VetaleBrowser.VetaleBrowser.Core.Scripts.Browser;

namespace VetaleBrowser.VetaleBrowser.DevTools.Pages
{
    public partial class WebViewWorkerPage : UserControl
    {
        private WebViewWorkerService? _webViewWorkerService;
        private TabWorker? _currentActiveTab;
        
        // WebView Control
        private IBrowserView? _webView;
        
        // Current file tracking
        private string? _currentFilePath;
        private string? _currentHtmlContent;
        
        // Public properties
        public string? CurrentUrl => _webView?.Address;
        public IBrowserView? WebView => _webView;
        
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
        private Button? _saveToEditorButton;

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
            _saveToEditorButton = this.FindControl<Button>("SaveToEditorButton");
        }

        private void InitializeWebView()
        {
            try
            {
                Debug.WriteLine("[WebViewWorkerPage] Initializing WebView...");
                
                _webView = new CefSharpAdapter();
                _webView.View[!IsVisibleProperty] = this[!IsVisibleProperty];

                // Subscribe to WebView events
                _webView.PropertyChanged += OnWebViewPropertyChanged;
                
                // Add WebView to container
                if (_webViewContainer != null)
                {
                    _webViewContainer.Children.Add(_webView.View);
                    
                    // Hide placeholder when WebView is initialized
                    if (_placeholderText != null)
                    {
                        _placeholderText.IsVisible = false;
                    }
                }

                UpdateStatus("✅", "WebView ready");
                Debug.WriteLine("[WebViewWorkerPage] WebView initialized successfully");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebViewWorkerPage] Error initializing WebView: {ex}");
                UpdateStatus("❌", $"Error: {ex.Message}");
                
                if (_placeholderText != null)
                {
                    _placeholderText.Text = $"❌ WebView initialization error:\n{ex.Message}";
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
                
                // If active tab changed, update UI only
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
                    _currentTabTitle.Text = "No active tab";
                
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
                _statusText.Text = messageKey;
            }
        }

        #region Event Handlers

        private void OnBackClick(object? sender, RoutedEventArgs e)
        {
            try
            {
                _webView?.GoBack();
                UpdateStatus("⬅️", "Navigated back");
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
                _webView?.GoForward();
                UpdateStatus("➡️", "Navigated forward");
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
                if (_webView != null && !string.IsNullOrWhiteSpace(_webView.Address))
                {
                    UpdateStatus("🔄", "Refreshing...");
                    _webView.Reload();
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

        private void OnLoadCurrentTabUrl(object? sender, RoutedEventArgs e)
        {
            try
            {
                if (_currentActiveTab == null || string.IsNullOrWhiteSpace(_currentActiveTab.Address))
                {
                    UpdateStatus("⚠️", "No URL to load");
                    return;
                }

                var url = _currentActiveTab.Address;
                NavigateToUrl(url);
                UpdateStatus("🔄", "Loading from tab");
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

        private async void OnLoadHtmlFromEditor(object? sender, RoutedEventArgs e)
        {
            try
            {
                UpdateStatus("⏳", "Loading HTML from editor...");
                
                // Get HTML from editor
                var htmlEditorService = new DevToolsDataService();
                var lastState = await htmlEditorService.GetActiveHtmlEditorStateAsync();
                
                if (lastState == null || string.IsNullOrWhiteSpace(lastState.EncryptedContent))
                {
                    UpdateStatus("⚠️", "No HTML found in editor");
                    return;
                }

                var htmlContent = lastState.EncryptedContent;
                _currentHtmlContent = htmlContent;
                
                // Method 1: Try creating temporary file and loading via file URL
                try
                {
                    var tempPath = Path.Combine(Path.GetTempPath(), $"vetale_preview_{Guid.NewGuid()}.html");
                    await File.WriteAllTextAsync(tempPath, htmlContent);
                    _currentFilePath = tempPath;
                    
                    if (_webView != null)
                    {
                        // Convert to proper file URL
                        var normalizedPath = tempPath.Replace("\\", "/");
                        var fileUri = new Uri($"file:///{normalizedPath}").AbsoluteUri;
                        
                        Debug.WriteLine($"[WebViewWorkerPage] Loading from editor via file URL: {fileUri}");
                        _webView.Address = fileUri;
                        
                        if (_urlTextBox != null)
                            _urlTextBox.Text = "HTML from Editor";
                        
                        UpdateStatus("✅", "HTML loaded from editor");
                        Debug.WriteLine($"[WebViewWorkerPage] Loaded HTML from editor: {tempPath}");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[WebViewWorkerPage] File URL method failed, trying direct HTML: {ex.Message}");
                    
                    // Method 2: Fallback - Load HTML directly
                    if (_webView != null)
                    {
                        try
                        {
                            // Try LoadHtml method via reflection
                            var loadHtmlMethod = _webView.InnerView.GetType().GetMethod("LoadHtml");
                            if (loadHtmlMethod != null)
                            {
                                loadHtmlMethod.Invoke(_webView.InnerView, new object[] { htmlContent });
                                Debug.WriteLine("[WebViewWorkerPage] Loaded via LoadHtml method");
                            }
                            else
                            {
                                // Use data URI as last resort
                                var base64Content = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(htmlContent));
                                var dataUri = $"data:text/html;base64,{base64Content}";
                                _webView.Address = dataUri;
                                Debug.WriteLine("[WebViewWorkerPage] Loaded via data URI");
                            }
                            
                            if (_urlTextBox != null)
                                _urlTextBox.Text = "HTML from Editor (direct)";
                            
                            UpdateStatus("✅", "HTML loaded from editor (direct)");
                        }
                        catch (Exception innerEx)
                        {
                            Debug.WriteLine($"[WebViewWorkerPage] Direct HTML load failed: {innerEx.Message}");
                            throw;
                        }
                    }
                }
                
                // Enable Save to Editor button
                if (_saveToEditorButton != null)
                    _saveToEditorButton.IsEnabled = true;
                
                // Wait for WebView to be ready before capturing
                Debug.WriteLine("[WebViewWorkerPage] Waiting for WebView to be ready...");
                await Task.Delay(2000); // Increased delay for local files
                
                // Check if WebView is still loaded (user didn't navigate away)
                if (_webView != null && !string.IsNullOrWhiteSpace(_webView.Address))
                {
                    Debug.WriteLine("[WebViewWorkerPage] Starting auto-capture...");
                    await CaptureDataFromWebView();
                }
                else
                {
                    Debug.WriteLine("[WebViewWorkerPage] WebView not ready, skipping auto-capture");
                    UpdateStatus("⚠️", "HTML loaded. Click 'Capture Data' manually.");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebViewWorkerPage] Error loading HTML from editor: {ex}");
                UpdateStatus("❌", $"Error: {ex.Message}");
            }
        }

        private async void OnOpenLocalHtmlFile(object? sender, RoutedEventArgs e)
        {
            try
            {
                UpdateStatus("📂", "Opening file dialog...");
                
                // Get main window
                var mainWindow = GetMainWindow();
                if (mainWindow == null)
                {
                    UpdateStatus("❌", "Cannot find main window");
                    return;
                }

                // Use modern StorageProvider API
                var storageProvider = mainWindow.StorageProvider;
                if (storageProvider == null)
                {
                    UpdateStatus("❌", "Storage provider not available");
                    return;
                }

                var filePickerOptions = new Avalonia.Platform.Storage.FilePickerOpenOptions
                {
                    Title = "Open HTML File",
                    AllowMultiple = false,
                    FileTypeFilter = new[]
                    {
                        new Avalonia.Platform.Storage.FilePickerFileType("HTML Files")
                        {
                            Patterns = new[] { "*.html", "*.htm" }
                        },
                        new Avalonia.Platform.Storage.FilePickerFileType("All Files")
                        {
                            Patterns = new[] { "*.*" }
                        }
                    }
                };

                // Show dialog
                var result = await storageProvider.OpenFilePickerAsync(filePickerOptions);
                
                if (result != null && result.Count > 0)
                {
                    var file = result[0];
                    var filePath = file.Path.LocalPath;
                    await LoadHtmlFile(filePath);
                }
                else
                {
                    UpdateStatus("⚪", "File selection cancelled");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebViewWorkerPage] Error opening file: {ex}");
                UpdateStatus("❌", $"Error: {ex.Message}");
            }
        }

        private async Task LoadHtmlFile(string filePath)
        {
            try
            {
                UpdateStatus("⏳", $"Loading {Path.GetFileName(filePath)}...");
                
                if (!File.Exists(filePath))
                {
                    UpdateStatus("❌", "File not found");
                    return;
                }

                // Read file content
                _currentHtmlContent = await File.ReadAllTextAsync(filePath);
                _currentFilePath = filePath;

                // Method 1: Try to navigate using file:// URL with proper format
                if (_webView != null)
                {
                    try
                    {
                        // Convert Windows path to proper file URL
                        // E:\path\file.html -> file:///E:/path/file.html
                        var normalizedPath = filePath.Replace("\\", "/");
                        var fileUri = new Uri($"file:///{normalizedPath}").AbsoluteUri;
                        
                        Debug.WriteLine($"[WebViewWorkerPage] Navigating to: {fileUri}");
                        _webView.Address = fileUri;
                        
                        if (_urlTextBox != null)
                            _urlTextBox.Text = filePath;
                        
                        UpdateStatus("✅", $"Loaded: {Path.GetFileName(filePath)}");
                        Debug.WriteLine($"[WebViewWorkerPage] Loaded file via URL: {filePath}");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[WebViewWorkerPage] File URL navigation failed, trying direct HTML load: {ex.Message}");
                        
                        // Method 2: Fallback - Load HTML content directly
                        try
                        {
                            // WebView might have LoadHtml method
                            var loadHtmlMethod = _webView.InnerView.GetType().GetMethod("LoadHtml");
                            if (loadHtmlMethod != null)
                            {
                                loadHtmlMethod.Invoke(_webView, new object[] { _currentHtmlContent });
                                Debug.WriteLine("[WebViewWorkerPage] Loaded HTML via LoadHtml method");
                            }
                            else
                            {
                                // Method 3: Navigate to data URI
                                var base64Content = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(_currentHtmlContent));
                                var dataUri = $"data:text/html;base64,{base64Content}";
                                _webView.Address = dataUri;
                                Debug.WriteLine("[WebViewWorkerPage] Loaded HTML via data URI");
                            }
                            
                            if (_urlTextBox != null)
                                _urlTextBox.Text = $"Local: {Path.GetFileName(filePath)}";
                            
                            UpdateStatus("✅", $"Loaded: {Path.GetFileName(filePath)} (direct)");
                        }
                        catch (Exception innerEx)
                        {
                            Debug.WriteLine($"[WebViewWorkerPage] Direct HTML load also failed: {innerEx.Message}");
                            throw;
                        }
                    }
                    
                    // Enable Save to Editor button
                    if (_saveToEditorButton != null)
                        _saveToEditorButton.IsEnabled = true;
                    
                    // Wait for WebView to be ready before capturing
                    Debug.WriteLine("[WebViewWorkerPage] Waiting for WebView to be ready...");
                    await Task.Delay(2000); // Increased delay for local files
                    
                    // Check if WebView is still loaded (user didn't navigate away)
                    if (_webView != null && !string.IsNullOrWhiteSpace(_webView.Address))
                    {
                        Debug.WriteLine("[WebViewWorkerPage] Starting auto-capture...");
                        await CaptureDataFromWebView();
                    }
                    else
                    {
                        Debug.WriteLine("[WebViewWorkerPage] WebView not ready, skipping auto-capture");
                        UpdateStatus("⚠️", "Page loaded. Click 'Capture Data' manually.");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebViewWorkerPage] Error loading file: {ex}");
                UpdateStatus("❌", $"Error: {ex.Message}");
            }
        }

        private async void OnSaveToEditor(object? sender, RoutedEventArgs e)
        {
            try
            {
                UpdateStatus("⏳", "Saving to editor...");
                
                string? htmlContent = null;
                
                // Try to get current HTML from WebView using service
                if (_webView != null && _webViewWorkerService != null)
                {
                    try
                    {
                        // Execute JavaScript to get current HTML
                        var script = "document.documentElement.outerHTML";
                        htmlContent = await _webViewWorkerService.ExecuteJavaScriptAsync(script);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[WebViewWorkerPage] Cannot get HTML from WebView, using cached: {ex.Message}");
                        htmlContent = _currentHtmlContent;
                    }
                }
                
                // Fallback to cached content
                if (string.IsNullOrWhiteSpace(htmlContent))
                {
                    htmlContent = _currentHtmlContent;
                }

                if (string.IsNullOrWhiteSpace(htmlContent))
                {
                    UpdateStatus("⚠️", "No HTML content to save");
                    return;
                }

                // Save to HTML Editor
                var htmlEditorService = new DevToolsDataService();
                
                // Create or update editor state
                var editorState = new Database.Models.HtmlEditorState
                {
                    SessionKey = $"html-editor-{Guid.NewGuid()}",
                    EncryptedContent = htmlContent,
                    FilePath = _currentFilePath,
                    IsActive = true,
                    UpdatedAt = DateTime.UtcNow
                };

                await htmlEditorService.SaveHtmlEditorStateAsync(editorState);
                
                UpdateStatus("✅", "Saved to HTML Editor");
                Debug.WriteLine($"[WebViewWorkerPage] Saved HTML to editor: {htmlContent.Length} characters");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebViewWorkerPage] Error saving to editor: {ex}");
                UpdateStatus("❌", $"Error: {ex.Message}");
            }
        }

        private async void OnCaptureData(object? sender, RoutedEventArgs e)
        {
            await CaptureDataFromWebView();
        }

        private async Task CaptureDataFromWebView()
        {
            try
            {
                if (_webView == null || _webViewWorkerService == null)
                {
                    Debug.WriteLine("[WebViewWorkerPage] WebView or service not initialized");
                    UpdateStatus("⚠️", "WebView not ready");
                    return;
                }

                // Check if WebView has loaded content
                if (string.IsNullOrWhiteSpace(_webView.Address))
                {
                    Debug.WriteLine("[WebViewWorkerPage] No URL loaded in WebView");
                    UpdateStatus("⚠️", "No page loaded");
                    return;
                }

                Debug.WriteLine("[WebViewWorkerPage] Starting data capture...");
                UpdateStatus("📊", "Capturing data...");

                int successCount = 0;
                int totalCount = 4;
                var errors = new List<string>();

                // Capture DOM with error handling
                var domElements = new List<VetaleBrowser.Database.Models.DomElement>();
                try
                {
                    Debug.WriteLine("[WebViewWorkerPage] Capturing DOM...");
                    domElements = await _webViewWorkerService.CaptureDomStructureAsync();
                    successCount++;
                    Debug.WriteLine($"[WebViewWorkerPage] ✅ DOM: {domElements.Count} elements");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[WebViewWorkerPage] ❌ DOM capture failed: {ex.Message}");
                    errors.Add($"DOM: {ex.Message}");
                }

                // Capture Performance with error handling
                VetaleBrowser.Database.Models.PerformanceSnapshot? perfSnapshot = null;
                try
                {
                    Debug.WriteLine("[WebViewWorkerPage] Capturing Performance...");
                    perfSnapshot = await _webViewWorkerService.CapturePerformanceSnapshotAsync();
                    successCount++;
                    Debug.WriteLine($"[WebViewWorkerPage] ✅ Performance: {perfSnapshot?.LoadTime ?? 0}ms");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[WebViewWorkerPage] ❌ Performance capture failed: {ex.Message}");
                    errors.Add($"Performance: {ex.Message}");
                }

                // Capture Resources with error handling
                var resources = new List<VetaleBrowser.Database.Models.PageResource>();
                try
                {
                    Debug.WriteLine("[WebViewWorkerPage] Capturing Resources...");
                    resources = await _webViewWorkerService.CapturePageResourcesAsync();
                    successCount++;
                    Debug.WriteLine($"[WebViewWorkerPage] ✅ Resources: {resources.Count} items");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[WebViewWorkerPage] ❌ Resources capture failed: {ex.Message}");
                    errors.Add($"Resources: {ex.Message}");
                }

                // Capture Storage with error handling
                var storage = new List<VetaleBrowser.Database.Models.StorageItem>();
                try
                {
                    Debug.WriteLine("[WebViewWorkerPage] Capturing Storage...");
                    storage = await _webViewWorkerService.CaptureStorageAsync();
                    successCount++;
                    Debug.WriteLine($"[WebViewWorkerPage] ✅ Storage: {storage.Count} items");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[WebViewWorkerPage] ❌ Storage capture failed: {ex.Message}");
                    errors.Add($"Storage: {ex.Message}");
                }

                // Show results
                Debug.WriteLine($"[WebViewWorkerPage] Capture complete: {successCount}/{totalCount} successful");
                
                if (successCount == totalCount)
                {
                    UpdateStatus("✅", $"Captured: {domElements.Count} DOM, {resources.Count} resources, {storage.Count} storage");
                }
                else if (successCount > 0)
                {
                    UpdateStatus("⚠️", $"Partial: {successCount}/{totalCount} captured ({string.Join(", ", errors.Take(2))})");
                }
                else
                {
                    UpdateStatus("❌", "Capture failed. Check Debug Output.");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebViewWorkerPage] Critical error in data capture: {ex}");
                UpdateStatus("❌", $"Capture error: {ex.Message}");
            }
        }

        private void NavigateToInputUrl()
        {
            try
            {
                if (_urlTextBox == null || string.IsNullOrWhiteSpace(_urlTextBox.Text))
                {
                    UpdateStatus("⚠️", "Enter URL");
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

        private void NavigateToUrl(string url)
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
                if (_webView != null)
                {
                    UpdateStatus("🔄", "Navigating...");
                    _webView.Address = url;
                    
                    if (_urlTextBox != null)
                        _urlTextBox.Text = url;
                    
                    Debug.WriteLine($"[WebViewWorkerPage] Navigating to: {url}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WebViewWorkerPage] Navigation error: {ex}");
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
            
            // Detach WebView from service
            _webViewWorkerService?.DetachLocalWebView();
        }
    }
}


