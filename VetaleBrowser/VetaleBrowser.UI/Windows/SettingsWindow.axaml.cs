using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Input;
using VetaleBrowser; // for MainWindow
using VetaleBrowser.VetaleBrowser.UI.Pages;

namespace VetaleBrowser.VetaleBrowser.UI.Windows;

public partial class SettingsWindow : Window
{
    private ContentControl? _contentHost;
    private SettingsMainPage? _mainPage;

    public SettingsWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
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
        // TODO: Load search engine settings page
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
