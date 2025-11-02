using System;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Input;
using VetaleBrowser; // for MainWindow
using VetaleBrowser.VetaleBrowser.UI.Pages;
using VetaleBrowser.VetaleBrowser.Database;
using VetaleBrowser.VetaleBrowser.Database.Services;

namespace VetaleBrowser.VetaleBrowser.UI.Windows;

public partial class SettingsWindow : Window
{
    private ContentControl? _contentHost;
    private SettingsMainPage? _mainPage;
    private ISettingsService? _settingsService;

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
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"SettingsWindow: Error initializing settings service: {ex}");
        }
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        _contentHost = this.FindControl<ContentControl>("PART_ContentHost");
        LoadMainPage();
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
        // TODO: Load language settings page
    }

    private void OnAppearanceRequested(object? sender, EventArgs e)
    {
        // TODO: Load appearance settings page
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

    private void OnSearchEngineBackRequested(object? sender, EventArgs e)
    {
        LoadMainPage();
    }

    private void OnSearchEngineSettingsSaved(object? sender, EventArgs e)
    {
        System.Diagnostics.Debug.WriteLine("SettingsWindow: Search engine settings saved successfully");
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
        MaximizeWindow(sender, e);
    }

    private void OpenMainWindow(object? sender, RoutedEventArgs e)
    {
        // Open another instance of MainWindow so user can return or place it on another monitor
        var wnd = new MainWindow();
        wnd.Show();
    }
}
