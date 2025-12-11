using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace VetaleBrowser.VetaleBrowser.UI.Pages;

public partial class SettingsMainPage : UserControl
{
    public event EventHandler? LanguageRequested;
    public event EventHandler? AppearanceRequested;
    public event EventHandler? SearchEngineRequested;

    public SettingsMainPage()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void OnLanguageClick(object? sender, RoutedEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine("[SettingsMainPage] OnLanguageClick called");
        LanguageRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnAppearanceClick(object? sender, RoutedEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine("[SettingsMainPage] OnAppearanceClick called");
        AppearanceRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnSearchEngineClick(object? sender, RoutedEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine("[SettingsMainPage] OnSearchEngineClick called");
        SearchEngineRequested?.Invoke(this, EventArgs.Empty);
    }
}
