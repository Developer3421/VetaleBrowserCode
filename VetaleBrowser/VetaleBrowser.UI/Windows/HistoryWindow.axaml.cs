using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using VetaleBrowser.VetaleBrowser.UI.Pages;
using VetaleBrowser.VetaleBrowser.Database.Services;

namespace VetaleBrowser.VetaleBrowser.UI.Windows;

public partial class HistoryWindow : Window
{
    private ContentControl? _contentHost;
    private HistoryPage? _historyPage;
    private IHistoryDatabaseService? _historyService;

    public HistoryWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Closing += OnWindowClosing;
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        _contentHost = this.FindControl<ContentControl>("PART_ContentHost");
        ShowHistoryPage();
    }

    private void OnWindowClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        // Очищаємо ресурси при закритті вікна
        Cleanup();
    }

    public void SetHistoryService(IHistoryDatabaseService historyService)
    {
        _historyService = historyService;
        if (_historyPage != null)
        {
            _historyPage.SetHistoryService(historyService);
        }
    }

    private void ShowHistoryPage()
    {
        _historyPage = new HistoryPage();
        
        if (_historyService != null)
        {
            _historyPage.SetHistoryService(_historyService);
        }
        
        if (_contentHost != null)
        {
            _contentHost.Content = _historyPage;
        }
    }

    private void TopBar_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginMoveDrag(e);
        }
    }

    private void TopBar_DoubleTapped(object? sender, TappedEventArgs e)
    {
        // Maximize disabled for secondary windows
    }

    private void MinimizeWindow(object? sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void MaximizeWindow(object? sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized 
            ? WindowState.Normal 
            : WindowState.Maximized;
    }

    private void CloseWindow(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private void Cleanup()
    {
        // Відписуємося від подій
        Loaded -= OnLoaded;
        Closing -= OnWindowClosing;

        // Очищаємо ContentControl
        if (_contentHost != null)
        {
            _contentHost.Content = null;
        }

        // Очищаємо HistoryPage
        if (_historyPage != null)
        {
            _historyPage = null;
        }

        // Очищаємо сервіси
        _historyService = null;
        _contentHost = null;
    }

    private void OpenMainWindow(object? sender, RoutedEventArgs e)
    {
        // Знаходимо головне вікно
        foreach (var window in ((Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)
            Avalonia.Application.Current!.ApplicationLifetime!).Windows)
        {
            if (window is MainWindow mainWindow)
            {
                mainWindow.Activate();
                return;
            }
        }
    }
}

