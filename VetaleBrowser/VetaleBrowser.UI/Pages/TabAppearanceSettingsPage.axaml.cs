using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using VetaleBrowser.VetaleBrowser.Database.Services;

namespace VetaleBrowser.VetaleBrowser.UI.Pages;

public partial class TabAppearanceSettingsPage : UserControl
{
    public event EventHandler? BackRequested;
    public event EventHandler? SettingsSaved;

    private TextBox? _tabWidthTextBox;
    private TextBox? _tabElementSizeTextBox;
    private TextBox? _tabIconSizeTextBox;
    private TextBox? _tabBackgroundColorTextBox;
    private TextBox? _tabTextColorTextBox;
    private TextBox? _tabActiveColorTextBox;

    private readonly IAppearanceSettingsService _appearanceSettingsService;
    private bool _isLoading = true;

    public TabAppearanceSettingsPage() : this(null!)
    {
    }

    public TabAppearanceSettingsPage(IAppearanceSettingsService appearanceSettingsService)
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
        _tabWidthTextBox = this.FindControl<TextBox>("TabWidthTextBox");
        _tabElementSizeTextBox = this.FindControl<TextBox>("TabElementSizeTextBox");
        _tabIconSizeTextBox = this.FindControl<TextBox>("TabIconSizeTextBox");
        _tabBackgroundColorTextBox = this.FindControl<TextBox>("TabBackgroundColorTextBox");
        _tabTextColorTextBox = this.FindControl<TextBox>("TabTextColorTextBox");
        _tabActiveColorTextBox = this.FindControl<TextBox>("TabActiveColorTextBox");

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
            var tabWidth = await _appearanceSettingsService.GetTabWidthAsync();
            var tabElementSize = await _appearanceSettingsService.GetTabElementSizeAsync();
            var tabIconSize = await _appearanceSettingsService.GetTabIconSizeAsync();
            var tabBackgroundColor = await _appearanceSettingsService.GetTabBackgroundColorAsync();
            var tabTextColor = await _appearanceSettingsService.GetTabTextColorAsync();
            var tabActiveColor = await _appearanceSettingsService.GetTabActiveColorAsync();

            if (_tabWidthTextBox != null)
                _tabWidthTextBox.Text = tabWidth.ToString();
            
            if (_tabElementSizeTextBox != null)
                _tabElementSizeTextBox.Text = tabElementSize.ToString();
            
            if (_tabIconSizeTextBox != null)
                _tabIconSizeTextBox.Text = tabIconSize.ToString();
            
            if (_tabBackgroundColorTextBox != null)
                _tabBackgroundColorTextBox.Text = tabBackgroundColor;
            
            if (_tabTextColorTextBox != null)
                _tabTextColorTextBox.Text = tabTextColor;
            
            if (_tabActiveColorTextBox != null)
                _tabActiveColorTextBox.Text = tabActiveColor;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading tab appearance settings: {ex.Message}");
        }
    }

    private async void OnSaveClick(object? sender, RoutedEventArgs e)
    {
        if (_appearanceSettingsService == null || _isLoading)
            return;

        try
        {
            // Save Tab Width
            if (_tabWidthTextBox != null && !string.IsNullOrWhiteSpace(_tabWidthTextBox.Text))
            {
                if (double.TryParse(_tabWidthTextBox.Text, out var tabWidth))
                {
                    await _appearanceSettingsService.SetTabWidthAsync(tabWidth);
                }
            }

            // Save Tab Element Size
            if (_tabElementSizeTextBox != null && !string.IsNullOrWhiteSpace(_tabElementSizeTextBox.Text))
            {
                if (double.TryParse(_tabElementSizeTextBox.Text, out var tabElementSize))
                {
                    await _appearanceSettingsService.SetTabElementSizeAsync(tabElementSize);
                }
            }

            // Save Tab Icon Size
            if (_tabIconSizeTextBox != null && !string.IsNullOrWhiteSpace(_tabIconSizeTextBox.Text))
            {
                if (double.TryParse(_tabIconSizeTextBox.Text, out var tabIconSize))
                {
                    await _appearanceSettingsService.SetTabIconSizeAsync(tabIconSize);
                }
            }

            // Save Tab Background Color
            if (_tabBackgroundColorTextBox != null && !string.IsNullOrWhiteSpace(_tabBackgroundColorTextBox.Text))
            {
                await _appearanceSettingsService.SetTabBackgroundColorAsync(_tabBackgroundColorTextBox.Text);
            }

            // Save Tab Text Color
            if (_tabTextColorTextBox != null && !string.IsNullOrWhiteSpace(_tabTextColorTextBox.Text))
            {
                await _appearanceSettingsService.SetTabTextColorAsync(_tabTextColorTextBox.Text);
            }

            // Save Tab Active Color
            if (_tabActiveColorTextBox != null && !string.IsNullOrWhiteSpace(_tabActiveColorTextBox.Text))
            {
                await _appearanceSettingsService.SetTabActiveColorAsync(_tabActiveColorTextBox.Text);
            }

            SettingsSaved?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving tab appearance settings: {ex.Message}");
        }
    }

    private async void OnResetClick(object? sender, RoutedEventArgs e)
    {
        if (_appearanceSettingsService == null)
            return;

        try
        {
            // Reset to default values
            await _appearanceSettingsService.SetTabWidthAsync(200.0);
            await _appearanceSettingsService.SetTabElementSizeAsync(16.0);
            await _appearanceSettingsService.SetTabIconSizeAsync(16.0);
            await _appearanceSettingsService.SetTabBackgroundColorAsync("#F5F5F5");
            await _appearanceSettingsService.SetTabTextColorAsync("#000000");
            await _appearanceSettingsService.SetTabActiveColorAsync("#9A1CE8");

            // Reload settings to UI
            await LoadSettingsAsync();

            SettingsSaved?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error resetting tab appearance settings: {ex.Message}");
        }
    }

    private void OnBackClick(object? sender, RoutedEventArgs e)
    {
        BackRequested?.Invoke(this, EventArgs.Empty);
    }
}

