using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;
using VetaleBrowser.VetaleBrowser.UI.Scripts;
using VetaleBrowser.VetaleBrowser.Core.Scripts.GlobalManagers;
using VetaleBrowser.VetaleBrowser.UI.Еlements;
using VetaleBrowser.VetaleBrowser.UI.Services;
using WebViewControl;
using Avalonia.Threading;
using Avalonia;
using VetaleBrowser.VetaleBrowser.Core.Scripts.Models;
using System.Linq;
using VetaleBrowser.VetaleBrowser.UI.Pages;
using VetaleBrowser.VetaleBrowser.UI.Windows; // added for SettingsWindow and ToolsWindow
using VetaleBrowser.VetaleBrowser.Database;
using VetaleBrowser.VetaleBrowser.Database.Services;
using Avalonia.Media;

namespace VetaleBrowser;

public partial class MainWindow : Window
{
    private readonly WindowManager? _windowManager;
    private readonly IFaviconService _faviconService = new FaviconService();
    private ISettingsService? _settingsService;
    private IAppearanceSettingsService? _appearanceSettingsService;

    private readonly TabsManager _tabs = new();

    // Pages
    private NormalModePage? _normalModePage;
    private FullscreenModePage? _fullscreenModePage;
    private ContentControl? _pageContainer;

    // Keep track of which worker's WebView we're listening to
    private TabWorker? _subscribedWorker;

    // Fullscreen state
    private bool _isFullscreen;
    private WindowState _preFullscreenWindowState;

    // Polling support for robust favicon and title updates
    private readonly DispatcherTimer _faviconPollTimer = new() { Interval = TimeSpan.FromMilliseconds(500) };
    private string? _lastFaviconUrl;
    private string? _lastPageTitle;

    // Cached appearance values
    private double _tabWidth = 200.0;

    public MainWindow()
    {
        try
        {
            InitializeComponent();
            System.Diagnostics.Debug.WriteLine("[MainWindow] InitializeComponent completed");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MainWindow] CRITICAL: InitializeComponent failed: {ex}");
            throw; // Can't continue without UI
        }

        try
        {
            _windowManager = new WindowManager(this);
            System.Diagnostics.Debug.WriteLine("[MainWindow] WindowManager initialized");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MainWindow] WindowManager initialization failed: {ex.Message}");
            // Continue - not critical
        }
        
        // Initialize settings service
        try
        {
            InitializeSettingsService();
            System.Diagnostics.Debug.WriteLine("[MainWindow] Settings service initialized");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MainWindow] Settings service initialization failed: {ex.Message}");
            // Continue - not critical for startup
        }

        try
        {
            InitializeAppearanceSettingsService();
            System.Diagnostics.Debug.WriteLine("[MainWindow] Appearance settings service initialized");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MainWindow] Appearance settings initialization failed: {ex.Message}");
            // Continue - not critical for startup
        }

        // Initialize after the window is loaded
        this.Loaded += OnWindowLoaded;
        this.Closed += OnWindowClosed;
        this.KeyDown += OnWindowKeyDown;

        _tabs.TabActivated += OnTabActivated;
        _tabs.TabClosed += OnTabClosed;
        // Removed TabCreated subscription to avoid duplicate Tab controls
        // _tabs.TabCreated += OnTabCreated;

        // Timer to check for URL/title/title changes periodically (covers redirects and edge cases)
        _faviconPollTimer.Tick += async (_, _) =>
        {
            try
            {
                var active = _tabs.Active;
                if (active != null)
                {
                    var url = active.Manager.GetCurrentUrl();
                    if (!string.IsNullOrWhiteSpace(url) && !string.Equals(url, _lastFaviconUrl, StringComparison.Ordinal))
                    {
                        _lastFaviconUrl = url;
                        await UpdateFaviconAsync(url);
                        await UpdateTabTitleAsync(null, url);
                    }

                    var currentTitle = TryGetWebViewTitle(active.WebView);
                    if (!string.IsNullOrWhiteSpace(currentTitle) && !string.Equals(currentTitle, _lastPageTitle, StringComparison.Ordinal))
                    {
                        _lastPageTitle = currentTitle;
                        await UpdateTabTitleAsync(currentTitle, url);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MainWindow] Poll tick error: {ex.Message}");
            }
        };

        System.Diagnostics.Debug.WriteLine("[MainWindow] Constructor completed successfully");
    }

    private void InitializeComponent()
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("[MainWindow] InitializeComponent: Loading XAML...");
            AvaloniaXamlLoader.Load(this);
            System.Diagnostics.Debug.WriteLine("[MainWindow] InitializeComponent: XAML loaded");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MainWindow] CRITICAL: AvaloniaXamlLoader.Load failed: {ex}");
            throw;
        }
        
        try
        {
            System.Diagnostics.Debug.WriteLine("[MainWindow] InitializeComponent: Finding PageContainer...");
            _pageContainer = this.FindControl<ContentControl>("PageContainer");
            if (_pageContainer == null)
            {
                System.Diagnostics.Debug.WriteLine("[MainWindow] ERROR: PageContainer not found!");
                throw new InvalidOperationException("PageContainer control not found in XAML");
            }
            System.Diagnostics.Debug.WriteLine("[MainWindow] InitializeComponent: PageContainer found");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MainWindow] ERROR finding PageContainer: {ex.Message}");
            throw;
        }
        
        try
        {
            // Create pages
            System.Diagnostics.Debug.WriteLine("[MainWindow] InitializeComponent: Creating NormalModePage...");
            _normalModePage = new NormalModePage();
            System.Diagnostics.Debug.WriteLine("[MainWindow] InitializeComponent: NormalModePage created");
            
            System.Diagnostics.Debug.WriteLine("[MainWindow] InitializeComponent: Creating FullscreenModePage...");
            _fullscreenModePage = new FullscreenModePage();
            System.Diagnostics.Debug.WriteLine("[MainWindow] InitializeComponent: FullscreenModePage created");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MainWindow] ERROR creating pages: {ex}");
            throw;
        }
        
        try
        {
            // Start with normal mode
            System.Diagnostics.Debug.WriteLine("[MainWindow] InitializeComponent: Setting PageContainer content...");
            _pageContainer.Content = _normalModePage;
            System.Diagnostics.Debug.WriteLine("[MainWindow] InitializeComponent: PageContainer content set");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MainWindow] ERROR setting PageContainer content: {ex}");
            throw;
        }
        
        try
        {
            // Setup add tab button handler
            if (_normalModePage.AddTabBtn != null)
            {
                _normalModePage.AddTabBtn.Click += OnAddTabBtnClickAsync;
                System.Diagnostics.Debug.WriteLine("[MainWindow] InitializeComponent: AddTabBtn handler attached");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("[MainWindow] WARNING: AddTabBtn is null");
            }
            
            // Setup window control buttons
            if (_normalModePage.MinBtn != null)
            {
                _normalModePage.MinBtn.Click += MinimizeWindow;
                System.Diagnostics.Debug.WriteLine("[MainWindow] InitializeComponent: MinBtn handler attached");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("[MainWindow] WARNING: MinBtn is null");
            }
            
            if (_normalModePage.MaxBtn != null)
            {
                _normalModePage.MaxBtn.Click += MaximizeWindow;
                System.Diagnostics.Debug.WriteLine("[MainWindow] InitializeComponent: MaxBtn handler attached");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("[MainWindow] WARNING: MaxBtn is null");
            }
            
            if (_normalModePage.ClsBtn != null)
            {
                _normalModePage.ClsBtn.Click += CloseWindow;
                System.Diagnostics.Debug.WriteLine("[MainWindow] InitializeComponent: ClsBtn handler attached");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("[MainWindow] WARNING: ClsBtn is null");
            }
            
            // Setup drag for TabBarRow
            if (_normalModePage.TabBar != null)
            {
                _normalModePage.TabBar.PointerPressed += TopBar_PointerPressed;
                _normalModePage.TabBar.DoubleTapped += TopBar_DoubleTapped;
                System.Diagnostics.Debug.WriteLine("[MainWindow] InitializeComponent: TabBar handlers attached");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("[MainWindow] WARNING: TabBar is null");
            }
            
            System.Diagnostics.Debug.WriteLine("[MainWindow] InitializeComponent: Completed successfully");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MainWindow] ERROR setting up event handlers: {ex.Message}");
            // Non-critical, continue
        }
    }

    private void InitializeSettingsService()
    {
        try
        {
            var config = DatabaseConfiguration.CreateDefault();
            _settingsService = new SettingsService(config.DatabasePath, config.EncryptionKey);
            System.Diagnostics.Debug.WriteLine("MainWindow: Settings service initialized");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"MainWindow: Error initializing settings service: {ex}");
        }
    }

    private void InitializeAppearanceSettingsService()
    {
        try
        {
            var config = DatabaseConfiguration.CreateDefault();
            var appearanceDbPath = System.IO.Path.Combine(
                System.IO.Path.GetDirectoryName(config.DatabasePath) ?? string.Empty,
                "appearance_settings.db");
            _appearanceSettingsService = new AppearanceSettingsService(appearanceDbPath, config.EncryptionKey);
            System.Diagnostics.Debug.WriteLine("MainWindow: Appearance settings service initialized");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"MainWindow: Error initializing appearance settings service: {ex}");
        }
    }

    private async void OnWindowLoaded(object? sender, RoutedEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine("MainWindow: OnWindowLoaded called");

        try
        {
            // Ensure there's at least one tab
            if (_tabs.Active == null)
            {
                try
                {
                    var home = await GetSearchHomePageAsync();
                    CreateNewTab(home);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"MainWindow: Failed to create initial tab: {ex.Message}");
                    // Fallback to Google if settings fail
                    CreateNewTab("https://www.google.com");
                }
            }

            // Initialize NavigationBar with the active tab's manager
            try
            {
                var navigationBar = _normalModePage?.NavBar;
                if (navigationBar != null && _tabs.Active != null)
                {
                    navigationBar.Initialize(_tabs.Active.Manager);
                    
                    // Set settings service for search engine configuration
                    if (_settingsService != null)
                    {
                        navigationBar.SetSettingsService(_settingsService);
                    }

                    // Subscribe only to events that need external handling
                    navigationBar.BookmarkRequested += OnBookmarkRequested;
                    navigationBar.ToolsRequested += OnToolsRequested;
                    navigationBar.SettingsRequested += OnSettingsRequested;

                    System.Diagnostics.Debug.WriteLine("MainWindow: NavigationBar initialized");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"MainWindow: NavigationBar initialization failed: {ex.Message}");
            }

            // Apply appearance settings at startup
            try
            {
                await ApplyAppearanceSettingsFromStoreAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"MainWindow: Failed to apply appearance settings: {ex.Message}");
            }

            // Start favicon/title polling
            try
            {
                _faviconPollTimer.Start();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"MainWindow: Failed to start poll timer: {ex.Message}");
            }

            System.Diagnostics.Debug.WriteLine("MainWindow: OnWindowLoaded completed successfully");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"MainWindow: Error in OnWindowLoaded: {ex}");
            
            // Last resort - ensure at least basic functionality
            try
            {
                if (_tabs.Active == null)
                {
                    CreateNewTab("https://www.google.com");
                }
            }
            catch (Exception innerEx)
            {
                System.Diagnostics.Debug.WriteLine($"MainWindow: CRITICAL - Could not create fallback tab: {innerEx.Message}");
                ShowErrorInWebViewContainer($"Помилка ініціалізації: {ex.Message}\n\nБудь ласка, перезапустіть додаток.");
            }
        }
    }

    // Public so SettingsWindow can force apply after save
    public async Task ApplyAppearanceSettingsFromStoreAsync()
    {
        if (_appearanceSettingsService == null || _normalModePage == null)
            return;

        try
        {
            // Read settings
            var navBarColor = await _appearanceSettingsService.GetNavigationBarColorAsync();
            var navBarHeight = await _appearanceSettingsService.GetNavigationBarHeightAsync();
            var topBarColor = await _appearanceSettingsService.GetTopBarBackgroundColorAsync();
            var btnSize = await _appearanceSettingsService.GetButtonSizeAsync();
            var tabWidth = await _appearanceSettingsService.GetTabWidthAsync();
            var windowW = await _appearanceSettingsService.GetMainWindowWidthAsync();
            var windowH = await _appearanceSettingsService.GetMainWindowHeightAsync();

            // Apply navigation bar row
            if (_normalModePage.NavBarRow != null)
            {
                _normalModePage.NavBarRow.Height = navBarHeight;
                var nbBrush = ParseBrush(navBarColor);
                if (nbBrush != null)
                    _normalModePage.NavBarRow.Background = nbBrush;
            }

            // Apply main top bar background (tabs row) only if a color is provided
            if (_normalModePage.TabBar != null)
            {
                var tbBrush = ParseBrush(topBarColor);
                if (tbBrush != null)
                    _normalModePage.TabBar.Background = tbBrush; // else keep GrayGradient from XAML
            }

            // Cache and apply tab width to existing tabs
            _tabWidth = tabWidth;
            UpdateAllTabWidths(tabWidth);

            // Optionally adjust button sizes in NavBar by setting its Height
            if (_normalModePage.NavBar != null && btnSize > 0)
            {
                _normalModePage.NavBar.Height = navBarHeight; // keep consistent height
            }

            // Apply window size immediately
            if (windowW > 0 && windowH > 0)
            {
                Width = windowW;
                Height = windowH;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"MainWindow: Failed to apply appearance settings: {ex}");
        }
    }

    private IBrush? ParseBrush(string? color)
    {
        if (string.IsNullOrWhiteSpace(color)) return null;
        try
        {
            if (Color.TryParse(color, out var c))
            {
                return new SolidColorBrush(c);
            }
        }
        catch { }
        return null;
    }

    private void UpdateAllTabWidths(double width)
    {
        try
        {
            var tabsHost = _normalModePage?.TabsHostPanel;
            if (tabsHost == null) return;
            foreach (var child in tabsHost.Children)
            {
                if (child is Tab tab)
                {
                    tab.Width = width;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"MainWindow: UpdateAllTabWidths error: {ex.Message}");
        }
    }

    private void ShowErrorInWebViewContainer(string message)
    {
        var container = _normalModePage?.WebViewGrid;
        if (container != null)
        {
            container.Children.Clear();
            container.Children.Add(new TextBlock
            {
                Text = message,
                TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                Foreground = Avalonia.Media.Brushes.Red,
                FontSize = 14,
                Margin = new Thickness(20)
            });
        }
    }

    private void CreateNewTab(string? initialUrl = null)
    {
        var worker = _tabs.Create(initialUrl);
        AddTabControlForWorker(worker);
        ActivateWorker(worker);
    }

    // Open a new tab using the selected search engine homepage
    private async void OnAddTabBtnClickAsync(object? sender, RoutedEventArgs e)
    {
        try
        {
            var home = await GetSearchHomePageAsync();
            CreateNewTab(home);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MainWindow] OnAddTabBtnClickAsync error: {ex.Message}");
            CreateNewTab("https://www.google.com");
        }
    }

    private void AddTabControlForWorker(TabWorker worker)
    {
        var tabsHost = _normalModePage?.TabsHostPanel;
        if (tabsHost == null) return;

        var tab = new Tab
        {
            Title = "New Tab",
            IsActive = worker.IsActive,
            IsCloseButtonVisible = true,
            IsMuted = worker.IsMuted,
            Width = _tabWidth
        };

        tab.Clicked += (_, __) => ActivateWorker(worker);
        tab.CloseRequested += (_, __) =>
        {
            // Remove the specific Tab control that was clicked, then close its worker
            tabsHost.Children.Remove(tab);
            _tabs.Close(worker);
        };
        tab.MuteToggled += async (_, __) =>
        {
            try
            {
                // Desired mute state (toggle)
                var desired = !worker.IsMuted;

                // JS to apply mute/unmute directly in the page (media elements fallback)
                var js = desired
                    ? "(function(){try{document.querySelectorAll('video,audio').forEach(m=>{m.muted=true; m.volume=0;});return true;}catch(e){return false;}})();"
                    : "(function(){try{document.querySelectorAll('video,audio').forEach(m=>{m.muted=false; if(m.volume===0) m.volume=1.0;});return true;}catch(e){return false;}})();";

                object? res = null;
                try
                {
                    // Ensure script evaluation runs on UI thread - WebView may require being called from UI dispatcher
                    res = await Dispatcher.UIThread.InvokeAsync(async () => await worker.WebView.EvaluateScript<object>(js));
                }
                catch (Exception jsEx)
                {
                    System.Diagnostics.Debug.WriteLine($"[MainWindow] Direct WebView mute JS failed: {jsEx.Message}");
                }

                static bool EvalResultAsBool(object? r)
                {
                    try
                    {
                        if (r is bool bb) return bb;
                        if (r is string s)
                        {
                            var t = s.Trim();
                            if (bool.TryParse(t, out var pb)) return pb;
                            if (t == "1") return true;
                            if (t == "0") return false;
                            if (string.Equals(t, "true", StringComparison.OrdinalIgnoreCase)) return true;
                            if (string.Equals(t, "false", StringComparison.OrdinalIgnoreCase)) return false;
                        }
                        if (r is int i) return i != 0;
                        if (r is long l) return l != 0;
                    }
                    catch { }
                    return false;
                }

                var applied = EvalResultAsBool(res);

                if (!applied)
                {
                    // JS didn't confirm application; fall back to TabWorker's ToggleMute which applies system-level or JS fallback
                    System.Diagnostics.Debug.WriteLine("[MainWindow] JS mute did not report success, falling back to TabWorker.ToggleMute()");
                    worker.ToggleMute();
                }
                else
                {
                    // JS confirmed; update worker state to keep it consistent
                    worker.IsMuted = desired;
                }

                // Update this tab header immediately from the worker state
                tab.IsMuted = worker.IsMuted;

                // Sync all tab headers to reflect current worker mute states
                for (int widx = 0; widx < _tabs.Workers.Count; widx++)
                {
                    var childIdx2 = widx + 1;
                    if (childIdx2 >= 0 && childIdx2 < tabsHost.Children.Count && tabsHost.Children[childIdx2] is Tab t2)
                    {
                        t2.IsMuted = _tabs.Workers[widx].IsMuted;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MainWindow] Direct mute toggle failed: {ex.Message}");
            }
        };

        tabsHost.Children.Add(tab);
    }

    private int GetChildIndexForWorker(TabWorker worker)
    {
        // Children[0] is the AddTab button; tabs start from index 1
        var workerIndex = _tabs.Workers.ToList().IndexOf(worker);
        return workerIndex < 0 ? -1 : workerIndex + 1;
    }

    private void ActivateWorker(TabWorker worker)
    {
        _tabs.Activate(worker);

        // Update UI: swap WebView into container, update tab headers, rebind NavigationBar
        var targetContainer = _isFullscreen 
            ? _fullscreenModePage?.FullscreenGrid 
            : _normalModePage?.WebViewGrid;
            
        if (targetContainer != null)
        {
            MoveActiveWebViewTo(targetContainer);
        }

        // Update tabs active state and titles
        var tabsHost = _normalModePage?.TabsHostPanel;
        if (tabsHost != null)
        {
            for (int widx = 0; widx < _tabs.Workers.Count; widx++)
            {
                var childIdx = widx + 1; // account for add button
                if (childIdx >= 0 && childIdx < tabsHost.Children.Count && tabsHost.Children[childIdx] is Tab t)
                {
                    var w = _tabs.Workers[widx];
                    t.IsActive = w == worker;
                    var interim = ComputeTitle(w.Title, w.Address);
                    t.Title = interim;
                    // Sync mute state from worker to tab UI
                    t.IsMuted = w.IsMuted;
                }
            }
        }

        // Bind navigation bar to active manager
        var navigationBar = _normalModePage?.NavBar;
        if (navigationBar != null)
        {
            navigationBar.Initialize(worker.Manager);
            
            // Set settings service for search engine configuration
            if (_settingsService != null)
            {
                navigationBar.SetSettingsService(_settingsService);
            }
            
            navigationBar.Url = worker.Address ?? string.Empty;
            navigationBar.CanGoBack = worker.WebView.CanGoBack;
            navigationBar.CanGoForward = worker.WebView.CanGoForward;
        }

        WireActiveWebViewPropertyChanged(worker);

        _lastFaviconUrl = worker.Address;
        _lastPageTitle = worker.Title;
    }

    private void MoveActiveWebViewTo(Grid target)
    {
        try
        {
            var active = _tabs.Active;
            if (active == null) return;
            var webView = active.WebView;

            // Remove from previous parent if necessary
            if (webView.Parent is Panel prev)
            {
                if (!ReferenceEquals(prev, target))
                {
                    prev.Children.Remove(webView);
                }
            }

            // Clear target and add webview
            target.Children.Clear();
            target.Children.Add(webView);

            System.Diagnostics.Debug.WriteLine($"[MainWindow] WebView moved to {(ReferenceEquals(target, _fullscreenModePage?.FullscreenGrid) ? "fullscreen" : "normal")} container");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MainWindow] MoveActiveWebViewTo error: {ex.Message}");
        }
    }

    private void WireActiveWebViewPropertyChanged(TabWorker worker)
    {
        if (_subscribedWorker != null)
        {
            try 
            { 
                _subscribedWorker.WebView.PropertyChanged -= WebView_OnPropertyChanged;
                _subscribedWorker.FullscreenChanged -= OnWorkerFullscreenChanged;
                _subscribedWorker.WebView.KeyDown -= OnWebViewKeyDown;
            } 
            catch { }
        }
        _subscribedWorker = worker;
        _subscribedWorker.WebView.PropertyChanged += WebView_OnPropertyChanged;
        _subscribedWorker.FullscreenChanged += OnWorkerFullscreenChanged;
        _subscribedWorker.WebView.KeyDown += OnWebViewKeyDown;
    }

    private void OnWebViewKeyDown(object? sender, KeyEventArgs e)
    {
        if (e == null) return;
        var isAlt = (e.KeyModifiers & KeyModifiers.Alt) == KeyModifiers.Alt;

        if (e.Key == Key.F11)
        {
            ToggleFullscreen();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.F && isAlt)
        {
            // Force single-WebView fullscreen
            EnterFullscreen();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.F)
        {
            ToggleFullscreen();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape && _isFullscreen)
        {
            ExitFullscreen();
            TryExitDocumentFullscreen();
            e.Handled = true;
            return;
        }
    }

    // React to WebView property changes (e.g., Address changes, CanGoBack/Forward, Title)
    private async void WebView_OnPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        try
        {
            if (_tabs.Active == null) return;
            var vw = _tabs.Active.WebView;
            if (!ReferenceEquals(sender, vw)) return; // only react to active

            var prop = e.Property?.Name;
            if (prop == "Address")
            {
                var url = _tabs.Active.Manager.GetCurrentUrl();
                _lastFaviconUrl = url; // keep poll baseline in sync
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    var nav = _normalModePage?.NavBar;
                    if (nav != null)
                    {
                        nav.Url = url ?? string.Empty;
                        nav.CanGoBack = vw.CanGoBack;
                        nav.CanGoForward = vw.CanGoForward;
                    }
                });

                await UpdateFaviconAsync(url);
                await UpdateTabTitleAsync(null, url);
                
                // Зберігаємо в історію
                try
                {
                    if (!string.IsNullOrWhiteSpace(url))
                    {
                        var title = vw.Title ?? url;
                        System.Diagnostics.Debug.WriteLine($"[MainWindow] Adding to history: {url} - {title}");
                        DatabaseManager.HistoryInstance.AddOrUpdateHistoryItem(
                            url: url,
                            title: title,
                            faviconUrl: null,
                            faviconData: null
                        );
                    }
                }
                catch (Exception historyEx)
                {
                    System.Diagnostics.Debug.WriteLine($"[MainWindow] Error adding to history: {historyEx.Message}");
                }
            }
            else if (prop == "CanGoBack" || prop == "CanGoForward")
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    var nav = _normalModePage?.NavBar;
                    if (nav != null)
                    {
                        nav.CanGoBack = vw.CanGoBack;
                        nav.CanGoForward = vw.CanGoForward;
                    }
                });
            }
            else if (prop == "Title")
            {
                // Direct access to Title property
                var pageTitle = vw.Title;
                _lastPageTitle = pageTitle ?? _lastPageTitle;
                await UpdateTabTitleAsync(pageTitle, vw.Address);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MainWindow] WebView_OnPropertyChanged error: {ex.Message}");
        }
    }

    private async void TryExitDocumentFullscreen()
    {
        try
        {
            var active = _tabs.Active;
            if (active == null) return;
            // Best-effort: try standard and webkit-prefixed APIs
            const string js = "(function(){try{if(document.fullscreenElement&&document.exitFullscreen){document.exitFullscreen();}else if(document.webkitFullscreenElement&&document.webkitExitFullscreen){document.webkitExitFullscreen();}}catch(e){}})();";
            await active.WebView.EvaluateScript<object>(js);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MainWindow] TryExitDocumentFullscreen error: {ex.Message}");
        }
    }

    // Handle fullscreen requests from web content (e.g., YouTube videos)
    private void OnWorkerFullscreenChanged(object? sender, bool isFullscreen)
    {
        System.Diagnostics.Debug.WriteLine($"[MainWindow] Worker fullscreen changed: {isFullscreen}");
        Dispatcher.UIThread.Post(() =>
        {
            if (isFullscreen) EnterFullscreen(); else ExitFullscreen();
        });
    }

    private void OnTabCreated(object? sender, TabWorker e)
    {
        // No-op: UI for tabs is created explicitly in CreateNewTab to avoid duplicates
    }


    private void OnTabActivated(object? sender, TabWorker e)
    {
        // Sync UI states when manager reports activation (already handled by ActivateWorker)
    }

    private void OnTabClosed(object? sender, TabWorker e)
    {
        // If the closed tab was active, Activate() in TabsManager already switched to another
        if (_tabs.Active != null)
        {
            ActivateWorker(_tabs.Active);
        }
        else
        {
            // Ensure at least one tab exists
            // Use selected search engine homepage
            _ = Dispatcher.UIThread.InvokeAsync(async () =>
            {
                var home = await GetSearchHomePageAsync();
                CreateNewTab(home);
            });
        }
    }

    // Get the document title from the WebView using direct property access
    private static string? TryGetWebViewTitle(WebView vw)
    {
        try
        {
            return vw.Title;
        }
        catch
        {
            return null;
        }
    }

    private async Task UpdateFaviconAsync(string? address)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(address)) return;
            if (!Uri.TryCreate(address, UriKind.Absolute, out var uri)) return;

            // Determine scale for better icon size
            var visualRoot = this.GetVisualRoot();
            double scale = 1.0;
            if (visualRoot is TopLevel top)
            {
                scale = top.RenderScaling;
            }

            var size = scale >= 1.5 ? 48 : 32;
            System.Diagnostics.Debug.WriteLine($"[MainWindow] UpdateFavicon: url={uri} size={size} scale={scale:0.00}");

            var image = await _faviconService.GetFaviconAsync(uri, size);
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                var tabsHost = _normalModePage?.TabsHostPanel;
                if (tabsHost != null && _tabs.Active != null)
                {
                    var idx = _tabs.Workers.ToList().IndexOf(_tabs.Active);
                    var childIdx = idx + 1; // account for add button
                    if (idx >= 0 && childIdx < tabsHost.Children.Count && tabsHost.Children[childIdx] is Tab tab)
                    {
                        tab.FaviconSource = image;
                        System.Diagnostics.Debug.WriteLine($"[MainWindow] Favicon applied: hasImage={(image != null)}");
                    }
                }
            });
        }
        catch (Exception ex)
        {
            // ignore favicon failures
            System.Diagnostics.Debug.WriteLine($"[MainWindow] UpdateFavicon error: {ex.Message}");
        }
    }

    // Compute and apply a friendly tab title from the page title or URL
    private async Task UpdateTabTitleAsync(string? pageTitle, string? url)
    {
        try
        {
            var friendly = ComputeTitle(pageTitle, url);
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                var tabsHost = _normalModePage?.TabsHostPanel;
                if (tabsHost != null && _tabs.Active != null)
                {
                    var idx = _tabs.Workers.ToList().IndexOf(_tabs.Active);
                    var childIdx = idx + 1; // account for add button
                    if (idx >= 0 && childIdx < tabsHost.Children.Count && tabsHost.Children[childIdx] is Tab tab)
                    {
                        tab.Title = friendly;
                    }
                }
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MainWindow] UpdateTabTitle error: {ex.Message}");
        }
    }

    private static string ComputeTitle(string? pageTitle, string? url)
    {
        // Use document title if provided
        if (!string.IsNullOrWhiteSpace(pageTitle))
        {
            return pageTitle.Trim();
        }

        // Fallback to host if we have a URL
        if (!string.IsNullOrWhiteSpace(url) && Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            var host = uri.Host;
            if (host.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
                host = host.Substring(4);
            return host;
        }

        return "New Tab";
    }

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        if (_subscribedWorker != null)
        {
            try { _subscribedWorker.WebView.PropertyChanged -= WebView_OnPropertyChanged; } catch { }
            _subscribedWorker = null;
        }

        _tabs.Dispose();
        if (_faviconService is IDisposable d) d.Dispose();
        _faviconPollTimer.Stop();
    }


    private void MinimizeWindow(object? sender, RoutedEventArgs e)
    {
        _windowManager?.Minimize();
    }

    private void MaximizeWindow(object? sender, RoutedEventArgs e)
    {
        _windowManager?.ToggleMaximize();
    }

    private void CloseWindow(object? sender, RoutedEventArgs e)
    {
        _windowManager?.Close();
    }

    // Подвійний клік по верхній панелі -> максимізувати/відновити
    private void TopBar_DoubleTapped(object? sender, RoutedEventArgs e)
    {
        _windowManager?.OnTopBarDoubleTapped(sender, e);
    }

    // Перетягування вікна при натисканні на верхню панель
    private void TopBar_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            _windowManager?.TryBeginMoveDrag(e);
        }
    }

    // Event handlers for functionality that needs to be handled externally
    private void OnBookmarkRequested(object? sender, EventArgs e)
    {
        try
        {
            if (_tabs.Active == null)
            {
                System.Diagnostics.Debug.WriteLine("MainWindow: No active tab to bookmark");
                return;
            }

            // Отримуємо поточний URL та заголовок
            var currentUrl = _tabs.Active.Manager.GetCurrentUrl() ?? string.Empty;
            var currentTitle = _tabs.Active.Title ?? TryGetWebViewTitle(_tabs.Active.WebView) ?? "Без назви";

            System.Diagnostics.Debug.WriteLine($"MainWindow: Opening bookmarks window with URL: {currentUrl}, Title: {currentTitle}");

            // Відкриваємо вікно закладок з формою додавання
            var bookmarksWindow = new BookmarksWindow(currentUrl, currentTitle);
            bookmarksWindow.Show();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"MainWindow: Error opening bookmarks window: {ex}");
        }
    }

    private void OnToolsRequested(object? sender, EventArgs e)
    {
        // Open tools window styled like the main window
        try
        {
            var tools = new ToolsWindow();
            tools.Show();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"MainWindow: Failed to open ToolsWindow: {ex}");
        }
    }

    private void OnSettingsRequested(object? sender, EventArgs e)
    {
        // Open settings window styled like the main window
        try
        {
            var settings = new SettingsWindow();
            settings.Show();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"MainWindow: Failed to open SettingsWindow: {ex}");
        }
    }

    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        // F11 toggles fullscreen
        if (e.Key == Key.F11)
        {
            System.Diagnostics.Debug.WriteLine("[MainWindow] F11 pressed - toggling fullscreen");
            ToggleFullscreen();
            e.Handled = true;
        }
        // Alt+F forces single-WebView fullscreen (global shortcut)
        else if (e.Key == Key.F && (e.KeyModifiers & KeyModifiers.Alt) == KeyModifiers.Alt)
        {
            System.Diagnostics.Debug.WriteLine("[MainWindow] Alt+F pressed - forcing fullscreen (single WebView)");
            EnterFullscreen();
            e.Handled = true;
        }
        // F also toggles fullscreen (for video playback)
        else if (e.Key == Key.F)
        {
            System.Diagnostics.Debug.WriteLine("[MainWindow] F pressed - toggling fullscreen");
            ToggleFullscreen();
            e.Handled = true;
        }
        // ESC exits fullscreen
        else if (e.Key == Key.Escape && _isFullscreen)
        {
            System.Diagnostics.Debug.WriteLine("[MainWindow] ESC pressed - exiting fullscreen");
            ExitFullscreen();
            // Also request the page to exit document fullscreen if it was set
            TryExitDocumentFullscreen();
            e.Handled = true;
        }
    }

    public void EnterFullscreen()
    {
        if (_isFullscreen) return;

        _isFullscreen = true;
        _preFullscreenWindowState = WindowState;

        System.Diagnostics.Debug.WriteLine("[MainWindow] === ENTERING FULLSCREEN ===");
        
        // Switch to fullscreen page
        if (_pageContainer != null && _fullscreenModePage != null)
        {
            _pageContainer.Content = _fullscreenModePage;
            System.Diagnostics.Debug.WriteLine("[MainWindow] Switched to FullscreenModePage");
        }

        // Move active WebView to fullscreen container
        if (_fullscreenModePage?.FullscreenGrid != null && _tabs.Active != null)
        {
            MoveActiveWebViewTo(_fullscreenModePage.FullscreenGrid);
        }

        // Remove window padding
        Padding = new Thickness(0);
        
        // Enter fullscreen mode
        WindowState = WindowState.FullScreen;
        SystemDecorations = SystemDecorations.None;
        
        System.Diagnostics.Debug.WriteLine("[MainWindow] === FULLSCREEN ENTERED ===");
    }

    public void ExitFullscreen()
    {
        if (!_isFullscreen) return;

        _isFullscreen = false;

        System.Diagnostics.Debug.WriteLine("[MainWindow] === EXITING FULLSCREEN ===");

        // Switch back to normal page
        if (_pageContainer != null && _normalModePage != null)
        {
            _pageContainer.Content = _normalModePage;
            System.Diagnostics.Debug.WriteLine("[MainWindow] Switched to NormalModePage");
        }

        // Move WebView back to normal container
        if (_normalModePage?.WebViewGrid != null && _tabs.Active != null)
        {
            MoveActiveWebViewTo(_normalModePage.WebViewGrid);
        }

        // Restore padding
        Padding = new Thickness(8);
        
        // Exit fullscreen mode
        SystemDecorations = SystemDecorations.BorderOnly;
        WindowState = _preFullscreenWindowState;
        
        System.Diagnostics.Debug.WriteLine("[MainWindow] === FULLSCREEN EXITED ===");

        // Ask page to exit document fullscreen if any (best-effort)
        TryExitDocumentFullscreen();
    }

    public void ToggleFullscreen()
    {
        if (_isFullscreen)
            ExitFullscreen();
        else
            EnterFullscreen();
    }

    public void NavigateUrlInActiveTab(string url, bool openInNewTabIfNone = true)
    {
        if (string.IsNullOrWhiteSpace(url)) return;

        try
        {
            if (_tabs.Active != null)
            {
                _tabs.Active.Navigate(url);
            }
            else if (openInNewTabIfNone)
            {
                CreateNewTab(url);
            }
            else
            {
                // No active tab and not allowed to create new one; do nothing
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"MainWindow: Failed to navigate active tab: {ex}");
        }
    }

    // Public entry to navigate to homepage of the selected search engine
    public async Task NavigateToSelectedSearchHomeAsync()
    {
        try
        {
            var home = await GetSearchHomePageAsync();
            NavigateUrlInActiveTab(home, openInNewTabIfNone: true);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MainWindow] NavigateToSelectedSearchHomeAsync error: {ex.Message}");
        }
    }

    private async Task<string> GetSearchHomePageAsync()
    {
        try
        {
            // Prefer configured search engine URL template
            string template = "https://www.google.com/search?q={0}";
            if (_settingsService != null)
            {
                try
                {
                    var t = await _settingsService.GetSearchEngineUrlAsync();
                    if (!string.IsNullOrWhiteSpace(t))
                        template = t;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[MainWindow] GetSearchHomePageAsync: failed to get template: {ex.Message}");
                }
            }

            // Derive homepage from template: take scheme+host root
            string basePart = template;
            var qIdx = template.IndexOf('?');
            if (qIdx >= 0)
                basePart = template.Substring(0, qIdx);

            if (Uri.TryCreate(basePart, UriKind.Absolute, out var uri))
            {
                var home = $"{uri.Scheme}://{uri.Host}/";
                return home;
            }

            // Fallback to Google
            return "https://www.google.com/";
        }
        catch
        {
            return "https://www.google.com/";
        }
    }
}
