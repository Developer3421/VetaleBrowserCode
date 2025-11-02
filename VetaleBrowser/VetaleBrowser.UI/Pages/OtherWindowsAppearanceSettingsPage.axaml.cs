using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using VetaleBrowser.VetaleBrowser.Database.Services;

namespace VetaleBrowser.VetaleBrowser.UI.Pages;

public partial class OtherWindowsAppearanceSettingsPage : UserControl
{
    public event EventHandler? BackRequested;
    public event EventHandler? SettingsSaved;

    private TextBox? _otherWindowsBackgroundColorTextBox;
    private TextBox? _otherWindowsTopBarColorTextBox;

    private readonly IAppearanceSettingsService _appearanceSettingsService;
    private bool _isLoading = true;

    public OtherWindowsAppearanceSettingsPage() : this(null!)
    {
    }

    public OtherWindowsAppearanceSettingsPage(IAppearanceSettingsService appearanceSettingsService)
    {
        _appearanceSettingsService = appearanceSettingsService;
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private async void OnLoaded(object? sender, RoutedEventArgs e)
    {
        _otherWindowsBackgroundColorTextBox = this.FindControl<TextBox>("OtherWindowsBackgroundColorTextBox");
        _otherWindowsTopBarColorTextBox = this.FindControl<TextBox>("OtherWindowsTopBarColorTextBox");

        if (_appearanceSettingsService != null)
        {
            await LoadSettingsAsync();
        }

        _isLoading = false;
    }

    private async System.Threading.Tasks.Task LoadSettingsAsync()
    {
        try
        {
            var backgroundColor = await _appearanceSettingsService.GetOtherWindowsBackgroundColorAsync();
            var topBarColor = await _appearanceSettingsService.GetOtherWindowsTopBarColorAsync();

            if (_otherWindowsBackgroundColorTextBox != null)
                _otherWindowsBackgroundColorTextBox.Text = backgroundColor;
            
            if (_otherWindowsTopBarColorTextBox != null)
                _otherWindowsTopBarColorTextBox.Text = topBarColor;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading other windows appearance settings: {ex.Message}");
        }
    }

    private async void OnSaveClick(object? sender, RoutedEventArgs e)
    {
        if (_appearanceSettingsService == null || _isLoading)
            return;

        try
        {
            // Save Other Windows Background Color
            if (_otherWindowsBackgroundColorTextBox != null && !string.IsNullOrWhiteSpace(_otherWindowsBackgroundColorTextBox.Text))
            {
                await _appearanceSettingsService.SetOtherWindowsBackgroundColorAsync(_otherWindowsBackgroundColorTextBox.Text);
            }

            // Save Other Windows Top Bar Color
            if (_otherWindowsTopBarColorTextBox != null && !string.IsNullOrWhiteSpace(_otherWindowsTopBarColorTextBox.Text))
            {
                await _appearanceSettingsService.SetOtherWindowsTopBarColorAsync(_otherWindowsTopBarColorTextBox.Text);
            }

            SettingsSaved?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving other windows appearance settings: {ex.Message}");
        }
    }

    private async void OnResetClick(object? sender, RoutedEventArgs e)
    {
        if (_appearanceSettingsService == null)
            return;

        try
        {
            // Reset to default values
            await _appearanceSettingsService.SetOtherWindowsBackgroundColorAsync("#FFFFFF");
            await _appearanceSettingsService.SetOtherWindowsTopBarColorAsync("#9A1CE8");

            // Reload settings to UI
            await LoadSettingsAsync();

            SettingsSaved?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error resetting other windows appearance settings: {ex.Message}");
        }
    }

    private void OnBackClick(object? sender, RoutedEventArgs e)
    {
        BackRequested?.Invoke(this, EventArgs.Empty);
    }
}

