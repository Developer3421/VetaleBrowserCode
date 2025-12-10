using System;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Input;
using Avalonia.Controls.ApplicationLifetimes; // added for Windows enumeration
// for MainWindow
using VetaleBrowser.VetaleBrowser.UI.Pages;
using VetaleBrowser.VetaleBrowser.Database;
using VetaleBrowser.VetaleBrowser.Database.Services;
using Avalonia.Media;

namespace VetaleBrowser.VetaleBrowser.UI.Windows;

public partial class SettingsWindow : Window
{
    private ContentControl? _contentHost;
    private Grid? _topBarGrid;
    private Grid? _contentGrid;
    private SettingsMainPage? _mainPage;
    private ISettingsService? _settingsService;
    private IAppearanceSettingsService? _appearanceSettingsService;

    public SettingsWindow()
    {
        InitializeComponent();
        InitializeSettingsService();
        Loaded += OnLoaded;
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void InitializeSettingsService()
    {
        try
        {
            var config = DatabaseConfiguration.CreateDefault();
            _settingsService = new SettingsService(config.DatabasePath, config.EncryptionKey);
            
            // Initialize appearance settings service with separate database
            var appearanceDbPath = Path.Combine(
                Path.GetDirectoryName(config.DatabasePath) ?? "",
                "appearance_settings.db"
            );
            _appearanceSettingsService = new AppearanceSettingsService(appearanceDbPath, config.EncryptionKey);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"SettingsWindow: Error initializing settings service: {ex}");
        }
    }

    private async void OnLoaded(object? sender, RoutedEventArgs e)
    {
        _contentHost = this.FindControl<ContentControl>("PART_ContentHost");
        _topBarGrid = this.FindControl<Grid>("TopBarGrid");
        _contentGrid = this.FindControl<Grid>("ContentGrid");
        LoadMainPage();

        // Apply current appearance for this window ("Other windows" settings)
        await ApplyOwnAppearanceAsync();
    }

    private void LoadMainPage()
    {
        _mainPage = new SettingsMainPage();
        _mainPage.LanguageRequested += OnLanguageRequested;
        _mainPage.AppearanceRequested += OnAppearanceRequested;
        _mainPage.SearchEngineRequested += OnSearchEngineRequested;

        if (_contentHost != null)
        {
            _contentHost.Content = _mainPage;
        }

        // Resize window to fit main page
        ResizeWindowForPage(660, 500);
    }

    private void OnLanguageRequested(object? sender, EventArgs e)
    {
        try
        {
            var page = new LanguageSettingsPage();
            page.BackRequested += (_, _) => LoadMainPage();
            page.SettingsSaved += OnLanguageSettingsSaved;
            if (_contentHost != null)
            {
                _contentHost.Content = page;
            }
            ResizeWindowForPage(660, 360);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"SettingsWindow: Error loading language page: {ex}");
        }
    }

    private void OnLanguageSettingsSaved(object? sender, EventArgs e)
    {
        // After language change, we can refresh main page labels if currently shown
        // Here just keep current page; user can go back
        // Optionally, update window title binding by re-setting Title property
        this.Title = this.FindResource("Settings.Title")?.ToString() ?? this.Title;
    }

    private void OnAppearanceRequested(object? sender, EventArgs e)
    {
        if (_appearanceSettingsService == null)
        {
            System.Diagnostics.Trace.WriteLine("SettingsWindow: Appearance settings service not initialized");
            return;
        }

        try
        {
            var appearanceMainPage = new AppearanceMainPage();
            appearanceMainPage.TabSettingsRequested += OnTabSettingsRequested;
            appearanceMainPage.MainWindowSettingsRequested += OnMainWindowSettingsRequested;
            appearanceMainPage.OtherWindowsSettingsRequested += OnOtherWindowsSettingsRequested;

            if (_contentHost != null)
            {
                _contentHost.Content = appearanceMainPage;
            }

            // Resize window for appearance main page
            ResizeWindowForPage(660, 500);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"SettingsWindow: Error loading appearance page: {ex}");
        }
    }

    private void OnTabSettingsRequested(object? sender, EventArgs e)
    {
        if (_appearanceSettingsService == null) return;

        try
        {
            var tabSettingsPage = new TabAppearanceSettingsPage(_appearanceSettingsService);
            tabSettingsPage.BackRequested += OnAppearanceBackRequested;
            tabSettingsPage.SettingsSaved += OnAppearanceSettingsSaved;

            if (_contentHost != null)
            {
                _contentHost.Content = tabSettingsPage;
            }

            ResizeWindowForPage(660, 600);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"SettingsWindow: Error loading tab settings page: {ex}");
        }
    }

    private void OnMainWindowSettingsRequested(object? sender, EventArgs e)
    {
        if (_appearanceSettingsService == null) return;

        try
        {
            var mainWindowSettingsPage = new MainWindowAppearanceSettingsPage(_appearanceSettingsService);
            mainWindowSettingsPage.BackRequested += OnAppearanceBackRequested;
            mainWindowSettingsPage.SettingsSaved += OnAppearanceSettingsSaved;

            if (_contentHost != null)
            {
                _contentHost.Content = mainWindowSettingsPage;
            }

            ResizeWindowForPage(660, 600);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"SettingsWindow: Error loading main window settings page: {ex}");
        }
    }

    private void OnOtherWindowsSettingsRequested(object? sender, EventArgs e)
    {
        if (_appearanceSettingsService == null) return;

        try
        {
            var otherWindowsSettingsPage = new OtherWindowsAppearanceSettingsPage(_appearanceSettingsService);
            otherWindowsSettingsPage.BackRequested += OnAppearanceBackRequested;
            otherWindowsSettingsPage.SettingsSaved += OnAppearanceSettingsSaved;

            if (_contentHost != null)
            {
                _contentHost.Content = otherWindowsSettingsPage;
            }

            ResizeWindowForPage(660, 550);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"SettingsWindow: Error loading other windows settings page: {ex}");
        }
    }

    private void OnAppearanceBackRequested(object? sender, EventArgs e)
    {
        OnAppearanceRequested(null, EventArgs.Empty);
    }

    private async void OnAppearanceSettingsSaved(object? sender, EventArgs e)
    {
        System.Diagnostics.Debug.WriteLine("SettingsWindow: Appearance settings saved successfully");
        
        // 1) Apply to this SettingsWindow (Other Windows appearance)
        await ApplyOwnAppearanceAsync();

        // 2) Apply to any open MainWindow immediately
        try
        {
            var lifetime = Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
            if (lifetime != null)
            {
                foreach (var w in lifetime.Windows)
                {
                    if (w is MainWindow main)
                    {
                        _ = main.ApplyAppearanceSettingsFromStoreAsync();
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"SettingsWindow: Failed to apply appearance to main window(s): {ex}");
        }

        // 3) Apply to any open ToolsWindow immediately
        try
        {
            var lifetime = Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
            if (lifetime != null)
            {
                foreach (var w in lifetime.Windows)
                {
                    if (w is ToolsWindow tools)
                    {
                        _ = tools.ApplyOwnAppearanceAsync();
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"SettingsWindow: Failed to apply appearance to tools window(s): {ex}");
        }
    }

    private async System.Threading.Tasks.Task ApplyOwnAppearanceAsync()
    {
        try
        {
            if (_appearanceSettingsService == null)
                return;

            var bg = await _appearanceSettingsService.GetOtherWindowsBackgroundColorAsync();
            var top = await _appearanceSettingsService.GetOtherWindowsTopBarColorAsync();

            if (_contentGrid != null)
            {
                var b = TryParseBrush(bg);
                if (b != null) _contentGrid.Background = b;
            }
            if (_topBarGrid != null)
            {
                var t = TryParseBrush(top);
                if (t != null) _topBarGrid.Background = t; // else keep GrayGradient from XAML
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"SettingsWindow: ApplyOwnAppearanceAsync error: {ex}");
        }
    }

    private IBrush? TryParseBrush(string? color)
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

    private void OnSearchEngineRequested(object? sender, EventArgs e)
    {
        if (_settingsService == null)
        {
            System.Diagnostics.Debug.WriteLine("SettingsWindow: Settings service not initialized");
            return;
        }

        try
        {
            var searchEnginePage = new SearchEngineSettingsPage(_settingsService);
            searchEnginePage.BackRequested += OnSearchEngineBackRequested;
            searchEnginePage.SettingsSaved += OnSearchEngineSettingsSaved;
            searchEnginePage.NavigateRequested += OnSearchEngineNavigateRequested;

            if (_contentHost != null)
            {
                _contentHost.Content = searchEnginePage;
            }

            // Resize window for search engine page
            ResizeWindowForPage(660, 600);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"SettingsWindow: Error loading search engine page: {ex}");
        }
    }

    private void OnSearchEngineNavigateRequested(object? sender, string url)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine($"SettingsWindow: Navigate requested to: {url}");
            
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var mainWindow = desktop.Windows.OfType<MainWindow>().FirstOrDefault();
                if (mainWindow != null)
                {
                    // Якщо явно просять відкрити домашню сторінку Vetale Search — використовуємо спеціальний метод
                    if (string.Equals(url, "vetale://search", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(url, "vetale://search/home", StringComparison.OrdinalIgnoreCase))
                    {
                        mainWindow.OpenVetaleSearchInCurrentTab();
                    }
                    else
                    {
                        // Для інших URL (у т.ч. vetale://search/results) використовуємо стандартну навігацію поточної вкладки
                        mainWindow.NavigateCurrentTabToUrl(url);
                    }

                    // Закрити вікно налаштувань
                    Close();
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"SettingsWindow: Error navigating: {ex}");
        }
    }

    private void OnSearchEngineBackRequested(object? sender, EventArgs e)
    {
        LoadMainPage();
    }

    private async void OnSearchEngineSettingsSaved(object? sender, EventArgs e)
    {
        System.Diagnostics.Debug.WriteLine("SettingsWindow: Search engine settings saved successfully");
        
        // Try to find an existing MainWindow and navigate it to the selected search engine home
        try
        {
            var lifetime = Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
            if (lifetime != null)
            {
                foreach (var w in lifetime.Windows)
                {
                    if (w is MainWindow main)
                    {
                        await main.NavigateToSelectedSearchHomeAsync();
                        
                        // Активувати головне вікно
                        main.Activate();
                        main.Focus();
                        
                        // Закрити вікно налаштувань після успішного оновлення
                        Close();
                        break;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"SettingsWindow: Failed to auto-navigate main window: {ex}");
        }
    }

    private void ResizeWindowForPage(double width, double height)
    {
        // Animate window size change
        Width = width;
        Height = height;
        MinWidth = width;
        MinHeight = height;
    }

    private void MinimizeWindow(object? sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void MaximizeWindow(object? sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    private void CloseWindow(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private void TopBar_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginMoveDrag(e);
        }
    }

    private void TopBar_DoubleTapped(object? sender, RoutedEventArgs e)
    {
        // Maximize disabled for secondary windows
    }

    private void OpenMainWindow(object? sender, RoutedEventArgs e)
    {
        // Open another instance of MainWindow so user can return or place it on another monitor
        var wnd = new MainWindow();
        wnd.Show();
    }
}
