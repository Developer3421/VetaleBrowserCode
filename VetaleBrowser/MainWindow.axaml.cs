using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;
using VetaleBrowser.VetaleBrowser.UI.Scripts;
using VetaleBrowser.VetaleBrowser.Core.Scripts.GlobalManagers;

using VetaleBrowser.VetaleBrowser.UI.Services;
using VetaleBrowser.VetaleBrowser.Core.Scripts.Browser;
using Avalonia.Threading;
using Avalonia;
using VetaleBrowser.VetaleBrowser.Core.Scripts.Models;
using VetaleBrowser.VetaleBrowser.Core.Scripts.ErrorHandlers;
using VetaleBrowser.VetaleBrowser.Core.Scripts.Services;
using System.Linq;
using VetaleBrowser.VetaleBrowser.UI.Pages;
using VetaleBrowser.VetaleBrowser.UI.Windows; // added for SettingsWindow and ToolsWindow
using VetaleBrowser.VetaleBrowser.Database;
using VetaleBrowser.VetaleBrowser.Database.Services;
using Avalonia.Media;
using VetaleBrowser.VetaleBrowser.Core.Services;
using VetaleBrowser.VetaleBrowser.Core.Services.Windows;
using VetaleBrowser.VetaleBrowser.UI.Elements;

namespace VetaleBrowser;

public partial class MainWindow : Window
{
    private readonly WindowManager? _windowManager;
    private readonly IFaviconService _faviconService = new FaviconService();
    private ISettingsService? _settingsService;
    private IAppearanceSettingsService? _appearanceSettingsService;

    private readonly TabsManager _tabs = new();
    private readonly HashSet<Guid> _historySubscribedWorkers = new();

    // Сервіси для пошуку
    private readonly VetaleBrowser.Search.Services.ISearchNavigationService _searchNavigationService = new VetaleBrowser.Search.Services.SearchNavigationService();
    private VetaleBrowser.Search.Database.ISearchHistoryDatabaseService? _searchHistoryService;

    // Public property to access TabsManager
    public TabsManager TabsManager => _tabs;
    
    /// <summary>
    /// Публічний метод для створення нової вкладки з URL (підтримує vetale:// протокол)
    /// </summary>
    public void NavigateToUrl(string url)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine($"[MainWindow] NavigateToUrl called with: {url}");
            CreateNewTab(url);
            
            // Активувати вікно
            Activate();
            Focus();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MainWindow] NavigateToUrl error: {ex}");
        }
    }

    private int NextFaviconRequestVersion(TabWorker worker)
    {
        if (!_faviconRequestVersions.TryGetValue(worker.Id, out var version))
            version = 0;
        version++;
        _faviconRequestVersions[worker.Id] = version;
        return version;
    }

    private bool IsCurrentFaviconRequest(TabWorker worker, int version)
        => _faviconRequestVersions.TryGetValue(worker.Id, out var current) && current == version;

    private void ApplyFaviconToTab(TabWorker worker, Avalonia.Media.IImage? image)
    {
        var mainPanelTab = FindTabByWorker(worker);
        if (mainPanelTab != null)
        {
            mainPanelTab.FaviconSource = image;
            return;
        }

        foreach (var overflowWindow in _tabOverflowWindows)
        {
            if (overflowWindow.GetTabByWorker(worker) != null)
            {
                overflowWindow.UpdateTabFavicon(worker, image);
                return;
            }
        }
    }
    
    /// <summary>
    /// Публічний метод для навігації поточної вкладки на URL (підтримує vetale:// протокол)
    /// </summary>
    public void NavigateCurrentTabToUrl(string url)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine($"[MainWindow] NavigateCurrentTabToUrl called with: {url}");
            
            var activeWorker = _tabs.Active;
            if (activeWorker == null)
            {
                // Якщо немає активної вкладки, створюємо нову
                CreateNewTab(url);
            }
            else if (InternalUrlHandler.IsInternalUrl(url))
            {
                // Оновлюємо поточну вкладку на внутрішню сторінку (VetaleSearch, закладки, історія тощо)
                HandleInternalNavigation(url);
            }
            else
            {
                // Оновлюємо поточну вкладку на звичайний URL (http/https, chrome://gpu тощо)
                // Через TabWorker.Navigate щоб зберегти історію та коректно обійти PreCheck для chrome://
                activeWorker.Navigate(url);
            }
            
            // Активувати вікно
            Activate();
            Focus();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MainWindow] NavigateCurrentTabToUrl error: {ex}");
        }
    }

    // Pages
    private NormalModePage? _normalModePage;
    private FullscreenModePage? _fullscreenModePage;
    private VideoFullscreenPage? _videoFullscreenPage;
    private ContentControl? _pageContainer;
    
    // MEMORY OPTIMIZATION: Lazy initialization of heavy services
    private VetaleBrowser.Search.Services.ISuggestionsService? _globalSuggestions;
    private VetaleBrowser.Search.Services.ISecurityCheckService? _securityCheckService;
    
    // Lazy getters for services
    private VetaleBrowser.Search.Services.ISuggestionsService GlobalSuggestions 
        => _globalSuggestions ??= new VetaleBrowser.Search.Services.GoogleSuggestionsService();
    
    private VetaleBrowser.Search.Services.ISecurityCheckService SecurityCheckService 
        => _securityCheckService ??= new VetaleBrowser.Search.Services.PhishTankSecurityService();

    // Keep track of which worker's WebView we're listening to
    private TabWorker? _subscribedWorker;
    private readonly HashSet<TabWorker> _propertySubscribedWorkers = new();

    // Fullscreen state
    private bool _isFullscreen;
    private WindowState _preFullscreenWindowState;
    private Avalonia.Controls.WindowDecorations _preFullscreenDecorations;

    // HTML5 video fullscreen (YouTube "F") без JS-моста: детект через процеси.
    // WebView НЕ пересаджуємо (від цього відео і відривається), тільки ховаємо хром.
    private VideoFullscreenWatcher? _videoFullscreenWatcher;
    private bool _videoFullscreen;
    private WindowState _preVideoFullscreenWindowState;
    private Avalonia.Controls.WindowDecorations _preVideoFullscreenDecorations;

    // Polling support for robust favicon and title updates - MEMORY OPTIMIZATION: longer interval
    private readonly DispatcherTimer _faviconPollTimer = new() { Interval = TimeSpan.FromMilliseconds(1000) };
    private string? _lastFaviconUrl;
    private string? _lastPageTitle;
    private readonly Dictionary<Guid, int> _faviconRequestVersions = new();

    // Cached appearance values
    private double _tabWidth = 200.0;
    
    // Tab overflow system - підтримка множинних overflow вікон
    private readonly List<TabOverflowWindow> _tabOverflowWindows = new();
    private readonly Dictionary<Tab, TabWorker> _mainPanelTabWorkerMap = new();
    private const int MaxTabsNormalMode = 4;   // Максимум вкладок у звичайному режимі
    private const int MaxTabsMaximizedMode = 8; // Максимум вкладок у розгорнутому вікні
    private const int MaxTabsFullscreenMode = 9; // Максимум вкладок у повноекранному режимі
    private const int MaxTabsPerOverflowWindow = 6; // Максимум вкладок в одному overflow вікні
    
    // Drag-and-drop support for main tabs panel
    private Border? _mainTabsDropIndicator;
    private int _mainTabsDropTargetIndex = -1;
    
    // Статичний список всіх екземплярів MainWindow для drag-and-drop між вікнами
    private static readonly List<MainWindow> _allMainWindows = new();
    
    /// <summary>Поточний ліміт вкладок залежно від режиму</summary>
    private int CurrentMaxTabs
    {
        get
        {
            if (_isFullscreen) return MaxTabsFullscreenMode;
            if (WindowState == WindowState.Maximized) return MaxTabsMaximizedMode;
            return MaxTabsNormalMode;
        }
    }

    public MainWindow()
    {
        // Реєструємо це вікно в статичному списку
        _allMainWindows.Add(this);
        
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

        // Wire internal navigation callbacks early
        try
        {
            VetaleBrowser.UI.Services.InternalUrlHandler.SuggestionsServiceProvider = () => GlobalSuggestions;
            VetaleBrowser.UI.Services.InternalUrlHandler.NavigationRequestCallback = NavigateCurrentTabToUrl;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MainWindow] InternalUrlHandler wiring failed: {ex.Message}");
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
        // Handle F11 before the native WebView sees it. Otherwise Chromium
        // enters its own fullscreen mode and leaves the host window unchanged.
        this.AddHandler(InputElement.KeyDownEvent, OnWindowKeyDown, RoutingStrategies.Tunnel);
        
        // Handle window state changes (maximized/restored) to reorganize tabs
        this.PropertyChanged += OnMainWindowPropertyChanged;

        _tabs.TabActivated += OnTabActivated;
        _tabs.TabClosed += OnTabClosed;
        // Removed TabCreated subscription to avoid duplicate Tab controls
        // _tabs.TabCreated += OnTabCreated;

        // PRO-style poll: covers redirects/SPA for ACTIVE tab (address+title),
        // plus live favicon refresh for ALL tabs (background tabs too).
        _faviconPollTimer.Tick += async (_, _) =>
        {
            try
            {
                foreach (var w in _tabs.Workers.ToArray())
                {
                    try { _ = w.RefreshFaviconAsync(); } catch { }
                }
                var active = _tabs.Active;
                if (active != null)
                {
                    var url = active.Manager.GetCurrentUrl();
                    if (!string.IsNullOrWhiteSpace(url) && !string.Equals(url, _lastFaviconUrl, StringComparison.Ordinal))
                    {
                        _lastFaviconUrl = url;
                        await UpdateFaviconAsync(url, active);
                        await UpdateTabTitleAsync(null, url, active);
                    }

                    var currentTitle = TryGetWebViewTitle(active.WebView);
                    if (!string.IsNullOrWhiteSpace(currentTitle) && !string.Equals(currentTitle, _lastPageTitle, StringComparison.Ordinal))
                    {
                        _lastPageTitle = currentTitle;
                        await UpdateTabTitleAsync(currentTitle, url, active);
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

            System.Diagnostics.Debug.WriteLine("[MainWindow] InitializeComponent: Creating VideoFullscreenPage...");
            _videoFullscreenPage = new VideoFullscreenPage();
            System.Diagnostics.Debug.WriteLine("[MainWindow] InitializeComponent: VideoFullscreenPage created");
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
            
            // Setup new window button handler
            if (_normalModePage.NewWindowBtn != null)
            {
                _normalModePage.NewWindowBtn.Click += OnNewWindowBtnClick;
                System.Diagnostics.Debug.WriteLine("[MainWindow] InitializeComponent: NewWindowBtn handler attached");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("[MainWindow] WARNING: NewWindowBtn is null");
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
            
            // Setup drag-and-drop for main tabs panel
            SetupMainTabsDragDrop();
            
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
            System.Diagnostics.Debug.WriteLine("[MainWindow] Initializing settings services via DatabaseServicesFactory...");
            
            // Ініціалізуємо через централізовану фабрику
            DatabaseServicesFactory.Initialize();
            
            // Отримуємо сервіси
            _settingsService = DatabaseServicesFactory.TryGetSettingsService();
            System.Diagnostics.Debug.WriteLine($"[MainWindow] SettingsService: {(_settingsService != null ? "OK" : "NULL")}");
            
            // Ініціалізуємо сервіс історії пошуку
            var config = DatabaseConfiguration.CreateDefault();
            var searchHistoryPath = System.IO.Path.Combine(
                System.IO.Path.GetDirectoryName(config.DatabasePath) ?? string.Empty,
                "Search",
                "search_history.db");
            
            // Гарантуємо директорію для історії пошуку
            var searchDir = System.IO.Path.GetDirectoryName(searchHistoryPath);
            if (!string.IsNullOrEmpty(searchDir) && !System.IO.Directory.Exists(searchDir))
            {
                System.IO.Directory.CreateDirectory(searchDir);
            }
            
            _searchHistoryService = new VetaleBrowser.Search.Database.SearchHistoryDatabaseService(searchHistoryPath, config.EncryptionKey);
            System.Diagnostics.Debug.WriteLine("[MainWindow] Search history service initialized");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MainWindow] Error initializing settings service: {ex}");
        }
    }

    private void InitializeAppearanceSettingsService()
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("[MainWindow] Initializing appearance settings service...");
            
            // Отримуємо сервіс через фабрику
            _appearanceSettingsService = DatabaseServicesFactory.TryGetAppearanceSettingsService();
            System.Diagnostics.Debug.WriteLine($"[MainWindow] AppearanceSettingsService: {(_appearanceSettingsService != null ? "OK" : "NULL")}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MainWindow] Error initializing appearance settings service: {ex}");
        }
    }

    private void EnsureActiveWebViewMounted()
    {
        var active = _tabs.Active;
        var container = _normalModePage?.WebViewGrid;
        if (active == null || container == null)
            return;

        var current = active.History.CurrentEntry;
        if (current?.IsInternal == true && current.InternalPageContent != null)
            return;

        var view = active.WebView.View;
        if (view.Parent is Panel parent && !ReferenceEquals(parent, container))
            parent.Children.Remove(view);

        if (!container.Children.Contains(view))
        {
            container.Children.Clear();
            view.IsVisible = true;
            view.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch;
            view.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch;
            container.Children.Add(view);
        }

        // Навігуємо тільки якщо браузер ще порожній — інакше перемикання вкладок
        // перезавантажувало б сторінку і вбивало стан таба.
        var currentAddr = active.WebView.Address;
        if (!string.IsNullOrWhiteSpace(active.Address) &&
            (string.IsNullOrWhiteSpace(currentAddr) ||
             currentAddr.StartsWith("about:", StringComparison.OrdinalIgnoreCase)) &&
            !string.Equals(currentAddr, active.Address, StringComparison.OrdinalIgnoreCase))
            active.WebView.LoadUrl(active.Address);
    }

    private async void OnWindowLoaded(object? sender, RoutedEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine("MainWindow: OnWindowLoaded called");

        try
        {
            // MEMORY OPTIMIZATION: Set lazy service providers - services created on first use only
            InternalUrlHandler.SuggestionsServiceProvider = () => GlobalSuggestions;
            System.Diagnostics.Debug.WriteLine("[MainWindow] Lazy service providers configured");
            
            // Ensure there's at least one tab
            if (_tabs.Active == null)
            {
                try
                {
                    var home = await GetSearchHomePageAsync();
                    System.Diagnostics.Debug.WriteLine($"[MainWindow] Creating initial tab with URL: {home}");
                    CreateNewTab(home);
                    
                    // Оновлюємо іконку та заголовок для початкової вкладки
                    await UpdateFaviconAsync(home);
                    await UpdateTabTitleAsync("Vetale Search", home);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"MainWindow: Failed to create initial tab: {ex.Message}");
                    // Fallback to Google if settings fail
                    CreateNewTab("https://www.google.com");
                }
            }

            EnsureActiveWebViewMounted();

            // Initialize NavigationBar with the active tab's manager
            try
            {
                var navigationBar = _normalModePage?.NavBar;
                if (navigationBar != null && _tabs.Active != null)
                {
                    navigationBar.SetSuggestionsService(GlobalSuggestions);
                    navigationBar.SetSecurityCheckService(SecurityCheckService);
                    // Підписка на внутрішню навігацію (vetale://) з адресного рядка/Додому
                    navigationBar.NavigateRequested -= OnNavigationBarNavigateRequested;
                    navigationBar.NavigateRequested += OnNavigationBarNavigateRequested;
                    navigationBar.BackRequested -= OnNavBarBackRequested;
                    navigationBar.BackRequested += OnNavBarBackRequested;
                    navigationBar.ForwardRequested -= OnNavBarForwardRequested;
                    navigationBar.ForwardRequested += OnNavBarForwardRequested;
                    navigationBar.ReloadRequested -= OnNavBarReloadRequested;
                    navigationBar.ReloadRequested += OnNavBarReloadRequested;
                    navigationBar.HomeRequested -= OnNavBarHomeRequested;
                    navigationBar.HomeRequested += OnNavBarHomeRequested;
                    navigationBar.BindWorker(_tabs.Active);
                    
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

                // Раніше тут була підписка на Manager.Navigated — видалено щоб уникнути циклів
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

            // Детект HTML5-фулскріна йде через рушій (EvaluateScriptAsync), DevTools-порт закритий.
            // Вотчер вікон вимкнено щоб не було подвійної обробки.
            // try
            // {
            //     if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
            //     {
            //         _videoFullscreenWatcher = new VideoFullscreenWatcher(GetMainWindowHandle);
            //         _videoFullscreenWatcher.FullscreenChanged += OnVideoFullscreenChanged;
            //         _videoFullscreenWatcher.Start();
            //     }
            // }
            // catch (Exception ex)
            // {
            //     System.Diagnostics.Debug.WriteLine($"MainWindow: VideoFullscreenWatcher failed: {ex.Message}");
            // }

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

    private TabWorker? CreateNewTab(string? initialUrl = null)
    {
        // Створюємо worker БЕЗ початкової навігації (передаємо null)
        var worker = _tabs.Create(null);
        AddTabControlForWorker(worker);
        
        // Підписуємося на події навігації
        worker.NavigationChanged += OnWorkerNavigationChanged;
        worker.FaviconChanged += OnWorkerFaviconChanged;
        worker.FaviconDataChanged += OnWorkerFaviconDataChanged;
        worker.TitleChanged += OnWorkerTitleChangedForFavicon;
        SubscribeWorkerHistoryUpdates(worker);
        
        // Підписуємося на події помилок для локалізованих сторінок помилок
        worker.ErrorOccurred += OnWorkerErrorOccurred;
        
        // Якщо є початковий URL, навігуємо ОДИН РАЗ з правильним content
        if (!string.IsNullOrEmpty(initialUrl))
        {
            if (InternalUrlHandler.IsInternalUrl(initialUrl))
            {
                // Для внутрішніх URL створюємо контент і навігаємо
                var content = InternalUrlHandler.CreatePageContent(initialUrl);
                if (content != null)
                {
                    // Підписуємося на події від внутрішніх сторінок
                    SubscribeToInternalPageEvents(content);
                    worker.Navigate(initialUrl, content);
                }
            }
            else if (ChromiumInternalHandler.IsChromiumInternalUrl(initialUrl))
            {
                // chrome://gpu, chrome://version - вбудовані сторінки (рушій схеми не має)
                var content = ChromiumInternalHandler.CreatePageContent(initialUrl);
                worker.Navigate(initialUrl, content);
            }
            else
            {
                // Для зовнішніх URL навігуємо без content
                worker.Navigate(initialUrl);
            }
        }
        
        // Іконка Vetale Search ставиться одразу при створенні вкладки
        if (IsVetaleSearchPageUrl(initialUrl))
        {
            ApplyVetaleSearchIconToTab(worker);
        }

        ActivateWorker(worker);
        return worker;
    }
    
    /// <summary>
    /// Підписується на зміни історії вкладки, щоб кнопки Назад/Вперед
    /// у 2-й лінії (NavigationBar) завжди відображали актуальний стан.
    /// </summary>
    private void OnWorkerFaviconChanged(object? sender, string? iconUrl)
    {
        try
        {
            if (sender is not TabWorker worker || string.IsNullOrWhiteSpace(iconUrl)) return;
            _ = ApplyPageIconAsync(worker, iconUrl);
            // Іконка прийшла пізніше коміта — дозбагачуємо запис історії.
            if (!string.IsNullOrWhiteSpace(worker.Address))
                UpsertHistory(worker, worker.Address);
        }
        catch { }
    }

    /// <summary>
    /// Байти іконки докачались — перезаписуємо запис історії свіжими байтами.
    /// Без цього історія показувала байти ПОПЕРЕДНЬОГО сайту/пошуковика
    /// (запис створювався на commit зі старими FaviconData, а merge не чіпає null).
    /// </summary>
    private void OnWorkerFaviconDataChanged(object? sender, byte[] data)
    {
        try
        {
            if (sender is not TabWorker worker || data == null || data.Length == 0) return;
            if (string.IsNullOrWhiteSpace(worker.Address)) return;
            // Перевірка: байти відповідають поточному URL вкладки, а не старій навігації.
            UpsertHistory(worker, worker.Address);
        }
        catch { }
    }

    private void OnWorkerTitleChangedForFavicon(object? sender, string? title)
    {
        try
        {
            if (sender is not TabWorker worker) return;
            // Title приїхав = сторінка реально завантажилась -> оновлюємо іконку (як Chrome)
            var addr = worker.Address;
            _ = UpdateFaviconAsync(addr, worker);
        }
        catch { }
    }

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, Avalonia.Media.IImage> _iconUrlCache = new();

    private async Task ApplyPageIconAsync(TabWorker worker, string iconUrl)
    {
        try
        {
            var addressAtStart = worker.Address;
            if (_iconUrlCache.TryGetValue(iconUrl, out var cached))
            {
                if (string.Equals(worker.Address, addressAtStart, StringComparison.OrdinalIgnoreCase))
                    ApplyFaviconToTab(worker, cached);
                return;
            }
            using var http = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            var bytes = await http.GetByteArrayAsync(iconUrl);
            if (bytes == null || bytes.Length == 0) return;
            Avalonia.Media.IImage? image;
            using (var ms = new MemoryStream(bytes)) image = new Avalonia.Media.Imaging.Bitmap(ms);
            _iconUrlCache[iconUrl] = image;
            if (!string.Equals(worker.Address, addressAtStart, StringComparison.OrdinalIgnoreCase)) return;
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                if (!string.Equals(worker.Address, addressAtStart, StringComparison.OrdinalIgnoreCase)) return;
                ApplyFaviconToTab(worker, image);
            });
        }
        catch { }
    }

    private void SubscribeWorkerHistoryUpdates(TabWorker worker)
    {
        if (!_historySubscribedWorkers.Add(worker.Id)) return;
        worker.History.HistoryChanged += (_, __) =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (_tabs.Active != worker) return;
                var navBar = _normalModePage?.NavBar;
                if (navBar == null) return;
                navBar.SetState(worker.Address, worker.CanGoBack, worker.CanGoForward);
            });
        };
    }

    /// <summary>
    /// Підписується на події навігації від внутрішніх сторінок
    /// </summary>
    private void SubscribeToInternalPageEvents(UserControl content)
    {
        if (content is VetaleSearchHomePage searchHomePage)
        {
            searchHomePage.NavigateRequested += OnInternalPageNavigateRequested;
        }
        else if (content is VetaleSearchResultsPage resultsPage)
        {
            resultsPage.NavigateRequested += OnInternalPageNavigateRequested;
            resultsPage.SearchResultNavigateRequested += OnSearchResultNavigateRequested;
        }
    }
    
    /// <summary>
    /// Створити вкладку з внутрішньою сторінкою браузера (DEPRECATED - використовується CreateNewTab)
    /// </summary>
    private TabWorker? CreateInternalPageTab(string url)
    {
        // Ця функція тепер делегує до CreateNewTab
        return CreateNewTab(url);
    }
    
    /// <summary>
    /// Активувати вкладку з внутрішньою сторінкою
    /// </summary>
    private void ActivateWorkerForInternalPage(TabWorker worker, UserControl pageContent, string? urlOverride = null)
    {
        _tabs.Activate(worker);
        
        // Використовуємо urlOverride або поточну адресу воркера
        var finalUrl = urlOverride ?? worker.Address ?? "";
        System.Diagnostics.Debug.WriteLine($"[MainWindow] ActivateWorkerForInternalPage - URL: {finalUrl}");
        
        var targetContainer = _isFullscreen 
            ? _fullscreenModePage?.FullscreenGrid 
            : _normalModePage?.WebViewGrid;
            
        if (targetContainer != null)
        {
            ShowControlInContainer(targetContainer, pageContent);
            System.Diagnostics.Debug.WriteLine($"[MainWindow] Internal page added to container: {pageContent.GetType().Name}");
        }
        
        // По мапі worker->Tab, а не за індексом (порядок дітей може не збігатися з воркерами).
        foreach (var w in _tabs.Workers)
        {
            var t = FindTabByWorker(w);
            if (t != null)
                t.IsActive = (w == worker);
        }
        
        // Оновлюємо NavBar з новим URL
        var navBar = _normalModePage?.NavBar;
        if (navBar != null)
        {
            navBar.Url = finalUrl;
            navBar.CanGoBack = worker.CanGoBack;
            navBar.CanGoForward = worker.CanGoForward;
            System.Diagnostics.Debug.WriteLine($"[MainWindow] NavBar updated: Url={navBar.Url}, CanGoBack={navBar.CanGoBack}, CanGoForward={navBar.CanGoForward}");
        }
    }

    /// <summary>
    /// Створює новий екземпляр головного вікна браузера
    /// </summary>
    private void OnNewWindowBtnClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("[MainWindow] Creating new browser window...");
            
            var newWindow = new MainWindow();
            newWindow.Show();
            
            // Позиціонуємо нове вікно з невеликим зміщенням
            newWindow.Position = new Avalonia.PixelPoint(this.Position.X + 30, this.Position.Y + 30);
            
            System.Diagnostics.Debug.WriteLine($"[MainWindow] New browser window created. Total windows: {_allMainWindows.Count}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MainWindow] OnNewWindowBtnClick error: {ex.Message}");
        }
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

        var shouldOverflow = ShouldUseOverflow();
        System.Diagnostics.Debug.WriteLine($"[MainWindow] AddTabControlForWorker: ShouldUseOverflow={shouldOverflow}, CurrentTabCount={GetMainPanelTabCount()}, MaxTabs={CurrentMaxTabs}, IsFullscreen={_isFullscreen}");

        // Перевіряємо чи є місце в основній панелі
        if (shouldOverflow)
        {
            // Додаємо до overflow вікна
            System.Diagnostics.Debug.WriteLine("[MainWindow] Adding tab to overflow window");
            AddTabToOverflow(worker);
            return;
        }

        var tab = CreateTabForWorker(worker, tabsHost);
        tabsHost.Children.Add(tab);
        System.Diagnostics.Debug.WriteLine($"[MainWindow] Tab added to main panel. New count={GetMainPanelTabCount()}");
    }

    /// <summary>
    /// Перевіряє чи треба використовувати overflow вікно
    /// Базується на кількості вкладок: 4 для звичайного режиму, 9 для fullscreen
    /// </summary>
    private bool ShouldUseOverflow()
    {
        var tabsHost = _normalModePage?.TabsHostPanel;
        if (tabsHost == null) return false;
        
        // Рахуємо поточну кількість вкладок (без кнопки "+")
        int currentTabCount = 0;
        foreach (var child in tabsHost.Children)
        {
            if (child is Tab)
            {
                currentTabCount++;
            }
        }
        
        // Перевіряємо чи досягнуто ліміту
        return currentTabCount >= CurrentMaxTabs;
    }
    
    /// <summary>
    /// Отримує кількість вкладок в основній панелі
    /// </summary>
    private int GetMainPanelTabCount()
    {
        var tabsHost = _normalModePage?.TabsHostPanel;
        if (tabsHost == null) return 0;
        
        int count = 0;
        foreach (var child in tabsHost.Children)
        {
            if (child is Tab)
            {
                count++;
            }
        }
        return count;
    }

    /// <summary>
    /// Створює вкладку для worker
    /// </summary>
    private Tab CreateTabForWorker(TabWorker worker, StackPanel tabsHost)
    {
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
            _mainPanelTabWorkerMap.Remove(tab);
            _tabs.Close(worker);
        };
        tab.MuteToggled += async (_, __) =>
        {
            await HandleMuteToggle(worker, tab, tabsHost);
        };
        
        // Підтримка перетягування (правою кнопкою миші)
        tab.DragStarted += (_, args) =>
        {
            if (args is TabDragStartedEventArgs dragArgs)
            {
                StartMainTabDrag(tab, worker, dragArgs.PointerEvent);
            }
        };

        // Додаємо до мапи для швидкого пошуку
        _mainPanelTabWorkerMap[tab] = worker;

        return tab;
    }

    /// <summary>
    /// Знаходить Tab в основній панелі по Worker
    /// </summary>
    private Tab? FindTabByWorker(TabWorker worker)
    {
        foreach (var kvp in _mainPanelTabWorkerMap)
        {
            if (kvp.Value == worker)
            {
                return kvp.Key;
            }
        }
        return null;
    }
    
    /// <summary>
    /// Публічний метод для видалення вкладки з основної панелі (для drag-and-drop між вікнами)
    /// </summary>
    public void RemoveTabFromMainPanel(TabWorker worker)
    {
        var tabsHost = _normalModePage?.TabsHostPanel;
        if (tabsHost == null) return;
        
        var existingTab = FindTabByWorker(worker);
        if (existingTab != null)
        {
            tabsHost.Children.Remove(existingTab);
            _mainPanelTabWorkerMap.Remove(existingTab);
            System.Diagnostics.Debug.WriteLine($"[MainWindow] RemoveTabFromMainPanel: Removed tab {existingTab.Title}");
        }
    }

    #region Main Tabs Drag-and-Drop
    
    /// <summary>
    /// Налаштовує drag-and-drop для основної панелі вкладок
    /// </summary>
    private void SetupMainTabsDragDrop()
    {
        var tabsHost = _normalModePage?.TabsHostPanel;
        if (tabsHost == null)
        {
            System.Diagnostics.Debug.WriteLine("[MainWindow] SetupMainTabsDragDrop: TabsHostPanel is null");
            return;
        }
        
        // Створюємо індикатор місця вставки
        _mainTabsDropIndicator = new Border
        {
            Width = 3,
            Height = 30,
            Background = new SolidColorBrush(Color.Parse("#7CB342")),
            CornerRadius = new CornerRadius(2),
            IsVisible = false,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            Margin = new Thickness(-1.5, 0, -1.5, 0)
        };
        
        // Додаємо підтримку drop
        tabsHost.AddHandler(DragDrop.DropEvent, OnMainTabsDrop);
        tabsHost.AddHandler(DragDrop.DragOverEvent, OnMainTabsDragOver);
        tabsHost.AddHandler(DragDrop.DragLeaveEvent, OnMainTabsDragLeave);
        DragDrop.SetAllowDrop(tabsHost, true);
        
        System.Diagnostics.Debug.WriteLine("[MainWindow] SetupMainTabsDragDrop: Drag-and-drop configured");
    }
    
    /// <summary>
    /// Обробник DragOver для основної панелі вкладок
    /// </summary>
    private void OnMainTabsDragOver(object? sender, DragEventArgs e)
    {
        var tabsHost = _normalModePage?.TabsHostPanel;
        if (tabsHost == null) return;
        
        if (e.DataTransfer.Contains(TabDragHelper.Format))
        {
            e.DragEffects = DragDropEffects.Move;

            // Визначаємо позицію для вставки
            var position = e.GetPosition(tabsHost);
            _mainTabsDropTargetIndex = CalculateMainTabsDropIndex(position.X, tabsHost);

            // Показуємо індикатор
            ShowMainTabsDropIndicator(_mainTabsDropTargetIndex, tabsHost);
        }
        else
        {
            e.DragEffects = DragDropEffects.None;
            HideMainTabsDropIndicator(tabsHost);
        }
    }
    
    /// <summary>
    /// Обробник DragLeave для основної панелі вкладок
    /// </summary>
    private void OnMainTabsDragLeave(object? sender, DragEventArgs e)
    {
        var tabsHost = _normalModePage?.TabsHostPanel;
        if (tabsHost != null)
        {
            HideMainTabsDropIndicator(tabsHost);
        }
    }
    
    /// <summary>
    /// Обробник Drop для основної панелі вкладок
    /// </summary>
    private void OnMainTabsDrop(object? sender, DragEventArgs e)
    {
        var tabsHost = _normalModePage?.TabsHostPanel;
        if (tabsHost == null) return;
        
        // Приховуємо індикатор
        int insertIndex = _mainTabsDropTargetIndex;
        HideMainTabsDropIndicator(tabsHost);
        
        if (TabDragHelper.TryGetData(e.DataTransfer) is TabDragData dragData)
        {
            System.Diagnostics.Debug.WriteLine($"[MainWindow] Tab dropped from {(dragData.SourceWindow != null ? "overflow" : "main")} at index {insertIndex}");
            
            // Видаляємо з вікна-джерела (overflow window)
            if (dragData.SourceWindow != null)
            {
                dragData.SourceWindow.RemoveWorker(dragData.Worker);
            }
            else if (dragData.SourceMainWindow is MainWindow sourceMainWindow)
            {
                // Вкладка з іншого MainWindow або з цього ж
                if (sourceMainWindow == this)
                {
                    // Це вкладка з цього ж вікна - видаляємо стару вкладку повністю
                    var existingTab = FindTabByWorker(dragData.Worker);
                    if (existingTab != null)
                    {
                        tabsHost.Children.Remove(existingTab);
                        _mainPanelTabWorkerMap.Remove(existingTab);
                        System.Diagnostics.Debug.WriteLine($"[MainWindow] Removed existing tab from this main panel: {existingTab.Title}");
                    }
                }
                else
                {
                    // Вкладка з іншого MainWindow - викликаємо видалення в тому вікні
                    sourceMainWindow.RemoveTabFromMainPanel(dragData.Worker);
                    System.Diagnostics.Debug.WriteLine($"[MainWindow] Removed tab from other MainWindow: {dragData.Title}");
                }
            }
            
            // Скидаємо візуальний стан вкладки-джерела (вона більше не потрібна)
            dragData.SourceTab.ResetDragState();
            
            // Додаємо до основної панелі в потрібну позицію (створюємо нову вкладку)
            AddTabToMainPanelAtIndex(dragData.Worker, insertIndex, tabsHost);
            
            System.Diagnostics.Debug.WriteLine($"[MainWindow] Tab added to main panel: {dragData.Title}");
        }
    }
    
    /// <summary>
    /// Обчислює індекс для вставки в основну панель
    /// </summary>
    private int CalculateMainTabsDropIndex(double x, StackPanel tabsHost)
    {
        double currentX = 0;
        int index = 0;
        
        foreach (var child in tabsHost.Children)
        {
            if (child is Tab tab)
            {
                double tabCenter = currentX + (tab.Width + 4) / 2;
                
                if (x < tabCenter)
                {
                    return index;
                }
                
                currentX += tab.Width + 4;
                index++;
            }
        }
        
        return index; // Вставка в кінець
    }
    
    /// <summary>
    /// Показує індикатор місця вставки в основній панелі
    /// </summary>
    private void ShowMainTabsDropIndicator(int index, StackPanel tabsHost)
    {
        if (_mainTabsDropIndicator == null) return;
        
        // Видаляємо індикатор якщо він вже є
        if (tabsHost.Children.Contains(_mainTabsDropIndicator))
        {
            tabsHost.Children.Remove(_mainTabsDropIndicator);
        }
        
        // Обчислюємо позицію для вставки індикатора
        int insertIndex = 0;
        int tabIndex = 0;
        
        for (int i = 0; i < tabsHost.Children.Count; i++)
        {
            if (tabsHost.Children[i] is Tab)
            {
                if (tabIndex == index)
                {
                    insertIndex = i;
                    break;
                }
                tabIndex++;
                insertIndex = i + 1;
            }
        }
        
        // Вставляємо індикатор
        _mainTabsDropIndicator.IsVisible = true;
        
        if (insertIndex >= tabsHost.Children.Count)
        {
            tabsHost.Children.Add(_mainTabsDropIndicator);
        }
        else
        {
            tabsHost.Children.Insert(insertIndex, _mainTabsDropIndicator);
        }
    }
    
    /// <summary>
    /// Приховує індикатор місця вставки в основній панелі
    /// </summary>
    private void HideMainTabsDropIndicator(StackPanel tabsHost)
    {
        if (_mainTabsDropIndicator != null)
        {
            _mainTabsDropIndicator.IsVisible = false;
            tabsHost.Children.Remove(_mainTabsDropIndicator);
        }
        _mainTabsDropTargetIndex = -1;
    }
    
    /// <summary>
    /// Додає вкладку до основної панелі в конкретну позицію
    /// </summary>
    private void AddTabToMainPanelAtIndex(TabWorker worker, int index, StackPanel tabsHost)
    {
        var tab = new Tab
        {
            Title = worker.Title ?? "New Tab",
            IsActive = worker.IsActive,
            IsCloseButtonVisible = true,
            IsMuted = worker.IsMuted,
            Width = _tabWidth
        };

        tab.Clicked += (_, __) => ActivateWorker(worker);
        tab.CloseRequested += (_, __) =>
        {
            tabsHost.Children.Remove(tab);
            _mainPanelTabWorkerMap.Remove(tab);
            _tabs.Close(worker);
        };
        tab.MuteToggled += async (_, __) =>
        {
            await HandleMuteToggle(worker, tab, tabsHost);
        };
        
        // Підтримка перетягування (правою кнопкою миші)
        tab.DragStarted += (_, args) =>
        {
            if (args is TabDragStartedEventArgs dragArgs)
            {
                StartMainTabDrag(tab, worker, dragArgs.PointerEvent);
            }
        };

        // Обчислюємо позицію для вставки
        int actualIndex = 0;
        int tabIndex = 0;
        
        for (int i = 0; i < tabsHost.Children.Count; i++)
        {
            if (tabsHost.Children[i] is Tab)
            {
                if (tabIndex == index)
                {
                    actualIndex = i;
                    break;
                }
                tabIndex++;
                actualIndex = i + 1;
            }
        }
        
        // Вставляємо вкладку
        if (actualIndex >= tabsHost.Children.Count)
        {
            tabsHost.Children.Add(tab);
        }
        else
        {
            tabsHost.Children.Insert(actualIndex, tab);
        }
        
        _mainPanelTabWorkerMap[tab] = worker;
        
        // Оновлює favicon для нової вкладки
        if (!string.IsNullOrEmpty(worker.Address))
        {
            _ = UpdateFaviconForTab(worker, tab);
        }

        // Іконка Vetale Search ставиться одразу, щоб була завжди
        if (IsVetaleSearchPageUrl(worker.Address))
        {
            var icon = GetVetaleSearchIcon();
            if (icon != null) tab.FaviconSource = icon;
        }
    }
    
    /// <summary>
    /// Оновлює favicon для конкретної вкладки
    /// </summary>
    private async Task UpdateFaviconForTab(TabWorker worker, Tab tab)
    {
        try
        {
            var url = worker.Address;
            if (string.IsNullOrEmpty(url)) return;

            // Сторінки Vetale Search завжди мають свою іконку
            if (IsVetaleSearchPageUrl(url))
            {
                var icon = GetVetaleSearchIcon();
                if (icon != null) tab.FaviconSource = icon;
                return;
            }
            
            if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                var favicon = await _faviconService.GetFaviconAsync(uri);
                if (favicon != null && string.Equals(worker.Address, url, StringComparison.OrdinalIgnoreCase))
                {
                    tab.FaviconSource = favicon;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MainWindow] UpdateFaviconForTab error: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Запускає перетягування вкладки з основної панелі
    /// </summary>
    private async void StartMainTabDrag(Tab tab, TabWorker worker, PointerPressedEventArgs pointerEvent)
    {
        var dragData = new TabDragData
        {
            Worker = worker,
            SourceTab = tab,
            SourceWindow = null, // null означає що вкладка з основної панелі MainWindow
            SourceMainWindow = this, // посилання на це MainWindow
            Title = tab.Title,
            Favicon = tab.FaviconSource,
            IsMuted = tab.IsMuted
        };

        using var dataTransfer = TabDragHelper.CreateTransfer(dragData);

        System.Diagnostics.Debug.WriteLine($"[MainWindow] Starting drag for main tab: {tab.Title}");

        try
        {
            var result = await DragDrop.DoDragDropAsync(pointerEvent, dataTransfer, DragDropEffects.Move);
            System.Diagnostics.Debug.WriteLine($"[MainWindow] Drag result: {result}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MainWindow] Drag error: {ex.Message}");
        }
        finally
        {
            // Скидаємо візуальний стан вкладки після завершення drag
            tab.ResetDragState();
        }
    }
    
    #endregion

    /// <summary>
    /// Обробляє toggle mute для вкладки
    /// </summary>
    private Task HandleMuteToggle(TabWorker worker, Tab tab, StackPanel tabsHost)
    {
        try
        {
            // TabWorker is the single source of truth. It applies the state
            // through the browser audio API and its page-level fallback.
            worker.ToggleMute();

            tab.IsMuted = worker.IsMuted;

            // Re-sync по мапі worker->Tab: індексна синхронізація ставила мут не на ті вкладки
            // (звідси "конфлікт іконок" — іконка муту світилась не на своїй вкладці).
            foreach (var w in _tabs.Workers)
            {
                var t2 = FindTabByWorker(w);
                if (t2 != null)
                    t2.IsMuted = w.IsMuted;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MainWindow] Mute toggle failed: {ex.Message}");
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Додає вкладку до overflow вікна
    /// </summary>
    private void AddTabToOverflow(TabWorker worker)
    {
        // Знаходимо overflow вікно з місцем або створюємо нове
        var overflowWindow = FindOrCreateOverflowWindow();
        
        if (!overflowWindow.AddTab(worker))
        {
            System.Diagnostics.Debug.WriteLine("[MainWindow] Failed to add tab to overflow window");
            return;
        }
        
        System.Diagnostics.Debug.WriteLine($"[MainWindow] Tab added to overflow window #{_tabOverflowWindows.IndexOf(overflowWindow) + 1}. Total overflow windows: {_tabOverflowWindows.Count}");
    }

    /// <summary>
    /// Знаходить overflow вікно з місцем або створює нове
    /// </summary>
    private TabOverflowWindow FindOrCreateOverflowWindow()
    {
        // Шукаємо існуюче вікно з місцем
        foreach (var window in _tabOverflowWindows)
        {
            if (window.TabCount < MaxTabsPerOverflowWindow)
            {
                return window;
            }
        }
        
        // Створюємо нове overflow вікно
        var newWindow = new TabOverflowWindow();
        newWindow.Initialize(this, _tabWidth);
        
        newWindow.TabCloseRequested += OnOverflowTabCloseRequested;
        newWindow.TabActivated += OnOverflowTabActivated;
        newWindow.CloseAllTabsRequested += OnOverflowCloseAllRequested;
        newWindow.BecameEmpty += OnOverflowBecameEmpty;
        newWindow.TabRemovedFromMainPanel += OnTabRemovedFromMainPanel;
        newWindow.TabRemovedFromMainPanelWithSource += OnTabRemovedFromMainPanelWithSource;
        
        _tabOverflowWindows.Add(newWindow);
        
        // Позиціонуємо нове вікно нижче попереднього
        PositionOverflowWindows();
        
        System.Diagnostics.Debug.WriteLine($"[MainWindow] Created new overflow window #{_tabOverflowWindows.Count}");
        
        return newWindow;
    }

    /// <summary>
    /// Позиціонує overflow вікна один під одним
    /// </summary>
    private void PositionOverflowWindows()
    {
        int yOffset = 50; // Початкове зміщення від верху головного вікна
        int windowHeight = 60; // Висота одного overflow вікна + відступ
        
        for (int i = 0; i < _tabOverflowWindows.Count; i++)
        {
            var window = _tabOverflowWindows[i];
            if (window.IsVisible)
            {
                try
                {
                    var parentPos = this.Position;
                    int x = parentPos.X + 10;
                    int y = parentPos.Y + yOffset + (i * windowHeight);
                    window.Position = new PixelPoint(x, y);
                }
                catch { }
            }
        }
    }

    private void OnOverflowTabCloseRequested(object? sender, TabWorker worker)
    {
        _tabs.Close(worker);
    }

    private void OnOverflowTabActivated(object? sender, TabWorker worker)
    {
        ActivateWorker(worker);
        
        // Встановлюємо активну вкладку у всіх overflow вікнах
        foreach (var window in _tabOverflowWindows)
        {
            window.SetActiveTab(worker);
        }
    }
    
    /// <summary>
    /// Обробник коли вкладка переноситься з основної панелі до overflow вікна
    /// </summary>
    private void OnTabRemovedFromMainPanel(object? sender, TabWorker worker)
    {
        var tabsHost = _normalModePage?.TabsHostPanel;
        if (tabsHost == null) return;
        
        // Знаходимо і видаляємо вкладку з основної панелі
        var existingTab = FindTabByWorker(worker);
        if (existingTab != null)
        {
            tabsHost.Children.Remove(existingTab);
            _mainPanelTabWorkerMap.Remove(existingTab);
            System.Diagnostics.Debug.WriteLine($"[MainWindow] Tab removed from main panel via OnTabRemovedFromMainPanel: {existingTab.Title}");
        }
    }
    
    /// <summary>
    /// Обробник коли вкладка переноситься з основної панелі іншого MainWindow до overflow вікна
    /// </summary>
    private void OnTabRemovedFromMainPanelWithSource(object? sender, (TabWorker Worker, object SourceMainWindow) args)
    {
        // Викликаємо видалення в правильному MainWindow-джерелі
        if (args.SourceMainWindow is MainWindow sourceMainWindow)
        {
            sourceMainWindow.RemoveTabFromMainPanel(args.Worker);
            System.Diagnostics.Debug.WriteLine($"[MainWindow] Tab removed from source MainWindow via OnTabRemovedFromMainPanelWithSource");
        }
    }

    private void OnOverflowCloseAllRequested(object? sender, EventArgs e)
    {
        if (sender is not TabOverflowWindow overflowWindow) return;
        
        // Закриваємо всі workers в цьому overflow вікні та робимо dispose
        var workers = overflowWindow.GetAllWorkers().ToList();
        foreach (var worker in workers)
        {
            // Dispose worker та закриваємо вкладку
            try
            {
                worker.Dispose();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MainWindow] Worker dispose error: {ex.Message}");
            }
            _tabs.Close(worker);
        }
        
        // Видаляємо вікно зі списку
        _tabOverflowWindows.Remove(overflowWindow);
        
        // Відписуємося від подій
        overflowWindow.TabCloseRequested -= OnOverflowTabCloseRequested;
        overflowWindow.TabActivated -= OnOverflowTabActivated;
        overflowWindow.CloseAllTabsRequested -= OnOverflowCloseAllRequested;
        overflowWindow.BecameEmpty -= OnOverflowBecameEmpty;
        overflowWindow.TabRemovedFromMainPanel -= OnTabRemovedFromMainPanel;
        overflowWindow.TabRemovedFromMainPanelWithSource -= OnTabRemovedFromMainPanelWithSource;
        
        overflowWindow.Close();
        
        // Перепозиціонуємо решту вікон
        PositionOverflowWindows();
        
        System.Diagnostics.Debug.WriteLine($"[MainWindow] Overflow window closed and disposed. Remaining: {_tabOverflowWindows.Count}");
    }

    private void OnOverflowBecameEmpty(object? sender, EventArgs e)
    {
        if (sender is not TabOverflowWindow overflowWindow) return;
        
        // Видаляємо порожнє overflow вікно
        _tabOverflowWindows.Remove(overflowWindow);
        
        // Відписуємося від подій
        overflowWindow.TabCloseRequested -= OnOverflowTabCloseRequested;
        overflowWindow.TabActivated -= OnOverflowTabActivated;
        overflowWindow.CloseAllTabsRequested -= OnOverflowCloseAllRequested;
        overflowWindow.BecameEmpty -= OnOverflowBecameEmpty;
        overflowWindow.TabRemovedFromMainPanel -= OnTabRemovedFromMainPanel;
        overflowWindow.TabRemovedFromMainPanelWithSource -= OnTabRemovedFromMainPanelWithSource;
        
        overflowWindow.Close();
        
        // Перепозиціонуємо решту вікон
        PositionOverflowWindows();
        
        System.Diagnostics.Debug.WriteLine($"[MainWindow] Empty overflow window removed. Remaining: {_tabOverflowWindows.Count}");
    }
    
    /// <summary>
    /// Реорганізовує вкладки при зміні режиму (звичайний/fullscreen)
    /// Переміщує вкладки між основною панеллю та overflow відповідно до нового ліміту
    /// </summary>
    private void ReorganizeTabsForMode()
    {
        var tabsHost = _normalModePage?.TabsHostPanel;
        if (tabsHost == null) return;
        
        int currentTabCount = GetMainPanelTabCount();
        int maxTabs = CurrentMaxTabs;
        
        System.Diagnostics.Debug.WriteLine($"[MainWindow] ReorganizeTabsForMode: currentCount={currentTabCount}, maxTabs={maxTabs}, isFullscreen={_isFullscreen}");
        
        // Якщо вкладок більше ніж дозволено - переміщуємо зайві в overflow
        while (currentTabCount > maxTabs)
        {
            // Знаходимо останню вкладку в основній панелі
            Tab? lastTab = null;
            TabWorker? lastWorker = null;
            
            for (int i = tabsHost.Children.Count - 1; i >= 0; i--)
            {
                if (tabsHost.Children[i] is Tab tab)
                {
                    lastTab = tab;
                    // Знаходимо відповідний worker через мапу
                    if (_mainPanelTabWorkerMap.TryGetValue(tab, out var worker))
                    {
                        lastWorker = worker;
                    }
                    break;
                }
            }
            
            if (lastTab != null && lastWorker != null)
            {
                // Зберігаємо title та favicon перед переміщенням
                var savedTitle = lastTab.Title;
                var savedFavicon = lastTab.FaviconSource;
                var savedIsMuted = lastTab.IsMuted;
                
                tabsHost.Children.Remove(lastTab);
                _mainPanelTabWorkerMap.Remove(lastTab);
                AddTabToOverflow(lastWorker);
                
                // Оновлюємо title та favicon в overflow вкладці
                UpdateOverflowTabData(lastWorker, savedTitle ?? "New Tab", savedFavicon);
                
                currentTabCount--;
            }
            else
            {
                break;
            }
        }
        
        // Якщо є місце в основній панелі і є вкладки в overflow - повертаємо їх
        while (currentTabCount < maxTabs && GetTotalOverflowTabCount() > 0)
        {
            // Знаходимо перше overflow вікно з вкладками
            var firstWindowWithTabs = _tabOverflowWindows.FirstOrDefault(w => w.TabCount > 0);
            if (firstWindowWithTabs == null) break;
            
            var overflowWorkers = firstWindowWithTabs.GetAllWorkers().ToList();
            if (overflowWorkers.Count > 0)
            {
                var workerToMove = overflowWorkers[0];
                
                // Отримуємо дані з overflow Tab перед видаленням
                var overflowTab = firstWindowWithTabs.GetTabByWorker(workerToMove);
                var savedTitle = overflowTab?.Title ?? workerToMove.Title ?? "New Tab";
                var savedFavicon = overflowTab?.FaviconSource;
                var savedIsMuted = overflowTab?.IsMuted ?? workerToMove.IsMuted;
                
                // Видаляємо worker з overflow
                firstWindowWithTabs.RemoveWorker(workerToMove);
                
                // Створюємо нову вкладку в основній панелі
                var newTab = CreateTabForWorker(workerToMove, tabsHost);
                
                // Встановлюємо збережені дані
                newTab.Title = savedTitle;
                newTab.FaviconSource = savedFavicon;
                newTab.IsMuted = savedIsMuted;
                
                tabsHost.Children.Add(newTab);
                
                currentTabCount++;
            }
            else
            {
                break;
            }
        }
        
        System.Diagnostics.Debug.WriteLine($"[MainWindow] ReorganizeTabsForMode complete: mainCount={GetMainPanelTabCount()}, overflowCount={GetTotalOverflowTabCount()}");
    }

    /// <summary>
    /// Оновлює дані вкладки в overflow вікнах
    /// </summary>
    private void UpdateOverflowTabData(TabWorker worker, string title, Avalonia.Media.IImage? favicon)
    {
        foreach (var window in _tabOverflowWindows)
        {
            if (window.GetTabByWorker(worker) != null)
            {
                window.UpdateTabTitle(worker, title);
                window.UpdateTabFavicon(worker, favicon);
                break;
            }
        }
    }

    /// <summary>
    /// Отримує загальну кількість вкладок у всіх overflow вікнах
    /// </summary>
    private int GetTotalOverflowTabCount()
    {
        return _tabOverflowWindows.Sum(w => w.TabCount);
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

        // Update tabs active state and titles — СТРОГО по мапі worker->Tab,
        // а не за індексом: порядок дітей в панелі може не збігатися з порядком воркерів
        // (drag-reorder, overflow-переміщення), інакше ліва вкладка забирає назву/іконку/мут правої.
        foreach (var w in _tabs.Workers)
        {
            var t = FindTabByWorker(w);
            if (t == null) continue;
            t.IsActive = w == worker;
            t.Title = ComputeTitle(w.Title, w.Address);
            // Sync mute state from worker to tab UI
            t.IsMuted = w.IsMuted;
        }

        // Bind navigation bar to active manager
        var navigationBar = _normalModePage?.NavBar;
        if (navigationBar != null)
        {
            navigationBar.SetSuggestionsService(GlobalSuggestions);
            navigationBar.SetSecurityCheckService(SecurityCheckService);

            // Single-shot subscriptions (avoid duplicates on tab switch).
            navigationBar.NavigateRequested -= OnNavigationBarNavigateRequested;
            navigationBar.NavigateRequested += OnNavigationBarNavigateRequested;
            navigationBar.BackRequested -= OnNavBarBackRequested;
            navigationBar.BackRequested += OnNavBarBackRequested;
            navigationBar.ForwardRequested -= OnNavBarForwardRequested;
            navigationBar.ForwardRequested += OnNavBarForwardRequested;
            navigationBar.ReloadRequested -= OnNavBarReloadRequested;
            navigationBar.ReloadRequested += OnNavBarReloadRequested;
            navigationBar.HomeRequested -= OnNavBarHomeRequested;
            navigationBar.HomeRequested += OnNavBarHomeRequested;
            navigationBar.BindWorker(worker);
            
            // Set settings service for search engine configuration
            if (_settingsService != null)
            {
                navigationBar.SetSettingsService(_settingsService);
            }
            
            navigationBar.SetState(worker.Address ?? string.Empty, worker.CanGoBack, worker.CanGoForward);
        }

        WireActiveWebViewPropertyChanged(worker);

        _lastFaviconUrl = worker.Address;
        _lastPageTitle = worker.Title;
        _ = UpdateFaviconAsync(worker.Address, worker);
        _ = UpdateTabTitleAsync(worker.Title, worker.Address, worker);
    }

    /// <summary>
    /// Монтує WebView/сторінку в контейнер як повноцінний елемент:
    /// Stretch-розтягнення, видимість, фокус. Від'єднані нативні в'ю просто
    /// прибираємо з контейнера без IsVisible=false — для windowed CEF
    /// ховання руйнує HWND і наступний показ дає білий екран.
    /// </summary>
    private static void ShowControlInContainer(Grid target, Control content)
    {
        if (content.Parent is Panel prev && !ReferenceEquals(prev, target))
        {
            prev.Children.Remove(content);
        }

        content.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch;
        content.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch;
        content.IsVisible = true;
        if (!target.Children.Contains(content))
        {
            target.Children.Add(content);
        }

        for (int i = target.Children.Count - 1; i >= 0; i--)
        {
            var c = target.Children[i];
            if (!ReferenceEquals(c, content))
            {
                target.Children.RemoveAt(i);
            }
        }

        try { content.Focus(); } catch { }
    }

    private void MoveActiveWebViewTo(Grid target)
    {
        try
        {
            var active = _tabs.Active;
            if (active == null) return;

            // Визначаємо що показувати за History.CurrentEntry
            var current = active.History.CurrentEntry;
            if (current != null && current.IsInternal && current.InternalPageContent != null)
            {
                ShowControlInContainer(target, current.InternalPageContent);
                System.Diagnostics.Debug.WriteLine("[MainWindow] Internal page (from History) moved to container");
                return;
            }

            var webView = active.WebView;

            ShowControlInContainer(target, webView.View);

            System.Diagnostics.Debug.WriteLine($"[MainWindow] WebView moved to {(ReferenceEquals(target, _fullscreenModePage?.FullscreenGrid) ? "fullscreen" : "normal")} container");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MainWindow] MoveActiveWebViewTo error: {ex.Message}");
        }
    }

    private void WireActiveWebViewPropertyChanged(TabWorker worker)
    {
        foreach (var candidate in _tabs.Workers)
        {
            if (_propertySubscribedWorkers.Add(candidate))
            {
                candidate.WebView.PropertyChanged += WebView_OnPropertyChanged;
                // Commit навігації (головний фрейм довантажився) — єдина точка запису історії,
                // як у професійних браузерів: пишемо факт візиту, а не кожну зміну адреси.
                if (candidate.WebView is VetaleBrowser.Core.Scripts.Browser.CefSharpAdapter adapter)
                    adapter.FrameLoadEnd += WebView_OnFrameLoadEnd;
            }
        }

        if (_subscribedWorker != null)
        {
            try
            {
                _subscribedWorker.FullscreenChanged -= OnWorkerFullscreenChanged;
                _subscribedWorker.WebView.KeyDown -= OnWebViewKeyDown;
            }
            catch { }
        }
        _subscribedWorker = worker;
        _subscribedWorker.FullscreenChanged += OnWorkerFullscreenChanged;
        _subscribedWorker.WebView.KeyDown += OnWebViewKeyDown;
    }

    private void OnWebViewKeyDown(object? sender, KeyEventArgs e)
    {
        if (e == null) return;
        var isAlt = (e.KeyModifiers & KeyModifiers.Alt) == KeyModifiers.Alt;

        // Якщо це внутрішня сторінка з результатами пошуку Vetale, ігноруємо F/F11
        if (IsCurrentInternalSearchResultsPage())
        {
            if (e.Key == Key.F || e.Key == Key.F11 || (e.Key == Key.F && isAlt))
            {
                // Не чіпаємо fullscreen гарячі клавіші на сторінці результатів пошуку
                return;
            }
        }

        // Якщо зараз показується Vetale Search (home або results), блокуємо F/F11
        if (IsCurrentVetaleSearchInternalPage())
        {
            if (e.Key == Key.F11 || e.Key == Key.F || (e.Key == Key.F && (e.KeyModifiers & KeyModifiers.Alt) == KeyModifiers.Alt))
            {
                return;
            }
        }

        if (e.Key == Key.F11)
        {
            ToggleFullscreen();
            e.Handled = true;
            return;
        }

        if ((e.Key == Key.F || e.Key == Key.F12) && _isFullscreen && !_videoFullscreen)
        {
            ExitFullscreen();
            TryExitDocumentFullscreen();
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
            // F belongs to the video page (for example YouTube). Let the page
            // toggle document fullscreen and react to its state change below.
            return;
        }

        if (e.Key == Key.Escape && _isFullscreen)
        {
            ExitFullscreen();
            TryExitDocumentFullscreen();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape && _videoFullscreen)
        {
            // Сторінка сама вийде з document fullscreen; повертаємо хром вікна
            ExitVideoFullscreen();
            e.Handled = true;
            return;
        }
    }

    /// <summary>
    /// Обробляє зміни властивостей головного вікна (зокрема WindowState)
    /// </summary>
    private void OnMainWindowPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property.Name == nameof(WindowState))
        {
            System.Diagnostics.Debug.WriteLine($"[MainWindow] WindowState changed: {e.OldValue} -> {e.NewValue}, CurrentMaxTabs={CurrentMaxTabs}");
            // Реорганізуємо вкладки при зміні стану вікна (maximized/normal)
            ReorganizeTabsForMode();
        }
    }

    private bool IsCurrentInternalSearchResultsPage()
    {
        try
        {
            var entry = _tabs.Active?.History.CurrentEntry;
            if (entry == null) return false;
            if (entry.IsInternal)
            {
                // Перевіряємо тип або URL
                if (entry.InternalPageContent is VetaleSearchResultsPage) return true;
                var url = entry.Url ?? string.Empty;
                if (url.StartsWith("vetale://search?", StringComparison.OrdinalIgnoreCase)) return true;
            }
        }
        catch { }
        return false;
    }

    private bool IsCurrentVetaleSearchInternalPage(TabWorker? worker = null)
    {
        try
        {
            var entry = (worker ?? _tabs.Active)?.History.CurrentEntry;
            if (entry == null) return false;
            if (entry.IsInternal)
            {
                if (entry.InternalPageContent is VetaleSearchHomePage || entry.InternalPageContent is VetaleSearchResultsPage)
                    return true;
                var url = entry.Url ?? string.Empty;
                if (url.Equals("vetale://search", StringComparison.OrdinalIgnoreCase) || url.StartsWith("vetale://search?", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }
        catch { }
        return false;
    }

    // React to WebView property changes (e.g., Address changes, CanGoBack/Forward, Title)
    private async void WebView_OnPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        try
        {
            var worker = _tabs.Workers.FirstOrDefault(w => ReferenceEquals(sender, w.WebView));
            if (worker == null) return;
            var vw = worker.WebView;

            var prop = e.Property?.Name;
            if (prop is "Address" or "Url")
            {
                var url = worker.Manager.GetCurrentUrl();
                if (ReferenceEquals(_tabs.Active, worker))
                {
                    _lastFaviconUrl = url;
                    Dispatcher.UIThread.Post(() =>
                    {
                        var nav = _normalModePage?.NavBar;
                        if (nav != null)
                        {
                            nav.Url = url ?? string.Empty;
                            nav.CanGoBack = worker.CanGoBack;
                            nav.CanGoForward = worker.CanGoForward;
                        }
                    });
                }

                await UpdateFaviconAsync(url, worker);
                await UpdateTabTitleAsync(null, url, worker);
                // Історію тут НЕ пишемо: AddressChanged — це початок навігації/набір адреси,
                // а візит фіксується на commit (FrameLoadEnd), як у Chrome/Firefox.
            }
            else if (prop == "CanGoBack" || prop == "CanGoForward")
            {
                Dispatcher.UIThread.Post(() =>
                {
                    var nav = _normalModePage?.NavBar;
                    if (nav != null && _tabs.Active != null)
                    {
                        nav.CanGoBack = _tabs.Active.CanGoBack;
                        nav.CanGoForward = _tabs.Active.CanGoForward;
                    }
                });
            }
            else if (prop == "Title")
            {
                // Direct access to Title property
                var pageTitle = vw.Title;
                if (ReferenceEquals(_tabs.Active, worker))
                    _lastPageTitle = pageTitle ?? _lastPageTitle;
                await UpdateTabTitleAsync(pageTitle, vw.Address, worker);
                // Титул прийшов пізніше коміта — дозбагачуємо запис історії.
                if (!string.IsNullOrWhiteSpace(pageTitle) && !string.IsNullOrWhiteSpace(vw.Address))
                    UpsertHistory(worker, vw.Address, pageTitle);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MainWindow] WebView_OnPropertyChanged error: {ex.Message}");
        }
    }

    /// <summary>
    /// Commit навігації як у професійних браузерів: головний фрейм довантажився —
    /// це факт візиту. Тут єдина точка створення запису історії; title/favicon
    /// дозбагачують його пізніше через upsert (AddOrUpdateHistoryItem зливає по URL).
    /// Внутрішні vetale://-сторінки сюди не потрапляють — вони не вантажаться в рушій.
    /// </summary>
    private void WebView_OnFrameLoadEnd(object? sender, string? url)
    {
        try
        {
            var worker = _tabs.Workers.FirstOrDefault(w => ReferenceEquals(sender, w.WebView));
            if (worker == null || string.IsNullOrWhiteSpace(url)) return;
            if (url.StartsWith("about:", StringComparison.OrdinalIgnoreCase)) return;

            var title = worker.Title;
            try { title ??= worker.WebView?.Title; } catch { }
            UpsertHistory(worker, url, title ?? url);

            _ = UpdateFaviconAsync(url, worker);
            _ = UpdateTabTitleAsync(worker.Title, url, worker);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MainWindow] WebView_OnFrameLoadEnd error: {ex.Message}");
        }
    }

    /// <summary>Upsert запису історії (створення або прогресивне збагачення title/favicon).</summary>
    private void UpsertHistory(TabWorker worker, string? url, string? title = null)
    {
        try
        {
            if (worker == null || string.IsNullOrWhiteSpace(url)) return;
            DatabaseManager.HistoryInstance.AddOrUpdateHistoryItem(
                url: url,
                title: title ?? worker.Title ?? worker.WebView?.Title ?? url,
                faviconUrl: worker.FaviconUrl,
                faviconData: worker.FaviconData
            );
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MainWindow] Error adding to history: {ex.Message}");
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
            await active.WebView.EvaluateScriptAsync<object>(js);
        }
        catch
        {
        }
    }

    // Handle fullscreen requests from web content (e.g., YouTube videos)
    private void OnWorkerFullscreenChanged(object? sender, bool isFullscreen)
    {
        Dispatcher.UIThread.Post(() =>
        {
            // Контентний фулскрін іде на окрему сторінку тільки з вебв'ю
            if (isFullscreen) EnterVideoFullscreen(); else ExitVideoFullscreen();
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
    private static string? TryGetWebViewTitle(IBrowserView vw)
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

    private async Task UpdateFaviconAsync(string? address, TabWorker? targetWorker = null)
    {
        try
        {
            var worker = targetWorker ?? _tabs.Active;
            if (worker == null) return;
            if (string.IsNullOrWhiteSpace(address))
            {
                ApplyFaviconToTab(worker, null);
                return;
            }

            var requestVersion = NextFaviconRequestVersion(worker);
            
            // Перевіряємо чи це Vetale Search (vetale:// або vetale:)
            var isVetaleSearch = address.StartsWith("vetale://", StringComparison.OrdinalIgnoreCase) ||
                                 address.StartsWith("vetale:", StringComparison.OrdinalIgnoreCase);
            
            // Також перевіряємо чи поточна вкладка показує внутрішню Vetale Search сторінку
            if (!isVetaleSearch && IsCurrentVetaleSearchInternalPage(worker))
            {
                isVetaleSearch = true;
                System.Diagnostics.Debug.WriteLine($"[MainWindow] Current tab shows internal Vetale Search page");
            }
            
            if (isVetaleSearch)
            {
                // Завантажуємо іконку Vetale Search
                System.Diagnostics.Debug.WriteLine($"[MainWindow] Detected Vetale URL: {address}, loading Vetale Search icon");
                await LoadVetaleSearchIconAsync(worker);
                return;
            }

            if (!Uri.TryCreate(address, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                ApplyFaviconToTab(worker, null);
                return;
            }

            // Clear the previous site's icon immediately. This prevents a
            // favicon from the old page being shown while the new one loads.
            ApplyFaviconToTab(worker, null);

            // Determine scale for better icon size
            var visualRoot = TopLevel.GetTopLevel(this);
            double scale = 1.0;
            if (visualRoot is TopLevel top)
            {
                scale = top.RenderScaling;
            }

            var size = scale >= 1.5 ? 48 : 32;
            System.Diagnostics.Debug.WriteLine($"[MainWindow] UpdateFavicon: url={uri} size={size} scale={scale:0.00}");

            // Перевіряємо, чи це гра HexGL (localhost)
            var isHexGlGame = address.StartsWith("http://localhost:") && 
                              (address.Contains("index.html") || address.EndsWith("/"));
            
            IImage? image = null;
            if (isHexGlGame && LocalGameServer.Instance.IsRunning)
            {
                // Завантажуємо іконку гри напряму з локального сервера
                var gameIconUrl = LocalGameServer.Instance.GameIconUrl;
                System.Diagnostics.Debug.WriteLine($"[MainWindow] Loading HexGL game icon directly: {gameIconUrl}");
                
                try
                {
                    using var httpClient = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(5) };
                    var iconBytes = await httpClient.GetByteArrayAsync(gameIconUrl);
                    
                    if (iconBytes != null && iconBytes.Length > 0)
                    {
                        using var stream = new MemoryStream(iconBytes);
                        image = new Avalonia.Media.Imaging.Bitmap(stream);
                        System.Diagnostics.Debug.WriteLine($"[MainWindow] HexGL icon loaded: {iconBytes.Length} bytes");
                    }
                }
                catch (Exception iconEx)
                {
                    System.Diagnostics.Debug.WriteLine($"[MainWindow] Failed to load HexGL icon: {iconEx.Message}");
                }
            }
            else
            {
                // PRO: сначала пробуем живую иконку страницы (<link rel=icon>), потом провайдеры
                if (!string.IsNullOrWhiteSpace(worker.FaviconUrl))
                {
                    await ApplyPageIconAsync(worker, worker.FaviconUrl);
                    // Если страница отдала иконку — дотягиваем фоном через сервис только если таб пуст
                    var tabNow = FindTabByWorker(worker);
                    if (tabNow?.FaviconSource != null) return;
                }
                // Стандартний favicon
                image = await _faviconService.GetFaviconAsync(uri, size);
            }
            
            Dispatcher.UIThread.Post(() =>
            {
                if (worker == null ||
                    !IsCurrentFaviconRequest(worker, requestVersion) ||
                    !string.Equals(worker.Address, address, StringComparison.OrdinalIgnoreCase))
                    return;
                ApplyFaviconToTab(worker, image);
            });
        }
        catch (Exception ex)
        {
            // ignore favicon failures
            System.Diagnostics.Debug.WriteLine($"[MainWindow] UpdateFavicon error: {ex.Message}");
        }
    }

    /// <summary>
    /// Завантажує іконку Vetale Search для вкладки
    /// </summary>
    private Task LoadVetaleSearchIconAsync(TabWorker? targetWorker = null)
    {
        try
        {
            var image = GetVetaleSearchIcon();
            var worker = targetWorker ?? _tabs.Active;
            if (worker == null) return Task.CompletedTask;
            System.Diagnostics.Debug.WriteLine($"[MainWindow] Final image state: {(image != null ? "LOADED" : "NULL")}");
            
            // Застосовуємо іконку до вкладки
            Dispatcher.UIThread.Post(() =>
            {
                // Шукаємо вкладку в основній панелі по worker
                var mainPanelTab = FindTabByWorker(worker);
                if (mainPanelTab != null)
                {
                    mainPanelTab.FaviconSource = image;
                    System.Diagnostics.Debug.WriteLine($"[MainWindow] Vetale Search icon APPLIED to main panel tab: hasImage={(image != null)}");
                    return;
                }
                
                // Якщо не знайшли в основній панелі - перевіряємо overflow вікна
                foreach (var overflowWindow in _tabOverflowWindows)
                {
                    if (overflowWindow.GetTabByWorker(worker) != null)
                    {
                        overflowWindow.UpdateTabFavicon(worker, image);
                        System.Diagnostics.Debug.WriteLine($"[MainWindow] Vetale Search icon APPLIED to overflow: hasImage={(image != null)}");
                        break;
                    }
                }
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MainWindow] LoadVetaleSearchIcon error: {ex.Message}");
        }
        
        return Task.CompletedTask;
    }

    // Compute and apply a friendly tab title from the page title or URL
    private Task UpdateTabTitleAsync(string? pageTitle, string? url, TabWorker? targetWorker = null)
    {
        try
        {
            var worker = targetWorker ?? _tabs.Active;
            if (worker == null) return Task.CompletedTask;
            // Пропускаємо оновлення title для Vetale Search сторінок (у них свій title)
            if (IsCurrentVetaleSearchInternalPage(worker))
            {
                System.Diagnostics.Debug.WriteLine("[MainWindow] Skipping title update for Vetale Search internal page");
                return Task.CompletedTask;
            }

            
            var friendly = ComputeTitle(pageTitle, url);
            Dispatcher.UIThread.Post(() =>
            {
                if (!string.IsNullOrWhiteSpace(url) &&
                    !string.Equals(worker.Address, url, StringComparison.OrdinalIgnoreCase))
                    return;

                // Шукаємо вкладку в основній панелі по worker
                var mainPanelTab = FindTabByWorker(worker);
                if (mainPanelTab != null)
                {
                    mainPanelTab.Title = friendly;
                    System.Diagnostics.Debug.WriteLine($"[MainWindow] Title applied to main panel: {friendly}");
                    return;
                }
                
                // Якщо не знайшли в основній панелі - перевіряємо overflow вікна
                foreach (var overflowWindow in _tabOverflowWindows)
                {
                    if (overflowWindow.GetTabByWorker(worker) != null)
                    {
                        overflowWindow.UpdateTabTitle(worker, friendly);
                        System.Diagnostics.Debug.WriteLine($"[MainWindow] Title applied to overflow: {friendly}");
                        break;
                    }
                }
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MainWindow] UpdateTabTitle error: {ex.Message}");
        }
        
        return Task.CompletedTask;
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

    // Кешована іконка Vetale Search (завантажується один раз)
    private static Avalonia.Media.IImage? _vetaleSearchIconCache;
    private static bool _vetaleSearchIconLoadAttempted;

    /// <summary>
    /// Чи є URL сторінкою Vetale Search (головна або результати)
    /// </summary>
    private static bool IsVetaleSearchPageUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return false;
        var pageType = InternalUrlHandler.GetPageType(url);
        return pageType == InternalPageType.VetaleSearch
            || pageType == InternalPageType.VetaleSearchResults;
    }

    /// <summary>
    /// Повертає кешовану іконку Vetale Search (VetaleSearchIcon.png з папки іконок)
    /// </summary>
    private static Avalonia.Media.IImage? GetVetaleSearchIcon()
    {
        if (_vetaleSearchIconLoadAttempted) return _vetaleSearchIconCache;
        _vetaleSearchIconLoadAttempted = true;

        try
        {
            // Спосіб 1: з ресурсів додатку
            if (Application.Current != null
                && Application.Current.TryFindResource("VetaleSearchIconImage", out var resource)
                && resource is Avalonia.Media.IImage img)
            {
                _vetaleSearchIconCache = img;
                return _vetaleSearchIconCache;
            }

            // Спосіб 2: напряму через avares
            try
            {
                var uri = new Uri("avares://VetaleBrowser/VetaleBrowser.UI/Sources/Icons/VetaleSearchIcon.png");
                _vetaleSearchIconCache = new Avalonia.Media.Imaging.Bitmap(Avalonia.Platform.AssetLoader.Open(uri));
                return _vetaleSearchIconCache;
            }
            catch (Exception uriEx)
            {
                System.Diagnostics.Debug.WriteLine($"[MainWindow] Vetale Search icon avares load failed: {uriEx.Message}");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MainWindow] Vetale Search icon load error: {ex.Message}");
        }

        return _vetaleSearchIconCache;
    }

    /// <summary>
    /// Синхронно ставить іконку Vetale Search на вкладку worker'а (головна панель або overflow).
    /// Викликається при створенні вкладки та при кожній навігації, щоб іконка була завжди.
    /// </summary>
    private void ApplyVetaleSearchIconToTab(TabWorker worker)
    {
        try
        {
            var image = GetVetaleSearchIcon();
            if (image == null) return;

            var mainPanelTab = FindTabByWorker(worker);
            if (mainPanelTab != null)
            {
                mainPanelTab.FaviconSource = image;
                return;
            }

            foreach (var overflowWindow in _tabOverflowWindows)
            {
                if (overflowWindow.GetTabByWorker(worker) != null)
                {
                    overflowWindow.UpdateTabFavicon(worker, image);
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MainWindow] ApplyVetaleSearchIconToTab error: {ex.Message}");
        }
    }

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        // Видаляємо це вікно зі статичного списку
        _allMainWindows.Remove(this);
        
        foreach (var worker in _propertySubscribedWorkers)
        {
            try { worker.WebView.PropertyChanged -= WebView_OnPropertyChanged; } catch { }
        }
        _propertySubscribedWorkers.Clear();
        _subscribedWorker = null;

        // Закриваємо всі overflow вікна та dispose workers
        try 
        { 
            foreach (var overflowWindow in _tabOverflowWindows.ToList())
            {
                // Dispose всіх workers в цьому overflow вікні
                var workers = overflowWindow.GetAllWorkers().ToList();
                foreach (var worker in workers)
                {
                    try { worker.Dispose(); } catch { }
                }
                
                overflowWindow.TabCloseRequested -= OnOverflowTabCloseRequested;
                overflowWindow.TabActivated -= OnOverflowTabActivated;
                overflowWindow.CloseAllTabsRequested -= OnOverflowCloseAllRequested;
                overflowWindow.BecameEmpty -= OnOverflowBecameEmpty;
                overflowWindow.TabRemovedFromMainPanel -= OnTabRemovedFromMainPanel;
                overflowWindow.TabRemovedFromMainPanelWithSource -= OnTabRemovedFromMainPanelWithSource;
                
                overflowWindow.Close();
            }
            _tabOverflowWindows.Clear();
        } 
        catch { }

        _tabs.Dispose();
        if (_faviconService is IDisposable d) d.Dispose();
        _faviconPollTimer.Stop();
        try { _videoFullscreenWatcher?.Dispose(); } catch { }
        _videoFullscreenWatcher = null;
        _videoFullscreen = false;
        
        // Зупиняємо локальні сервери (гра + ігровий двигун)
        try { LocalGameServer.Instance.Dispose(); } catch { }
        try { MicroStudioServer.Instance.Dispose(); } catch { }
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
        // Якщо зараз показується Vetale Search (home або results), блокуємо F/F11
        if (IsCurrentVetaleSearchInternalPage())
        {
            if (e.Key == Key.F11 || e.Key == Key.F || (e.Key == Key.F && (e.KeyModifiers & KeyModifiers.Alt) == KeyModifiers.Alt))
            {
                return;
            }
        }

        // F11 toggles fullscreen
        if (e.Key == Key.F11)
        {
            if (_isFullscreen)
                ExitFullscreen();
            else
                _windowManager?.ToggleMaximize();
            e.Handled = true;
        }
        else if ((e.Key == Key.F || e.Key == Key.F12) && _isFullscreen && !_videoFullscreen)
        {
            ExitFullscreen();
            TryExitDocumentFullscreen();
            e.Handled = true;
        }
        // Alt+F forces single-WebView fullscreen (global shortcut)
        else if (e.Key == Key.F && (e.KeyModifiers & KeyModifiers.Alt) == KeyModifiers.Alt)
        {
            EnterFullscreen();
            e.Handled = true;
        }
        // F also toggles fullscreen (for video playback)
        else if (e.Key == Key.F)
        {
            // F is owned by the active page, not by the browser window.
            return;
        }
        // ESC exits fullscreen
        else if (e.Key == Key.Escape && _isFullscreen)
        {
            ExitFullscreen();
            // Also request the page to exit document fullscreen if it was set
            TryExitDocumentFullscreen();
            e.Handled = true;
        }
    }

    public void EnterFullscreen()
    {
        if (_isFullscreen) return;
        // Під час HTML5-фулскріна відео WebView чіпати не можна (відрив поверхні)
        if (_videoFullscreen) return;

        _isFullscreen = true;
        _preFullscreenWindowState = WindowState;
        _preFullscreenDecorations = WindowDecorations;

        // Remove window padding
        Padding = new Thickness(0);
        if (_normalModePage?.TabBar != null) _normalModePage.TabBar.IsVisible = false;
        if (_normalModePage?.NavBarRow != null) _normalModePage.NavBarRow.IsVisible = false;

        // Keep the WebView in its existing native host to avoid a first-frame
        // renderer reset when F11 is pressed over a page.
        WindowState = WindowState.FullScreen;
        WindowDecorations = Avalonia.Controls.WindowDecorations.None;
    }

    public void ExitFullscreen()
    {
        if (!_isFullscreen) return;

        _isFullscreen = false;

        // Restore padding
        Padding = new Thickness(8);
        if (_normalModePage?.TabBar != null) _normalModePage.TabBar.IsVisible = true;
        if (_normalModePage?.NavBarRow != null) _normalModePage.NavBarRow.IsVisible = true;

        // Exit fullscreen mode
        WindowState = _preFullscreenWindowState;
        WindowDecorations = _preFullscreenDecorations;
        ReorganizeTabsForMode();

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

    private IntPtr GetMainWindowHandle()
    {
        try { return TryGetPlatformHandle()?.Handle ?? IntPtr.Zero; }
        catch { return IntPtr.Zero; }
    }

    private void OnVideoFullscreenChanged(object? sender, bool isFullscreen)
    {
        try
        {
            Dispatcher.UIThread.Post(() =>
            {
                try
                {
                    if (isFullscreen) EnterVideoFullscreen();
                    else ExitVideoFullscreen();
                }
                catch
                {
                }
            });
        }
        catch { }
    }

    /// <summary>
    /// Відео-фулскрін: ПРОСТО максимайз + F-механізм на місці.
    /// Жодних окремих вікон і пересадок WebView — тільки ховаємо панелі,
    /// вікно як кнопка максимайз. Працює і для кнопки ⛶, і для F на YouTube.
    /// </summary>
    private void EnterVideoFullscreen()
    {
        if (_videoFullscreen || _isFullscreen) return;
        if (_normalModePage?.WebViewGrid == null) return;
        _videoFullscreen = true;
        _preVideoFullscreenWindowState = WindowState;
        _preVideoFullscreenDecorations = WindowDecorations;

        try
        {
            if (_normalModePage?.TabBar != null) _normalModePage.TabBar.IsVisible = false;
            if (_normalModePage?.NavBarRow != null) _normalModePage.NavBarRow.IsVisible = false;

            if (_normalModePage.WebViewGrid.Parent is Grid root)
            {
                if (root.RowDefinitions.Count > 0) root.RowDefinitions[0].Height = new GridLength(0);
                if (root.RowDefinitions.Count > 1) root.RowDefinitions[1].Height = new GridLength(0);
            }
        }
        catch { }

        try
        {
            // Use a real borderless fullscreen window, like a modern browser.
            WindowDecorations = Avalonia.Controls.WindowDecorations.None;
            WindowState = WindowState.FullScreen;
        }
        catch { }
    }

    private void ExitVideoFullscreen()
    {
        if (!_videoFullscreen) return;
        _videoFullscreen = false;

        try
        {
            if (_normalModePage?.WebViewGrid?.Parent is Grid root)
            {
                if (root.RowDefinitions.Count > 0) root.RowDefinitions[0].Height = GridLength.Auto;
                if (root.RowDefinitions.Count > 1) root.RowDefinitions[1].Height = GridLength.Auto;
            }

            if (_normalModePage?.TabBar != null) _normalModePage.TabBar.IsVisible = true;
            if (_normalModePage?.NavBarRow != null) _normalModePage.NavBarRow.IsVisible = true;
        }
        catch { }

        try
        {
            // Restore the exact window state and decorations from before video
            // fullscreen instead of toggling maximize (which causes a visible
            // resize/flicker on exit).
            WindowState = _preVideoFullscreenWindowState;
            WindowDecorations = _preVideoFullscreenDecorations;
        }
        catch { }
    }

    /// <summary>
    /// Opens URL in a NEW browser tab (used by Tools window for local engines/servers)
    /// </summary>
    public void OpenUrlInNewTab(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return;

        try
        {
            CreateNewTab(url);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"MainWindow: Failed to open new tab: {ex}");
        }
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
            System.Diagnostics.Debug.WriteLine($"[MainWindow] ===== NavigateToSelectedSearchHomeAsync STARTED =====");

            var home = await GetSearchHomePageAsync();
            System.Diagnostics.Debug.WriteLine($"[MainWindow] Home page URL: {home}");

            // Якщо це Vetale Search (внутрішня домашня сторінка)
            if (string.Equals(home, "vetale://search", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(home, "vetale://search/home", StringComparison.OrdinalIgnoreCase))
            {
                System.Diagnostics.Debug.WriteLine($"[MainWindow] Detected Vetale Search - calling OpenVetaleSearchInCurrentTab()");
                OpenVetaleSearchInCurrentTab();
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[MainWindow] Regular URL - calling NavigateUrlInActiveTab()");
                NavigateUrlInActiveTab(home, openInNewTabIfNone: true);
            }

            System.Diagnostics.Debug.WriteLine($"[MainWindow] ===== NavigateToSelectedSearchHomeAsync COMPLETED =====");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MainWindow] NavigateToSelectedSearchHomeAsync ERROR: {ex}");
            System.Diagnostics.Debug.WriteLine($"[MainWindow] Stack trace: {ex.StackTrace}");
        }
    }

    private async Task<string> GetSearchHomePageAsync()
    {
        try
        {
            System.Diagnostics.Debug.WriteLine($"[MainWindow] GetSearchHomePageAsync STARTED");
            const string vetaleSearchName = "Vetale Search";

            // Якщо налаштовано Vetale Search як локальний пошук
            if (_settingsService != null)
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine($"[MainWindow] Getting search engine name and URL from settings...");
                    var name = await _settingsService.GetSearchEngineNameAsync();
                    var url = await _settingsService.GetSearchEngineUrlAsync();

                    System.Diagnostics.Debug.WriteLine($"[MainWindow] Settings: Name='{name}', URL='{url}'");

                    if (string.Equals(name, vetaleSearchName, StringComparison.OrdinalIgnoreCase) &&
                        string.IsNullOrWhiteSpace(url))
                    {
                        // Повертаємо внутрішню домашню сторінку Vetale Search
                        System.Diagnostics.Debug.WriteLine($"[MainWindow] Detected Vetale Search! Returning vetale://search");
                        return "vetale://search";
                    }

                    System.Diagnostics.Debug.WriteLine($"[MainWindow] Not Vetale Search, proceeding with web search engine logic");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[MainWindow] GetSearchHomePageAsync: failed to get name/url: {ex.Message}");
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[MainWindow] Settings service is NULL");
            }

            // Для веб-пошукових систем будуємо URL за шаблоном
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

            System.Diagnostics.Debug.WriteLine($"[MainWindow] Using template: {template}");

            // Derive homepage from template: take scheme+host root
            string basePart = template;
            var qIdx = template.IndexOf('?');
            if (qIdx >= 0)
                basePart = template.Substring(0, qIdx);

            if (Uri.TryCreate(basePart, UriKind.Absolute, out var uri))
            {
                var home = $"{uri.Scheme}://{uri.Host}/";
                System.Diagnostics.Debug.WriteLine($"[MainWindow] Returning web home page: {home}");
                return home;
            }

            // Fallback to Google
            System.Diagnostics.Debug.WriteLine($"[MainWindow] Falling back to Google");
            return "https://www.google.com/";
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MainWindow] GetSearchHomePageAsync EXCEPTION: {ex.Message}");
            return "https://www.google.com/";
        }
    }

    private void OnNavigationBarNavigateRequested(object? sender, string url)
    {
        try
        {
            HandleNavigationUrl(url);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"MainWindow: OnNavigationBarNavigateRequested error: {ex}");
        }
    }

    private void OnNavBarBackRequested(object? sender, EventArgs e)
    {
        try
        {
            var w = _tabs.Active;
            if (w == null) return;
            w.GoBack();
            RefreshNavBarFromWorker(w);
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[MainWindow] Back error: {ex.Message}"); }
    }

    private void OnNavBarForwardRequested(object? sender, EventArgs e)
    {
        try
        {
            var w = _tabs.Active;
            if (w == null) return;
            w.GoForward();
            RefreshNavBarFromWorker(w);
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[MainWindow] Forward error: {ex.Message}"); }
    }

    private void OnNavBarReloadRequested(object? sender, EventArgs e)
    {
        try { _tabs.Active?.Manager?.Reload(); }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[MainWindow] Reload error: {ex.Message}"); }
    }

    private async void OnNavBarHomeRequested(object? sender, EventArgs e)
    {
        try
        {
            var home = await GetSearchHomePageAsync();
            HandleNavigationUrl(home);
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[MainWindow] Home error: {ex.Message}"); }
    }

    private void RefreshNavBarFromWorker(VetaleBrowser.Core.Scripts.Models.TabWorker w)
    {
        try
        {
            var bar = _normalModePage?.NavBar;
            bar?.SetState(w.Address, w.CanGoBack, w.CanGoForward);
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[MainWindow] RefreshNavBar error: {ex.Message}"); }
    }

    private void OnManagerNavigated(object? sender, string url)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine($"MainWindow: OnManagerNavigated: {url}");

            // Для vetale:// URL обробляємо спеціально
            if (url.StartsWith("vetale://", StringComparison.OrdinalIgnoreCase))
            {
                // Створюємо нову вкладку для vetale:// URL
                CreateNewTab(url);
            }
            else
            {
                // Звичайна навігація по URL
                NavigateUrlInActiveTab(url, openInNewTabIfNone: true);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"MainWindow: OnManagerNavigated error: {ex}");
        }
    }

    private void HandleNavigationUrl(string url)
    {
        try
        {
            if (InternalUrlHandler.IsInternalUrl(url))
            {
                HandleInternalNavigation(url);
            }
            else
            {
                NavigateUrlInActiveTab(url, openInNewTabIfNone: true);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"MainWindow: HandleNavigationUrl error: {ex}");
        }
    }

    private void HandleInternalNavigation(string url)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine($"[MainWindow] HandleInternalNavigation called with URL: {url}");

            if (_tabs.Active == null)
            {
                System.Diagnostics.Debug.WriteLine($"[MainWindow] No active tab, creating new tab");
                CreateNewTab(url);
                return;
            }

            System.Diagnostics.Debug.WriteLine($"[MainWindow] Active tab exists, creating page content");

            // Reuse the current results page for a new results search in the same tab.
            // Otherwise every search would create a new VetaleSearchResultsPage with
            // a new Perplexity WebView (heavy Chromium copy) kept alive in history.
            if (InternalUrlHandler.GetPageType(url) == InternalPageType.VetaleSearchResults
                && _tabs.Active.History.CurrentEntry?.InternalPageContent is VetaleSearchResultsPage existingResults)
            {
                System.Diagnostics.Debug.WriteLine("[MainWindow] Reusing existing VetaleSearchResultsPage (no new WebView)");
                var q = InternalUrlHandler.GetQueryParameter(url, "q") ?? string.Empty;
                var mode = InternalUrlHandler.GetQueryParameter(url, "mode");
                existingResults.SetSearchQuery(q, mode);

                System.Diagnostics.Debug.WriteLine($"[MainWindow] Navigating through TabWorker to: {url}");
                _tabs.Active.Navigate(url, existingResults);

                // Підписуємося на подію NavigationChanged для оновлення UI
                _tabs.Active.NavigationChanged -= OnWorkerNavigationChanged;
                _tabs.Active.NavigationChanged += OnWorkerNavigationChanged;
                return;
            }

            var content = InternalUrlHandler.CreatePageContent(url);
            if (content == null)
            {
                System.Diagnostics.Debug.WriteLine($"[MainWindow] Unknown internal URL: {url}");
                return;
            }

            System.Diagnostics.Debug.WriteLine($"[MainWindow] Page content created: {content.GetType().Name}");

            // Підписуємося на події навігації від внутрішніх сторінок
            if (content is VetaleSearchHomePage searchHomePage)
            {
                System.Diagnostics.Debug.WriteLine($"[MainWindow] Subscribing to VetaleSearchHomePage.NavigateRequested");
                searchHomePage.NavigateRequested += OnInternalPageNavigateRequested;
            }
            else if (content is VetaleSearchResultsPage resultsPage)
            {
                System.Diagnostics.Debug.WriteLine($"[MainWindow] Subscribing to VetaleSearchResultsPage events");
                resultsPage.NavigateRequested += OnInternalPageNavigateRequested;
                resultsPage.SearchResultNavigateRequested += OnSearchResultNavigateRequested;
            }

            // Використовуємо нову систему навігації через TabWorker
            System.Diagnostics.Debug.WriteLine($"[MainWindow] Navigating through TabWorker to: {url}");
            _tabs.Active.Navigate(url, content);

            // Підписуємося на подію NavigationChanged для оновлення UI
            _tabs.Active.NavigationChanged -= OnWorkerNavigationChanged;
            _tabs.Active.NavigationChanged += OnWorkerNavigationChanged;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"MainWindow: HandleInternalNavigation error: {ex}");
        }
    }

    /// <summary>
    /// Обробник зміни навігації у TabWorker (оновлює UI)
    /// </summary>
    private void OnWorkerNavigationChanged(object? sender, NavigationEntry entry)
    {
        try
        {
            if (sender is not TabWorker worker) return;

            System.Diagnostics.Debug.WriteLine($"[MainWindow] OnWorkerNavigationChanged: {entry.Url}, IsInternal: {entry.IsInternal}");

            // Контейнер і адресний рядок чіпаємо ТІЛЬКИ для активної вкладки,
            // інакше фонова навігація переписує адресу і підміняє в'ю
            // (розсинхрон "адреса однієї вкладки, контент іншої").
            bool isActive = _tabs.Active == worker;

            if (isActive)
            {
                _normalModePage?.NavBar?.SetState(
                    worker.Address ?? entry.Url,
                    worker.CanGoBack,
                    worker.CanGoForward);
            }

            if (isActive)
            {
                if (entry.IsInternal && entry.InternalPageContent != null)
                {
                    // Показуємо внутрішню сторінку
                    ActivateWorkerForInternalPage(worker, entry.InternalPageContent, entry.Url);

                    // Оновлюємо іконку для внутрішньої сторінки (Vetale Search)
                    _ = UpdateFaviconAsync(entry.Url, worker);

                    // Іконка Vetale Search ставиться синхронно, щоб була завжди
                    if (IsVetaleSearchPageUrl(entry.Url))
                    {
                        ApplyVetaleSearchIconToTab(worker);
                    }
                }
                else
                {
                    // Показуємо WebView для зовнішніх URL ТІЛЬКИ якщо це активна вкладка.
                    // Інакше фонова навігація (редирект, программного Navigate) крала фокус
                    // і підміняла в'ю: "одна вкладка перебирає на себе іншу".
                    if (isActive)
                        ActivateWorker(worker);
                }
            }

            // Перевіряємо, чи це гра HexGL (localhost з index.html від локального сервера)
            var url = entry.Url ?? "";
            var isHexGlGame = url.StartsWith("http://localhost:") && 
                              (url.Contains("index.html") || url.EndsWith("/"));
            
            if (isHexGlGame)
            {
                // Оновлюємо заголовок вкладки на назву гри з розробником
                UpdateTabTitle(worker, "HexGL - by Thibaut Despoulain");

                if (isActive)
                {
                    // Оновлюємо адресний рядок з інформацією про гру
                    UpdateNavigationBar("vetale://game/hexgl?by=Thibaut%20Despoulain");

                    // Примусово оновлюємо іконку гри
                    _ = UpdateFaviconAsync(url, worker);
                }
            }
            else
            {
                // Оновлюємо заголовок вкладки
                UpdateTabTitle(worker, entry.Title ?? worker.Title);

                if (isActive)
                {
                    // Оновлюємо адресний рядок
                    UpdateNavigationBar(entry.Url);
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MainWindow] OnWorkerNavigationChanged error: {ex}");
        }
    }
    
    /// <summary>
    /// Обробник помилок браузера у TabWorker (показує локалізовану сторінку помилки)
    /// </summary>
    private void OnWorkerErrorOccurred(object? sender, BrowserErrorEventArgs e)
    {
        try
        {
            if (sender is not TabWorker worker) return;
            if (_tabs.Active != worker) return; // Показуємо помилку тільки для активної вкладки
            
            System.Diagnostics.Debug.WriteLine($"[MainWindow] Browser error: {e.Error.Title} ({e.Error.ErrorName}) for {e.Error.FailedUrl}");
            
            // Створюємо сторінку помилки
            var errorPage = e.CreateErrorPage();
            
            // Підписуємося на дії користувача
            errorPage.RetryRequested += (_, _) =>
            {
                // Перезавантажуємо сторінку
                if (!string.IsNullOrEmpty(e.Error.FailedUrl))
                {
                    worker.Navigate(e.Error.FailedUrl);
                }
                else
                {
                    worker.Manager.Reload();
                }
            };
            
            errorPage.GoBackRequested += (_, _) =>
            {
                if (worker.CanGoBack)
                {
                    worker.GoBack();
                }
                else
                {
                    // Якщо немає історії, переходимо на домашню сторінку
                    worker.Navigate("vetale://search");
                }
            };
            
            errorPage.GoHomeRequested += (_, _) =>
            {
                worker.Navigate("vetale://search");
            };
            
            errorPage.SearchRequested += (_, query) =>
            {
                // Навігуємо до Vetale Search з запитом
                var searchUrl = $"vetale://search?q={Uri.EscapeDataString(query)}";
                var content = InternalUrlHandler.CreatePageContent(searchUrl);
                if (content != null)
                {
                    SubscribeToInternalPageEvents(content);
                    worker.Navigate(searchUrl, content);
                }
            };
            
            errorPage.PlayGameRequested += (_, _) =>
            {
                try
                {
                    // Запускаємо локальний HTTP сервер для гри
                    var gameServer = LocalGameServer.Instance;
                    if (!gameServer.IsRunning)
                    {
                        gameServer.Start();
                    }
                    
                    if (gameServer.IsRunning)
                    {
                        // Навігуємо до гри через HTTP
                        var gameUrl = gameServer.GameUrl;
                        worker.Navigate(gameUrl);
                        
                        // Оновлюємо заголовок вкладки на назву гри
                        UpdateTabTitle(worker, "HexGL - by Thibaut Despoulain");
                        
                        System.Diagnostics.Debug.WriteLine($"[MainWindow] Navigating to HexGL game via HTTP: {gameUrl}");
                    }
                    else if (gameServer.GameExists)
                    {
                        // Fallback: сервер не стартував — запускаємо гру напряму
                        // з папки VetaleBrowserOfflineGame через file://
                        var fileUrl = gameServer.GameFileUrl;
                        worker.Navigate(fileUrl);
                        
                        UpdateTabTitle(worker, "HexGL - by Thibaut Despoulain");
                        
                        System.Diagnostics.Debug.WriteLine($"[MainWindow] Game server failed, fallback to offline folder: {fileUrl}");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"[MainWindow] Failed to start game server and offline folder missing: {gameServer.GameRootPath}");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[MainWindow] Error starting game: {ex.Message}");
                    // Останній шанс: пробуємо відкрити гру з папки напряму
                    try
                    {
                        var fallbackServer = LocalGameServer.Instance;
                        if (fallbackServer.GameExists)
                        {
                            var fileUrl = fallbackServer.GameFileUrl;
                            worker.Navigate(fileUrl);
                            UpdateTabTitle(worker, "HexGL - by Thibaut Despoulain");
                            System.Diagnostics.Debug.WriteLine($"[MainWindow] Exception fallback to offline folder: {fileUrl}");
                        }
                    }
                    catch (Exception fallbackEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"[MainWindow] Fallback game launch failed: {fallbackEx.Message}");
                    }
                }
            };
            
            // Показуємо сторінку помилки в контейнері
            Dispatcher.UIThread.Post(() =>
            {
                try
                {
                    var targetContainer = _isFullscreen 
                        ? _fullscreenModePage?.FullscreenGrid 
                        : _normalModePage?.WebViewGrid;
                    
                    if (targetContainer != null)
                    {
                        targetContainer.Children.Clear();
                        targetContainer.Children.Add(errorPage);
                        System.Diagnostics.Debug.WriteLine($"[MainWindow] Error page displayed: {e.Error.Title}");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[MainWindow] Failed to display error page: {ex.Message}");
                }
            });
            
            e.Handled = true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MainWindow] OnWorkerErrorOccurred error: {ex}");
        }
    }

    /// <summary>
    /// Оновлює заголовок вкладки
    /// </summary>
    private void UpdateTabTitle(TabWorker worker, string? title)
    {
        // СТРОГО по мапі worker->Tab, а не за індексом (див. ActivateWorker):
        // індексна математика ставила назву лівої вкладки на праву і навпаки.
        var tab = FindTabByWorker(worker);
        if (tab != null)
        {
            tab.Title = title ?? "New Tab";
            System.Diagnostics.Debug.WriteLine($"[MainWindow] Tab title updated to: {tab.Title}");
            return;
        }

        foreach (var overflowWindow in _tabOverflowWindows)
        {
            if (overflowWindow.GetTabByWorker(worker) != null)
            {
                overflowWindow.UpdateTabTitle(worker, title ?? "New Tab");
                return;
            }
        }
    }

    /// <summary>
    /// Оновлює адресний рядок
    /// </summary>
    private void UpdateNavigationBar(string? url)
    {
        var navBar = _normalModePage?.NavBar;
        if (navBar != null && !string.IsNullOrWhiteSpace(url))
        {
            navBar.Url = url;

            // Оновлюємо стан кнопок назад/вперед
            if (_tabs.Active != null)
            {
                navBar.CanGoBack = _tabs.Active.CanGoBack;
                navBar.CanGoForward = _tabs.Active.CanGoForward;
            }
        }
    }

    /// <summary>
    /// Обробник навігації від внутрішніх сторінок (VetaleSearchHomePage/VetaleSearchResultsPage)
    /// </summary>
    private void OnInternalPageNavigateRequested(object? sender, string url)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine($"[MainWindow] ===== OnInternalPageNavigateRequested =====");
            System.Diagnostics.Debug.WriteLine($"[MainWindow] URL: {url}");
            System.Diagnostics.Debug.WriteLine($"[MainWindow] Sender: {sender?.GetType().Name ?? "null"}");

            if (InternalUrlHandler.IsInternalUrl(url))
            {
                HandleInternalNavigation(url);
            }
            else
            {
                var activeWorker = _tabs.Active;
                if (activeWorker != null)
                {
                    // Валідний лише http/https
                    if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
                        !(uri.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase) || uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase)))
                    {
                        System.Diagnostics.Debug.WriteLine($"[MainWindow] Invalid URL for navigation: '{url}'");
                        return;
                    }

                    // Навігація: далі OnWorkerNavigationChanged сам оновить UI/адресний рядок/кнопки
                    activeWorker.Navigate(url);
                }
                else
                {
                    CreateNewTab(url);
                }
            }

            System.Diagnostics.Debug.WriteLine($"[MainWindow] ===== OnInternalPageNavigateRequested END =====");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MainWindow] OnInternalPageNavigateRequested ERROR: {ex.Message}");
        }
    }

    /// <summary>
    /// Обробник навігації з результату пошуку (з повною інформацією про сесію).
    /// Замінює внутрішню сторінку результатів на WebView у поточній вкладці і навігує на URL.
    /// </summary>
    private void OnSearchResultNavigateRequested(object? sender, SearchResultNavigationEventArgs e)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine($"[MainWindow] ===== OnSearchResultNavigateRequested =====");
            System.Diagnostics.Debug.WriteLine($"[MainWindow] URL: '{e.Url}', SessionId: {e.SessionId}, Query: '{e.Query}', Source: {e.SourceType}");

            _searchHistoryService?.AddSearchClick(e.SessionId, e.Query, e.Url, e.SourceType);

            var currentWorker = _tabs.Active;
            var url = e.Url?.Trim();
            if (string.IsNullOrWhiteSpace(url)) return;
            if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                url = "https://" + url;
            }
            if (!Uri.TryCreate(url, UriKind.Absolute, out var _))
            {
                System.Diagnostics.Debug.WriteLine($"[MainWindow] Invalid URL after normalization: '{url}'");
                return;
            }

            if (currentWorker == null)
            {
                CreateNewTab(url);
            }
            else
            {
                currentWorker.Navigate(url);
            }

            System.Diagnostics.Debug.WriteLine($"[MainWindow] ===== OnSearchResultNavigateRequested END =====");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MainWindow] OnSearchResultNavigateRequested ERROR: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"[MainWindow] Stack: {ex.StackTrace}");
        }
    }

    public void OpenVetaleSearchInCurrentTab()
    {
        Dispatcher.UIThread.Post(async () =>
        {
            const string vetaleSearchUrl = "vetale://search";

            try
            {
                // Use the same internal navigation pipeline everywhere to keep event wiring stable
                HandleInternalNavigation(vetaleSearchUrl);

                // Best-effort: favicon update
                await UpdateFaviconAsync(vetaleSearchUrl);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MainWindow] OpenVetaleSearchInCurrentTab error: {ex}");
            }
        });
    }
}
