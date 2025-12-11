using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace VetaleBrowser.VetaleBrowser.UI.Pages;

public partial class AppearanceMainPage : UserControl
{
    public event EventHandler? TabSettingsRequested;
    public event EventHandler? MainWindowSettingsRequested;
    public event EventHandler? OtherWindowsSettingsRequested;
    public event EventHandler? BackRequested;

    public AppearanceMainPage()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void OnTabSettingsClick(object? sender, RoutedEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine("[AppearanceMainPage] OnTabSettingsClick called");
        TabSettingsRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnMainWindowSettingsClick(object? sender, RoutedEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine("[AppearanceMainPage] OnMainWindowSettingsClick called");
        MainWindowSettingsRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnOtherWindowsSettingsClick(object? sender, RoutedEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine("[AppearanceMainPage] OnOtherWindowsSettingsClick called");
        OtherWindowsSettingsRequested?.Invoke(this, EventArgs.Empty);
    }
    
    private void OnBackClick(object? sender, RoutedEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine("[AppearanceMainPage] OnBackClick called");
        BackRequested?.Invoke(this, EventArgs.Empty);
    }
}

