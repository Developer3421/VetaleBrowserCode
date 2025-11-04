using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Input;
using Avalonia.Controls.ApplicationLifetimes;
using VetaleBrowser;
using VetaleBrowser.VetaleBrowser.UI.Pages;

namespace VetaleBrowser.VetaleBrowser.UI.Windows;

public partial class VetaleAIWindow : Window
{
    private ContentControl? _contentHost;
    private Grid? _topBarGrid;
    private Grid? _contentGrid;
    private VetaleAIChatPage? _chatPage;

    public VetaleAIWindow()
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
        _topBarGrid = this.FindControl<Grid>("TopBarGrid");
        _contentGrid = this.FindControl<Grid>("ContentGrid");
        LoadChatPage();
    }

    private void LoadChatPage()
    {
        _chatPage = new VetaleAIChatPage();

        if (_contentHost != null)
        {
            _contentHost.Content = _chatPage;
        }
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
        WindowState = WindowState == WindowState.Maximized 
            ? WindowState.Normal 
            : WindowState.Maximized;
    }

    private void MinimizeWindow(object? sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void CloseWindow(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OpenMainWindow(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var mainWindow = desktop.Windows
                    .OfType<MainWindow>()
                    .FirstOrDefault();

                if (mainWindow != null)
                {
                    mainWindow.Activate();
                    mainWindow.WindowState = WindowState.Normal;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"VetaleAIWindow: Error opening main window: {ex}");
        }
    }
}

