using System;
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
using WebViewControl;
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
using VetaleBrowser.VetaleBrowser.VoiceRecognition.Services;
using VetaleBrowser.VetaleBrowser.Core.Services;
using VetaleBrowser.VetaleBrowser.UI.Elements;

namespace VetaleBrowser;

public partial class MainWindow : Window
{
    private readonly WindowManager? _windowManager;
    private readonly IFaviconService _faviconService = new FaviconService();
    private ISettingsService? _settingsService;
    private IAppearanceSettingsService? _appearanceSettingsService;

    private readonly TabsManager _tabs = new();

    // Сервіси для пошуку
    private readonly VetaleBrowser.Search.Services.ISearchNavigationService _searchNavigationService = new VetaleBrowser.Search.Services.SearchNavigationService();
    private VetaleBrowser.Search.Database.ISearchHistoryDatabaseService? _searchHistoryService;

    // Public property to access TabsManager (for DevTools)
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
                // Оновлюємо поточну вкладку на звичайний URL
                _ = activeWorker.Manager.NavigateAsync(url);
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
    private ContentControl? _pageContainer;
    
    // MEMORY OPTIMIZATION: Lazy initialization of heavy services
    private VetaleBrowser.Search.Services.ISuggestionsService? _globalSuggestions;
    private VetaleBrowser.Search.Services.ISecurityCheckService? _securityCheckService;
    private IVoiceRecognitionService? _voiceRecognitionService;
    
    // Lazy getters for services
    private VetaleBrowser.Search.Services.ISuggestionsService GlobalSuggestions 
        => _globalSuggestions ??= new VetaleBrowser.Search.Services.GoogleSuggestionsService();
    
    private VetaleBrowser.Search.Services.ISecurityCheckService SecurityCheckService 
        => _securityCheckService ??= new VetaleBrowser.Search.Services.PhishTankSecurityService();
    
    private IVoiceRecognitionService VoiceRecognitionService 
        => _voiceRecognitionService ??= new WindowsVoiceRecognitionService();

    // Keep track of which worker's WebView we're listening to
    private TabWorker? _subscribedWorker;

    // Fullscreen state
    private bool _isFullscreen;
    private WindowState _preFullscreenWindowState;

    // Polling support for robust favicon and title updates - MEMORY OPTIMIZATION: longer interval
    private readonly DispatcherTimer _faviconPollTimer = new() { Interval = TimeSpan.FromMilliseconds(1000) };
    private string? _lastFaviconUrl;
    private string? _lastPageTitle;

    // Cached appearance values
    private double _tabWidth = 200.0;
    
    // Tab overflow system
    private TabOverflowWindow? _tabOverflowWindow;
    private const int MaxTabsNormalMode = 4;   // Максимум вкладок у звичайному режимі
    private const int MaxTabsFullscreenMode = 9; // Максимум вкладок у повноекранному режимі
    
    /// <summary>Поточний ліміт вкладок залежно від режиму</summary>
    private int CurrentMaxTabs => _isFullscreen ? MaxTabsFullscreenMode : MaxTabsNormalMode;

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

    private async void OnWindowLoaded(object? sender, RoutedEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine("MainWindow: OnWindowLoaded called");

        try
        {
            // MEMORY OPTIMIZATION: Set lazy service providers - services created on first use only
            InternalUrlHandler.SuggestionsServiceProvider = () => GlobalSuggestions;
            InternalUrlHandler.VoiceRecognitionServiceProvider = () => VoiceRecognitionService;
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

            // Initialize NavigationBar with the active tab's manager
            try
            {
                var navigationBar = _normalModePage?.NavBar;
                if (navigationBar != null && _tabs.Active != null)
                {
                    navigationBar.Initialize(_tabs.Active.Manager);
                    navigationBar.SetSuggestionsService(GlobalSuggestions);
                    navigationBar.SetSecurityCheckService(SecurityCheckService);
                    // Підписка на внутрішню навігацію (vetale://) з адресного рядка/Додому
                    navigationBar.NavigateRequested -= OnNavigationBarNavigateRequested;
                    navigationBar.NavigateRequested += OnNavigationBarNavigateRequested;
                    
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
            else
            {
                // Для зовнішніх URL навігуємо без content
                worker.Navigate(initialUrl);
            }
        }
        
        ActivateWorker(worker);
        return worker;
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
            targetContainer.Children.Clear();
            targetContainer.Children.Add(pageContent);
            System.Diagnostics.Debug.WriteLine($"[MainWindow] Internal page added to container: {pageContent.GetType().Name}");
        }
        
        var tabsHost = _normalModePage?.TabsHostPanel;
        if (tabsHost != null)
        {
            for (int widx = 0; widx < _tabs.Workers.Count; widx++)
            {
                var childIdx = widx + 1;
                if (childIdx >= 0 && childIdx < tabsHost.Children.Count && tabsHost.Children[childIdx] is Tab t)
                {
                    t.IsActive = (_tabs.Workers[widx] == worker);
                }
            }
        }
        
        // Оновлюємо NavBar з новим URL
        var navBar = _normalModePage?.NavBar;
        if (navBar != null)
        {
            navBar.Url = finalUrl;
            navBar.CanGoBack = worker.History.CanGoBack;
            navBar.CanGoForward = worker.History.CanGoForward;
            System.Diagnostics.Debug.WriteLine($"[MainWindow] NavBar updated: Url={navBar.Url}, CanGoBack={navBar.CanGoBack}, CanGoForward={navBar.CanGoForward}");
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
            _tabs.Close(worker);
        };
        tab.MuteToggled += async (_, __) =>
        {
            await HandleMuteToggle(worker, tab, tabsHost);
        };

        return tab;
    }

    /// <summary>
    /// Обробляє toggle mute для вкладки
    /// </summary>
    private async Task HandleMuteToggle(TabWorker worker, Tab tab, StackPanel tabsHost)
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
    }

    /// <summary>
    /// Додає вкладку до overflow вікна
    /// </summary>
    private void AddTabToOverflow(TabWorker worker)
    {
        // Ініціалізуємо overflow вікно якщо потрібно
        if (_tabOverflowWindow == null)
        {
            _tabOverflowWindow = new TabOverflowWindow();
            _tabOverflowWindow.Initialize(this, _tabWidth);
            
            _tabOverflowWindow.TabCloseRequested += OnOverflowTabCloseRequested;
            _tabOverflowWindow.TabActivated += OnOverflowTabActivated;
            _tabOverflowWindow.CloseAllTabsRequested += OnOverflowCloseAllRequested;
            _tabOverflowWindow.BecameEmpty += OnOverflowBecameEmpty;
        }
        
        // Перевіряємо чи є місце в overflow
        if (_tabOverflowWindow.IsFull)
        {
            System.Diagnostics.Debug.WriteLine("[MainWindow] Overflow window is full, cannot add more tabs");
            return;
        }
        
        if (!_tabOverflowWindow.AddTab(worker))
        {
            System.Diagnostics.Debug.WriteLine("[MainWindow] Failed to add tab to overflow window");
            return;
        }
        
        System.Diagnostics.Debug.WriteLine($"[MainWindow] Tab added to overflow window. Total overflow tabs: {_tabOverflowWindow.TabCount}");
    }

    private void OnOverflowTabCloseRequested(object? sender, TabWorker worker)
    {
        _tabs.Close(worker);
    }

    private void OnOverflowTabActivated(object? sender, TabWorker worker)
    {
        ActivateWorker(worker);
        _tabOverflowWindow?.SetActiveTab(worker);
    }

    private void OnOverflowCloseAllRequested(object? sender, EventArgs e)
    {
        if (_tabOverflowWindow == null) return;
        
        // Закриваємо всі workers в overflow
        var workers = _tabOverflowWindow.GetAllWorkers().ToList();
        foreach (var worker in workers)
        {
            _tabs.Close(worker);
        }
        
        _tabOverflowWindow.Hide();
    }

    private void OnOverflowBecameEmpty(object? sender, EventArgs e)
    {
        // Overflow вікно автоматично приховається
        System.Diagnostics.Debug.WriteLine("[MainWindow] Overflow window became empty");
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
                    // Знаходимо відповідний worker
                    int workerIndex = i - 1; // -1 через кнопку "+"
                    if (workerIndex >= 0 && workerIndex < _tabs.Workers.Count)
                    {
                        lastWorker = _tabs.Workers.ToList()[workerIndex];
                    }
                    break;
                }
            }
            
            if (lastTab != null && lastWorker != null)
            {
                tabsHost.Children.Remove(lastTab);
                AddTabToOverflow(lastWorker);
                currentTabCount--;
            }
            else
            {
                break;
            }
        }
        
        // Якщо є місце в основній панелі і є вкладки в overflow - повертаємо їх
        while (currentTabCount < maxTabs && _tabOverflowWindow != null && _tabOverflowWindow.TabCount > 0)
        {
            var overflowWorkers = _tabOverflowWindow.GetAllWorkers().ToList();
            if (overflowWorkers.Count > 0)
            {
                var workerToMove = overflowWorkers[0];
                
                // Видаляємо з overflow (потрібно спочатку отримати Tab)
                // Створюємо нову вкладку в основній панелі
                var newTab = CreateTabForWorker(workerToMove, tabsHost);
                tabsHost.Children.Add(newTab);
                
                // Видаляємо worker з overflow через закриття
                // (TabOverflowWindow сам обробить видалення)
                _tabOverflowWindow.RemoveWorker(workerToMove);
                
                currentTabCount++;
            }
            else
            {
                break;
            }
        }
        
        // Приховуємо overflow якщо порожнє
        if (_tabOverflowWindow != null && _tabOverflowWindow.TabCount == 0)
        {
            _tabOverflowWindow.Hide();
        }
        
        System.Diagnostics.Debug.WriteLine($"[MainWindow] ReorganizeTabsForMode complete: mainCount={GetMainPanelTabCount()}, overflowCount={_tabOverflowWindow?.TabCount ?? 0}");
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
            navigationBar.SetTabWorker(worker); // Додаємо підтримку TabWorker
            navigationBar.SetSuggestionsService(GlobalSuggestions);
            navigationBar.SetSecurityCheckService(SecurityCheckService);
            
            // Set settings service for search engine configuration
            if (_settingsService != null)
            {
                navigationBar.SetSettingsService(_settingsService);
            }
            
            navigationBar.Url = worker.Address ?? string.Empty;
            navigationBar.CanGoBack = worker.History.CanGoBack;
            navigationBar.CanGoForward = worker.History.CanGoForward;
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

            // Визначаємо що показувати за History.CurrentEntry
            var current = active.History.CurrentEntry;
            if (current != null && current.IsInternal && current.InternalPageContent != null)
            {
                target.Children.Clear();
                target.Children.Add(current.InternalPageContent);
                System.Diagnostics.Debug.WriteLine("[MainWindow] Internal page (from History) moved to container");
                return;
            }

            var webView = active.WebView;

            // Вилучити з попереднього контейнера якщо потрібно
            if (webView.Parent is Panel prev && !ReferenceEquals(prev, target))
            {
                prev.Children.Remove(webView);
            }

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

    private bool IsCurrentVetaleSearchInternalPage()
    {
        try
        {
            var entry = _tabs.Active?.History.CurrentEntry;
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
            if (_tabs.Active == null) return;
            var vw = _tabs.Active.WebView;
            if (!ReferenceEquals(sender, vw)) return; // only react to active

            var prop = e.Property?.Name;
            if (prop == "Address")
            {
                var url = _tabs.Active.Manager.GetCurrentUrl();
                _lastFaviconUrl = url; // keep poll baseline in sync
                Dispatcher.UIThread.Post(() =>
                {
                    var nav = _normalModePage?.NavBar;
                    if (nav != null)
                    {
                        nav.Url = url ?? string.Empty;
                        // ВАЖЛИВО: стан кнопок з History, а не з WebView
                        nav.CanGoBack = _tabs.Active.History.CanGoBack;
                        nav.CanGoForward = _tabs.Active.History.CanGoForward;
                    }
                });

                await UpdateFaviconAsync(url);
                await UpdateTabTitleAsync(null, url);

                // Збереження в історію БД
                try
                {
                    if (!string.IsNullOrWhiteSpace(url))
                    {
                        var title = vw.Title ?? url;
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
                Dispatcher.UIThread.Post(() =>
                {
                    var nav = _normalModePage?.NavBar;
                    if (nav != null && _tabs.Active != null)
                    {
                        nav.CanGoBack = _tabs.Active.History.CanGoBack;
                        nav.CanGoForward = _tabs.Active.History.CanGoForward;
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
            
            // Перевіряємо чи це Vetale Search (vetale:// або vetale:)
            var isVetaleSearch = address.StartsWith("vetale://", StringComparison.OrdinalIgnoreCase) ||
                                 address.StartsWith("vetale:", StringComparison.OrdinalIgnoreCase);
            
            if (isVetaleSearch)
            {
                // Завантажуємо іконку Vetale Search
                System.Diagnostics.Debug.WriteLine($"[MainWindow] Detected Vetale URL: {address}, loading Vetale Search icon");
                await LoadVetaleSearchIconAsync();
                return;
            }
            
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
                // Стандартний favicon
                image = await _faviconService.GetFaviconAsync(uri, size);
            }
            
            Dispatcher.UIThread.Post(() =>
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

    /// <summary>
    /// Завантажує іконку Vetale Search для вкладки
    /// </summary>
    private Task LoadVetaleSearchIconAsync()
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("[MainWindow] LoadVetaleSearchIconAsync called");
            
            IImage? image = null;
            
            // Спосіб 1: Завантажуємо з Application ресурсів
            try
            {
                System.Diagnostics.Debug.WriteLine("[MainWindow] Trying to load from Application resources...");
                if (Application.Current != null)
                {
                    if (Application.Current.TryFindResource("VetaleSearchIconImage", out var resource))
                    {
                        System.Diagnostics.Debug.WriteLine($"[MainWindow] Resource found, type: {resource?.GetType().Name ?? "null"}");
                        if (resource is IImage img)
                        {
                            image = img;
                            System.Diagnostics.Debug.WriteLine("[MainWindow] Vetale Search icon loaded from Application resources");
                        }
                        else if (resource is Avalonia.Media.Imaging.Bitmap bmp)
                        {
                            image = bmp;
                            System.Diagnostics.Debug.WriteLine("[MainWindow] Vetale Search icon loaded as Bitmap from resources");
                        }
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("[MainWindow] VetaleSearchIconImage resource NOT found");
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("[MainWindow] Application.Current is null");
                }
            }
            catch (Exception resEx)
            {
                System.Diagnostics.Debug.WriteLine($"[MainWindow] Failed to load from resources: {resEx.Message}");
            }
            
            // Спосіб 2: Завантажуємо через Uri напряму
            if (image == null)
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine("[MainWindow] Trying to load via direct Uri...");
                    var uri = new Uri("avares://VetaleBrowser/VetaleBrowser.UI/Sources/Icons/VetaleSearchIcon.png");
                    image = new Avalonia.Media.Imaging.Bitmap(Avalonia.Platform.AssetLoader.Open(uri));
                    System.Diagnostics.Debug.WriteLine("[MainWindow] Vetale Search icon loaded via direct Uri");
                }
                catch (Exception uriEx)
                {
                    System.Diagnostics.Debug.WriteLine($"[MainWindow] Failed to load via Uri: {uriEx.Message}");
                }
            }
            
            // Спосіб 3: файлова система
            if (image == null)
            {
                try
                {
                    var basePath = AppDomain.CurrentDomain.BaseDirectory;
                    System.Diagnostics.Debug.WriteLine($"[MainWindow] Base path: {basePath}");
                    
                    // Список можливих шляхів до іконки
                    var possiblePaths = new[]
                    {
                        Path.Combine(basePath, "VetaleBrowser.UI", "Sources", "Icons", "VetaleSearchIcon.png"),
                        Path.Combine(basePath, "Sources", "Icons", "VetaleSearchIcon.png"),
                        Path.Combine(basePath, "Icons", "VetaleSearchIcon.png"),
                        Path.Combine(basePath, "VetaleSearchIcon.png")
                    };
                    
                    foreach (var path in possiblePaths)
                    {
                        System.Diagnostics.Debug.WriteLine($"[MainWindow] Checking path: {path}, exists: {File.Exists(path)}");
                        if (File.Exists(path))
                        {
                            image = new Avalonia.Media.Imaging.Bitmap(path);
                            System.Diagnostics.Debug.WriteLine($"[MainWindow] Vetale Search icon loaded from file: {path}");
                            break;
                        }
                    }
                }
                catch (Exception fileEx)
                {
                    System.Diagnostics.Debug.WriteLine($"[MainWindow] Failed to load from file: {fileEx.Message}");
                }
            }
            
            System.Diagnostics.Debug.WriteLine($"[MainWindow] Final image state: {(image != null ? "LOADED" : "NULL")}");
            
            // Застосовуємо іконку до вкладки
            Dispatcher.UIThread.Post(() =>
            {
                var tabsHost = _normalModePage?.TabsHostPanel;
                System.Diagnostics.Debug.WriteLine($"[MainWindow] TabsHost: {(tabsHost != null ? "found" : "null")}, Active: {(_tabs.Active != null ? "found" : "null")}");
                
                if (tabsHost != null && _tabs.Active != null)
                {
                    var idx = _tabs.Workers.ToList().IndexOf(_tabs.Active);
                    var childIdx = idx + 1;
                    System.Diagnostics.Debug.WriteLine($"[MainWindow] Tab index: {idx}, childIdx: {childIdx}, children count: {tabsHost.Children.Count}");
                    
                    if (idx >= 0 && childIdx < tabsHost.Children.Count && tabsHost.Children[childIdx] is Tab tab)
                    {
                        tab.FaviconSource = image;
                        System.Diagnostics.Debug.WriteLine($"[MainWindow] Vetale Search icon APPLIED to tab: hasImage={(image != null)}");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("[MainWindow] Could not find tab at expected index");
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
    private Task UpdateTabTitleAsync(string? pageTitle, string? url)
    {
        try
        {
            var friendly = ComputeTitle(pageTitle, url);
            Dispatcher.UIThread.Post(() =>
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

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        if (_subscribedWorker != null)
        {
            try { _subscribedWorker.WebView.PropertyChanged -= WebView_OnPropertyChanged; } catch { }
            _subscribedWorker = null;
        }

        // Закриваємо overflow вікно
        try 
        { 
            _tabOverflowWindow?.Close();
            _tabOverflowWindow = null;
        } 
        catch { }

        _tabs.Dispose();
        if (_faviconService is IDisposable d) d.Dispose();
        _faviconPollTimer.Stop();
        
        // Зупиняємо локальний сервер гри
        try { LocalGameServer.Instance.Dispose(); } catch { }
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
        
        // Реорганізовуємо вкладки - в fullscreen режимі дозволено більше вкладок (9 замість 4)
        ReorganizeTabsForMode();
        
        // Приховуємо overflow вікно в fullscreen режимі (вкладки повертаються в основну панель)
        _tabOverflowWindow?.Hide();

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
        
        // Реорганізовуємо вкладки - в звичайному режимі дозволено менше вкладок (4)
        // Зайві вкладки переміщуються в overflow вікно
        ReorganizeTabsForMode();

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
            System.Diagnostics.Debug.WriteLine($"[MainWindow] NavigateToSelectedSearchHomeAsync ERROR: {ex.Message}");
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
                // Якщо з Home йдемо на Results — зберігаємо query у поточному записі Home і UI
                try
                {
                    var active = _tabs.Active;
                    var entry = active?.History.CurrentEntry;
                    if (active != null && entry != null && entry.IsInternal && entry.InternalPageContent is VetaleSearchHomePage homePage)
                    {
                        // Переходимо саме на результати пошуку?
                        if (url.StartsWith("vetale://search/results", StringComparison.OrdinalIgnoreCase))
                        {
                            var q = InternalUrlHandler.GetQueryParameter(url, "q");
                            if (!string.IsNullOrWhiteSpace(q))
                            {
                                var qSafe = q ?? string.Empty;
                                entry.Url = $"vetale://search?q={Uri.EscapeDataString(qSafe)}";
                                homePage.SetQuery(qSafe);
                            }
                        }
                    }
                }
                catch { }

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

            if (entry.IsInternal && entry.InternalPageContent != null)
            {
                // Показуємо внутрішню сторінку
                ActivateWorkerForInternalPage(worker, entry.InternalPageContent, entry.Url);
                
                // Оновлюємо іконку для внутрішньої сторінки (Vetale Search)
                _ = UpdateFaviconAsync(entry.Url);
            }
            else
            {
                // Показуємо WebView для зовнішніх URL
                ActivateWorker(worker);
            }

            // Перевіряємо, чи це гра HexGL (localhost з index.html від локального сервера)
            var url = entry.Url ?? "";
            var isHexGlGame = url.StartsWith("http://localhost:") && 
                              (url.Contains("index.html") || url.EndsWith("/"));
            
            if (isHexGlGame)
            {
                // Оновлюємо заголовок вкладки на назву гри з розробником
                UpdateTabTitle(worker, "HexGL - by Thibaut Despoulain");
                
                // Оновлюємо адресний рядок з інформацією про гру
                UpdateNavigationBar("vetale://game/hexgl?by=Thibaut%20Despoulain");
                
                // Примусово оновлюємо іконку гри
                _ = UpdateFaviconAsync(url);
            }
            else
            {
                // Оновлюємо заголовок вкладки
                UpdateTabTitle(worker, entry.Title ?? worker.Title);

                // Оновлюємо адресний рядок
                UpdateNavigationBar(entry.Url);
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
                if (worker.History.CanGoBack)
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
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("[MainWindow] Failed to start game server");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[MainWindow] Error starting game: {ex.Message}");
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
        var tabsHost = _normalModePage?.TabsHostPanel;
        if (tabsHost == null) return;

        var idx = _tabs.Workers.ToList().IndexOf(worker);
        var childIdx = idx + 1; // враховуючи кнопку додавання
        if (idx >= 0 && childIdx < tabsHost.Children.Count && tabsHost.Children[childIdx] is Tab tab)
        {
            tab.Title = title ?? "New Tab";
            System.Diagnostics.Debug.WriteLine($"[MainWindow] Tab title updated to: {tab.Title}");
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
                navBar.CanGoBack = _tabs.Active.History.CanGoBack;
                navBar.CanGoForward = _tabs.Active.History.CanGoForward;
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
                // Якщо з Home йдемо на Results — зберігаємо query у поточному записі Home і UI
                try
                {
                    var active = _tabs.Active;
                    var entry = active?.History.CurrentEntry;
                    if (active != null && entry != null && entry.IsInternal && entry.InternalPageContent is VetaleSearchHomePage homePage)
                    {
                        // Переходимо саме на результати пошуку?
                        if (url.StartsWith("vetale://search/results", StringComparison.OrdinalIgnoreCase))
                        {
                            var q = InternalUrlHandler.GetQueryParameter(url, "q");
                            if (!string.IsNullOrWhiteSpace(q))
                            {
                                var qSafe = q ?? string.Empty;
                                entry.Url = $"vetale://search?q={Uri.EscapeDataString(qSafe)}";
                                homePage.SetQuery(qSafe);
                            }
                        }
                    }
                }
                catch { }

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
            var activeWorker = _tabs.Active;
            if (activeWorker == null)
            {
                CreateNewTab(vetaleSearchUrl);
                // Оновлюємо іконку для нової вкладки
                await UpdateFaviconAsync(vetaleSearchUrl);
                Activate();
                Focus();
                return;
            }

            var home = new VetaleSearchHomePage();
            home.SetSuggestionsService(GlobalSuggestions);
            home.SetVoiceRecognitionService(VoiceRecognitionService);
            SubscribeToInternalPageEvents(home);

            // Навігуємо через History/TabWorker: подія OnWorkerNavigationChanged виставить UI
            activeWorker.Navigate(vetaleSearchUrl, home);
            
            // Оновлюємо іконку вкладки
            await UpdateFaviconAsync(vetaleSearchUrl);
        });
    }
}

