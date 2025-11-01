using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Input;
using VetaleBrowser;
using VetaleBrowser.VetaleBrowser.UI.Pages;

namespace VetaleBrowser.VetaleBrowser.UI.Windows;

public partial class ToolsWindow : Window
{
    private Grid? _contentArea;
    private ToolsMainPage? _mainPage;
    private ToolsWebViewPage? _webViewPage;

    public ToolsWindow()
    {
        InitializeComponent();
        _contentArea = this.FindControl<Grid>("ContentArea");
        InitializePages();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void InitializePages()
    {
        // Create main page
        _mainPage = new ToolsMainPage();
        _mainPage.NavigateInWebView += OnNavigateInWebView;
        _mainPage.NavigateInMainTab += OnNavigateInMainTab;

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

    private void OnNavigateInMainTab(object? sender, string url)
    {
        // Open URL in main browser window
        System.Diagnostics.Debug.WriteLine($"[ToolsWindow] Navigate in main tab: {url}");
        
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

        // TODO: Navigate to URL in active tab
        // This requires access to MainWindow's navigation methods
        mainWindow.Activate();
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
        MaximizeWindow(sender, e);
    }

    private void OpenMainWindow(object? sender, RoutedEventArgs e)
    {
        var wnd = new MainWindow();
        wnd.Show();
    }
}

