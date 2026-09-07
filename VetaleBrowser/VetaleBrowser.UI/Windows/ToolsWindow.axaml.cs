using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Input;
using VetaleBrowser.VetaleBrowser.UI.Pages;
using VetaleBrowser.VetaleBrowser.Database;
using VetaleBrowser.VetaleBrowser.Database.Services;
using Avalonia.Media;

namespace VetaleBrowser.VetaleBrowser.UI.Windows;

public partial class ToolsWindow : Window
{
    private Grid? _contentArea;
    private Border? _topBarGrid;
    private ToolsMainPage? _mainPage;
    private ToolsWebViewPage? _webViewPage;
    private IAppearanceSettingsService? _appearanceSettingsService;

    public ToolsWindow()
    {
        InitializeComponent();
        _contentArea = this.FindControl<Grid>("ContentArea");
        _topBarGrid = this.FindControl<Border>("TopBarGrid");
        InitializeAppearanceService();
        Loaded += OnLoaded;
        InitializePages();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void InitializeAppearanceService()
    {
        try
        {
            var cfg = DatabaseConfiguration.CreateDefault();
            var path = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(cfg.DatabasePath) ?? string.Empty, "appearance_settings.db");
            _appearanceSettingsService = new AppearanceSettingsService(path, cfg.EncryptionKey);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"ToolsWindow: InitializeAppearanceService error: {ex}");
        }
    }

    private async void OnLoaded(object? sender, RoutedEventArgs e)
    {
        await ApplyOwnAppearanceAsync();
    }

    public async System.Threading.Tasks.Task ApplyOwnAppearanceAsync()
    {
        try
        {
            if (_appearanceSettingsService == null) return;
            var bg = await _appearanceSettingsService.GetOtherWindowsBackgroundColorAsync();
            var top = await _appearanceSettingsService.GetOtherWindowsTopBarColorAsync();

            if (_contentArea != null)
            {
                var b = TryParseBrush(bg);
                if (b != null) _contentArea.Background = b;
            }
            if (_topBarGrid != null)
            {
                var t = TryParseBrush(top);
                if (t != null) _topBarGrid.Background = t; // else keep GrayGradient from XAML
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"ToolsWindow: ApplyOwnAppearanceAsync error: {ex}");
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

    private void InitializePages()
    {
        // Create main page
        _mainPage = new ToolsMainPage();
        _mainPage.NavigateInWebView += OnNavigateInWebView;
        _mainPage.NavigateInMainTab += OnNavigateInMainTab;
        _mainPage.OpenInNewMainTab += OnOpenInNewMainTab;

        // Show main page initially
        ShowMainPage();
    }

    private void ShowMainPage()
    {
        if (_contentArea == null || _mainPage == null) return;

        _contentArea.Children.Clear();
        _contentArea.Children.Add(_mainPage);
    }

    private void ShowWebViewPage(string toolName, string url)
    {
        if (_contentArea == null) return;

        // Dispose old WebView page if exists
        if (_webViewPage != null)
        {
            _webViewPage.BackRequested -= OnWebViewBackRequested;
            _webViewPage = null;
        }

        // Create new WebView page
        _webViewPage = new ToolsWebViewPage();
        _webViewPage.BackRequested += OnWebViewBackRequested;
        _webViewPage.LoadTool(toolName, url);

        _contentArea.Children.Clear();
        _contentArea.Children.Add(_webViewPage);
    }

    private void OnNavigateInWebView(object? sender, ToolNavigationEventArgs e)
    {
        ShowWebViewPage(e.ToolName, e.Url);
    }

    private void OnOpenInNewMainTab(object? sender, string url)
    {
        // Open URL in a NEW tab of the main browser window
        System.Diagnostics.Trace.WriteLine($"[ToolsWindow] Open in new main tab: {url}");

        MainWindow? mainWindow = null;
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            mainWindow = desktop.Windows?.OfType<MainWindow>().FirstOrDefault();
        }

        if (mainWindow == null)
        {
            mainWindow = new MainWindow();
            mainWindow.Show();
        }

        if (mainWindow.WindowState == WindowState.Minimized)
        {
            mainWindow.WindowState = WindowState.Normal;
        }

        try
        {
            mainWindow.OpenUrlInNewTab(url);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"[ToolsWindow] Failed to open new tab: {ex}");
        }

        mainWindow.Activate();
        mainWindow.Topmost = true;
        mainWindow.Topmost = false;
    }

    private void OnNavigateInMainTab(object? sender, string url)
    {
        // Open URL in main browser window
        System.Diagnostics.Trace.WriteLine($"[ToolsWindow] Navigate in main tab: {url}");
        
        // Try to find existing main window
        MainWindow? mainWindow = null;
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            mainWindow = desktop.Windows?.OfType<MainWindow>().FirstOrDefault();
        }
        
        if (mainWindow == null)
        {
            // Create new main window if none exists
            mainWindow = new MainWindow();
            mainWindow.Show();
        }

        // If minimized, restore first
        if (mainWindow.WindowState == WindowState.Minimized)
        {
            mainWindow.WindowState = WindowState.Normal;
        }

        // Navigate to URL in active tab (or create one if none)
        try
        {
            mainWindow.NavigateUrlInActiveTab(url, openInNewTabIfNone: true);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"[ToolsWindow] Failed to navigate main window: {ex}");
        }

        // Bring main window to foreground
        mainWindow.Activate();
        mainWindow.Topmost = true;
        mainWindow.Topmost = false;
    }

    private void OnWebViewBackRequested(object? sender, EventArgs e)
    {
        ShowMainPage();
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
        var wnd = new MainWindow();
        wnd.Show();
    }
}
