using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using VetaleBrowser.VetaleBrowser.Database.Services;

namespace VetaleBrowser.VetaleBrowser.UI.Pages;

public partial class MainWindowAppearanceSettingsPage : UserControl
{
    public event EventHandler? BackRequested;
    public event EventHandler? SettingsSaved;

    private TextBox? _navigationBarColorTextBox;
    private TextBox? _navigationBarHeightTextBox;
    private TextBox? _mainWindowWidthTextBox;
    private TextBox? _mainWindowHeightTextBox;
    private TextBox? _topBarBackgroundColorTextBox;
    private TextBox? _buttonSizeTextBox;
    private TextBox? _buttonIconSizeTextBox;

    private readonly IAppearanceSettingsService _appearanceSettingsService;
    private bool _isLoading = true;

    public MainWindowAppearanceSettingsPage() : this(null!)
    {
    }

    public MainWindowAppearanceSettingsPage(IAppearanceSettingsService appearanceSettingsService)
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
        _navigationBarColorTextBox = this.FindControl<TextBox>("NavigationBarColorTextBox");
        _navigationBarHeightTextBox = this.FindControl<TextBox>("NavigationBarHeightTextBox");
        _mainWindowWidthTextBox = this.FindControl<TextBox>("MainWindowWidthTextBox");
        _mainWindowHeightTextBox = this.FindControl<TextBox>("MainWindowHeightTextBox");
        _topBarBackgroundColorTextBox = this.FindControl<TextBox>("TopBarBackgroundColorTextBox");
        _buttonSizeTextBox = this.FindControl<TextBox>("ButtonSizeTextBox");
        _buttonIconSizeTextBox = this.FindControl<TextBox>("ButtonIconSizeTextBox");

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
            var navBarColor = await _appearanceSettingsService.GetNavigationBarColorAsync();
            var navBarHeight = await _appearanceSettingsService.GetNavigationBarHeightAsync();
            var windowWidth = await _appearanceSettingsService.GetMainWindowWidthAsync();
            var windowHeight = await _appearanceSettingsService.GetMainWindowHeightAsync();
            var topBarColor = await _appearanceSettingsService.GetTopBarBackgroundColorAsync();
            var buttonSize = await _appearanceSettingsService.GetButtonSizeAsync();
            var buttonIconSize = await _appearanceSettingsService.GetButtonIconSizeAsync();

            if (_navigationBarColorTextBox != null)
                _navigationBarColorTextBox.Text = navBarColor;
            
            if (_navigationBarHeightTextBox != null)
                _navigationBarHeightTextBox.Text = navBarHeight.ToString();
            
            if (_mainWindowWidthTextBox != null)
                _mainWindowWidthTextBox.Text = windowWidth.ToString();
            
            if (_mainWindowHeightTextBox != null)
                _mainWindowHeightTextBox.Text = windowHeight.ToString();
            
            if (_topBarBackgroundColorTextBox != null)
                _topBarBackgroundColorTextBox.Text = topBarColor;
            
            if (_buttonSizeTextBox != null)
                _buttonSizeTextBox.Text = buttonSize.ToString();
            
            if (_buttonIconSizeTextBox != null)
                _buttonIconSizeTextBox.Text = buttonIconSize.ToString();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading main window appearance settings: {ex.Message}");
        }
    }

    private async void OnSaveClick(object? sender, RoutedEventArgs e)
    {
        if (_appearanceSettingsService == null || _isLoading)
            return;

        try
        {
            // Save Navigation Bar Color
            if (_navigationBarColorTextBox != null && !string.IsNullOrWhiteSpace(_navigationBarColorTextBox.Text))
            {
                await _appearanceSettingsService.SetNavigationBarColorAsync(_navigationBarColorTextBox.Text);
            }

            // Save Navigation Bar Height
            if (_navigationBarHeightTextBox != null && !string.IsNullOrWhiteSpace(_navigationBarHeightTextBox.Text))
            {
                if (double.TryParse(_navigationBarHeightTextBox.Text, out var height))
                {
                    await _appearanceSettingsService.SetNavigationBarHeightAsync(height);
                }
            }

            // Save Main Window Width
            if (_mainWindowWidthTextBox != null && !string.IsNullOrWhiteSpace(_mainWindowWidthTextBox.Text))
            {
                if (double.TryParse(_mainWindowWidthTextBox.Text, out var width))
                {
                    await _appearanceSettingsService.SetMainWindowWidthAsync(width);
                }
            }

            // Save Main Window Height
            if (_mainWindowHeightTextBox != null && !string.IsNullOrWhiteSpace(_mainWindowHeightTextBox.Text))
            {
                if (double.TryParse(_mainWindowHeightTextBox.Text, out var height))
                {
                    await _appearanceSettingsService.SetMainWindowHeightAsync(height);
                }
            }

            // Save Top Bar Background Color
            if (_topBarBackgroundColorTextBox != null && !string.IsNullOrWhiteSpace(_topBarBackgroundColorTextBox.Text))
            {
                await _appearanceSettingsService.SetTopBarBackgroundColorAsync(_topBarBackgroundColorTextBox.Text);
            }

            // Save Button Size
            if (_buttonSizeTextBox != null && !string.IsNullOrWhiteSpace(_buttonSizeTextBox.Text))
            {
                if (double.TryParse(_buttonSizeTextBox.Text, out var size))
                {
                    await _appearanceSettingsService.SetButtonSizeAsync(size);
                }
            }

            // Save Button Icon Size
            if (_buttonIconSizeTextBox != null && !string.IsNullOrWhiteSpace(_buttonIconSizeTextBox.Text))
            {
                if (double.TryParse(_buttonIconSizeTextBox.Text, out var iconSize))
                {
                    await _appearanceSettingsService.SetButtonIconSizeAsync(iconSize);
                }
            }

            SettingsSaved?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving main window appearance settings: {ex.Message}");
        }
    }

    private async void OnResetClick(object? sender, RoutedEventArgs e)
    {
        if (_appearanceSettingsService == null)
            return;

        try
        {
            // Reset to default values
            await _appearanceSettingsService.SetNavigationBarColorAsync("#E0E0E0");
            await _appearanceSettingsService.SetNavigationBarHeightAsync(40.0);
            await _appearanceSettingsService.SetMainWindowWidthAsync(1200.0);
            await _appearanceSettingsService.SetMainWindowHeightAsync(800.0);
            await _appearanceSettingsService.SetTopBarBackgroundColorAsync("#8B4513");
            await _appearanceSettingsService.SetButtonSizeAsync(32.0);
            await _appearanceSettingsService.SetButtonIconSizeAsync(16.0);

            // Reload settings to UI
            await LoadSettingsAsync();

            SettingsSaved?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error resetting main window appearance settings: {ex.Message}");
        }
    }

    private void OnBackClick(object? sender, RoutedEventArgs e)
    {
        BackRequested?.Invoke(this, EventArgs.Empty);
    }
}

