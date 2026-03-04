using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using VetaleBrowser.VetaleBrowser.Database;
using VetaleBrowser.VetaleBrowser.Database.Services;

namespace VetaleBrowser.VetaleBrowser.UI.Pages;

public partial class OtherWindowsAppearanceSettingsPage : UserControl
{
    public event EventHandler? BackRequested;
    public event EventHandler? SettingsSaved;

    private TextBox? _otherWindowsBackgroundColorTextBox;
    private TextBox? _otherWindowsTopBarColorTextBox;

    private readonly IAppearanceSettingsService? _appearanceSettingsService;
    private bool _isLoading = true;

    public OtherWindowsAppearanceSettingsPage() : this(null)
    {
    }

    public OtherWindowsAppearanceSettingsPage(IAppearanceSettingsService? appearanceSettingsService)
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
            // Default values
            string backgroundColor = "#FFFFFF";
            string topBarColor = "";
            
            if (_appearanceSettingsService != null)
            {
                try
                {
                    backgroundColor = await _appearanceSettingsService.GetOtherWindowsBackgroundColorAsync();
                    topBarColor = await _appearanceSettingsService.GetOtherWindowsTopBarColorAsync();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[OtherWindowsAppearanceSettingsPage] Error loading from DB: {ex.Message}");
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("[OtherWindowsAppearanceSettingsPage] AppearanceSettingsService is null, using defaults");
            }

            if (_otherWindowsBackgroundColorTextBox != null)
                _otherWindowsBackgroundColorTextBox.Text = backgroundColor;
            
            if (_otherWindowsTopBarColorTextBox != null)
                _otherWindowsTopBarColorTextBox.Text = topBarColor;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading other windows appearance settings: {ex.Message}");
        }
    }

    private async void OnSaveClick(object? sender, RoutedEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine("[OtherWindowsAppearanceSettingsPage] OnSaveClick called");
        
        if (_isLoading)
        {
            System.Diagnostics.Debug.WriteLine("[OtherWindowsAppearanceSettingsPage] Still loading, skipping save");
            return;
        }
        
        // If service is null - try to create our own
        var service = _appearanceSettingsService;
        if (service == null)
        {
            System.Diagnostics.Debug.WriteLine("[OtherWindowsAppearanceSettingsPage] _appearanceSettingsService is null, getting from factory...");
            try
            {
                service = DatabaseServicesFactory.GetAppearanceSettingsService();
                System.Diagnostics.Debug.WriteLine("[OtherWindowsAppearanceSettingsPage] Got AppearanceSettingsService from factory successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[OtherWindowsAppearanceSettingsPage] ERROR getting service: {ex.Message}");
                SettingsSaved?.Invoke(this, EventArgs.Empty);
                return;
            }
        }
        
        if (service == null)
        {
            System.Diagnostics.Debug.WriteLine("[OtherWindowsAppearanceSettingsPage] ERROR: Service still null");
            SettingsSaved?.Invoke(this, EventArgs.Empty);
            return;
        }

        try
        {
            System.Diagnostics.Debug.WriteLine("[OtherWindowsAppearanceSettingsPage] Saving settings...");
            
            // Save Other Windows Background Color
            if (_otherWindowsBackgroundColorTextBox != null && !string.IsNullOrWhiteSpace(_otherWindowsBackgroundColorTextBox.Text))
            {
                await service.SetOtherWindowsBackgroundColorAsync(_otherWindowsBackgroundColorTextBox.Text);
                System.Diagnostics.Debug.WriteLine($"[OtherWindowsAppearanceSettingsPage] Saved BgColor: {_otherWindowsBackgroundColorTextBox.Text}");
            }

            // Save Other Windows Top Bar Color
            if (_otherWindowsTopBarColorTextBox != null && !string.IsNullOrWhiteSpace(_otherWindowsTopBarColorTextBox.Text))
            {
                await service.SetOtherWindowsTopBarColorAsync(_otherWindowsTopBarColorTextBox.Text);
                System.Diagnostics.Debug.WriteLine($"[OtherWindowsAppearanceSettingsPage] Saved TopBarColor: {_otherWindowsTopBarColorTextBox.Text}");
            }

            System.Diagnostics.Debug.WriteLine("[OtherWindowsAppearanceSettingsPage] All settings saved successfully!");
            SettingsSaved?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[OtherWindowsAppearanceSettingsPage] ERROR saving settings: {ex.Message}");
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

