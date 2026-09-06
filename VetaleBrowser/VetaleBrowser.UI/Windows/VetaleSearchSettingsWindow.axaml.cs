using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using VetaleBrowser.VetaleBrowser.UI.Pages;
using VetaleBrowser.VetaleBrowser.UI.Theme;

namespace VetaleBrowser.VetaleBrowser.UI.Windows;

/// <summary>
/// Window for Vetale Search settings - API keys and appearance
/// </summary>
public partial class VetaleSearchSettingsWindow : Window
{
    private ContentControl? _contentHost;
    private VetaleSearchSettingsPage? _settingsPage;

    public VetaleSearchSettingsWindow()
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
        _contentHost = this.FindControl<ContentControl>("ContentHost");
        LoadSettingsPage();
    }

    private void LoadSettingsPage()
    {
        if (_contentHost == null) return;

        _settingsPage = new VetaleSearchSettingsPage();
        _settingsPage.BackRequested += (_, _) => Close();
        _settingsPage.SettingsSaved += (_, _) =>
        {
            System.Diagnostics.Debug.WriteLine("[VetaleSearchSettingsWindow] Settings saved");
            VetaleSearchThemeManager.NotifyThemeChanged();
        };

        _contentHost.Content = _settingsPage;
    }

    private void TopBar_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginMoveDrag(e);
        }
    }

    private void TopBar_DoubleTapped(object? sender, Avalonia.Input.TappedEventArgs e)
    {
        // Maximize disabled for secondary windows
    }

    private void MinimizeWindow(object? sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void OpenMainWindow(object? sender, RoutedEventArgs e)
    {
        // Find the main window
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

    private void CloseWindow(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
