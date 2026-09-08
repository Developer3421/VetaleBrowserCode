using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using VetaleBrowser.VetaleBrowser.Database;
using VetaleBrowser.VetaleBrowser.Database.Services;

namespace VetaleBrowser.VetaleBrowser.UI.Pages;

public partial class SearchEngineSettingsPage : UserControl
{
    public event EventHandler? BackRequested;
    public event EventHandler? SettingsSaved;
    public event EventHandler<string>? NavigateRequested; // New event for navigation

    private RadioButton? _vetaleRadio;
    private RadioButton? _googleRadio;
    private RadioButton? _bingRadio;
    private RadioButton? _yahooRadio;
    private RadioButton? _baiduRadio;
    private RadioButton? _customRadio;
    private TextBox? _customNameTextBox;
    private TextBox? _customUrlTextBox;
    private TextBlock? _customErrorText;

    private readonly ISettingsService? _settingsService;
    private bool _isLoading = true;

    private const string VetaleSearchName = "Vetale Search";
    private const string VetaleSearchUrl = ""; // local search marker, no external URL

    private readonly Dictionary<string, string> _searchEngines = new()
    {
        { "Google", "https://www.google.com/search?q={0}" },
        { "Bing", "https://www.bing.com/search?q={0}" },
        { "Yahoo", "https://search.yahoo.com/search?p={0}" },
        { "Baidu", "https://www.baidu.com/s?wd={0}" }
    };

    // Constructor for XAML
    public SearchEngineSettingsPage() : this(null)
    {
    }

    public SearchEngineSettingsPage(ISettingsService? settingsService)
    {
        _settingsService = settingsService;
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private async void OnLoaded(object? sender, RoutedEventArgs e)
    {
        _vetaleRadio = this.FindControl<RadioButton>("VetaleRadio");
        _googleRadio = this.FindControl<RadioButton>("GoogleRadio");
        _bingRadio = this.FindControl<RadioButton>("BingRadio");
        _yahooRadio = this.FindControl<RadioButton>("YahooRadio");
        _baiduRadio = this.FindControl<RadioButton>("BaiduRadio");
        _customRadio = this.FindControl<RadioButton>("CustomRadio");
        _customNameTextBox = this.FindControl<TextBox>("CustomNameTextBox");
        _customUrlTextBox = this.FindControl<TextBox>("CustomUrlTextBox");
        _customErrorText = this.FindControl<TextBlock>("CustomErrorText");

        // Load current settings
        await LoadCurrentSettings();
        _isLoading = false;
    }

    private async System.Threading.Tasks.Task LoadCurrentSettings()
    {
        try
        {
            string currentName = "Google"; // Default
            string currentUrl = "https://www.google.com/search?q={0}"; // Default
            
            if (_settingsService != null)
            {
                try
                {
                    currentName = await _settingsService.GetSearchEngineNameAsync();
                    currentUrl = await _settingsService.GetSearchEngineUrlAsync();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[SearchEngineSettingsPage] Error loading settings: {ex.Message}");
                    // Use defaults
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("[SearchEngineSettingsPage] SettingsService is null, using defaults");
            }

            // Special case: Vetale Search as local search
            if (string.Equals(currentName, VetaleSearchName, StringComparison.OrdinalIgnoreCase))
            {
                if (_vetaleRadio != null)
                {
                    _vetaleRadio.IsChecked = true;
                }
                return;
            }

            // Find the corresponding RadioButton for built-in web search engines
            if (_searchEngines.TryGetValue(currentName, out var url) && url == currentUrl)
            {
                var radio = currentName switch
                {
                    "Google" => _googleRadio,
                    "Bing" => _bingRadio,
                    "Yahoo" => _yahooRadio,
                    "Baidu" => _baiduRadio,
                    _ => null
                };

                if (radio != null)
                {
                    radio.IsChecked = true;
                }
            }
            else
            {
                // This is a custom search engine — показуємо збережені назву і шаблон
                if (_customRadio != null)
                {
                    _customRadio.IsChecked = true;
                    if (_customNameTextBox != null)
                    {
                        _customNameTextBox.Text = currentName;
                        _customNameTextBox.IsEnabled = true;
                    }
                    if (_customUrlTextBox != null)
                    {
                        _customUrlTextBox.Text = currentUrl;
                        _customUrlTextBox.IsEnabled = true;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"SearchEngineSettingsPage: Error loading settings: {ex}");
        }
    }

    private void OnSearchEngineChanged(object? sender, RoutedEventArgs e)
    {
        if (_isLoading)
            return;

        // Enable/disable TextBox depending on selection
        var custom = _customRadio?.IsChecked == true;
        if (_customUrlTextBox != null)
            _customUrlTextBox.IsEnabled = custom;
        if (_customNameTextBox != null)
            _customNameTextBox.IsEnabled = custom;
        if (!custom)
            ShowCustomError(null);
    }

    private void OnCustomUrlChanged(object? sender, TextChangedEventArgs e)
    {
        if (_isLoading || _customRadio?.IsChecked != true) return;
        // Жива валідація прямо в інтерфейсі
        ShowCustomError(VetaleBrowser.UI.Services.SearchUrlBuilder.Validate(_customUrlTextBox?.Text));
    }

    private void ShowCustomError(string? message)
    {
        if (_customErrorText == null) return;
        _customErrorText.Text = message ?? string.Empty;
        _customErrorText.IsVisible = !string.IsNullOrEmpty(message);
    }

    private async void OnSaveClick(object? sender, RoutedEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine("[SearchEngineSettingsPage] OnSaveClick called");
        
        // If service is null - try to create our own
        var settingsService = _settingsService;
        if (settingsService == null)
        {
            System.Diagnostics.Debug.WriteLine("[SearchEngineSettingsPage] _settingsService is null, getting from factory...");
            try
            {
                settingsService = DatabaseServicesFactory.GetSettingsService();
                System.Diagnostics.Debug.WriteLine("[SearchEngineSettingsPage] Got SettingsService from factory successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SearchEngineSettingsPage] ERROR getting SettingsService: {ex.Message}");
                SettingsSaved?.Invoke(this, EventArgs.Empty);
                return;
            }
        }

        try
        {
            string searchEngineName;
            string searchEngineUrl;

            if (_vetaleRadio?.IsChecked == true)
            {
                // Vetale Search selected as local search
                searchEngineName = VetaleSearchName;
                searchEngineUrl = VetaleSearchUrl;
            }
            else if (_googleRadio?.IsChecked == true)
            {
                searchEngineName = "Google";
                searchEngineUrl = _searchEngines["Google"];
            }
            else if (_bingRadio?.IsChecked == true)
            {
                searchEngineName = "Bing";
                searchEngineUrl = _searchEngines["Bing"];
            }
            else if (_yahooRadio?.IsChecked == true)
            {
                searchEngineName = "Yahoo";
                searchEngineUrl = _searchEngines["Yahoo"];
            }
            else if (_baiduRadio?.IsChecked == true)
            {
                searchEngineName = "Baidu";
                searchEngineUrl = _searchEngines["Baidu"];
            }
            else if (_customRadio?.IsChecked == true)
            {
                var rawUrl = _customUrlTextBox?.Text?.Trim() ?? "";
                var error = VetaleBrowser.UI.Services.SearchUrlBuilder.Validate(rawUrl);
                if (error != null)
                {
                    ShowCustomError(error);
                    System.Diagnostics.Debug.WriteLine($"SearchEngineSettingsPage: Custom invalid: {error}");
                    return;
                }

                searchEngineName = _customNameTextBox?.Text?.Trim() ?? "";
                if (string.IsNullOrWhiteSpace(searchEngineName))
                    searchEngineName = "Custom";
                searchEngineUrl = VetaleBrowser.UI.Services.SearchUrlBuilder.Normalize(rawUrl);
                ShowCustomError(null);
            }
            else
            {
                // Nothing selected
                return;
            }

            // Save settings
            await settingsService.SetSearchEngineAsync(searchEngineName, searchEngineUrl);
            
            System.Diagnostics.Debug.WriteLine($"[SearchEngineSettingsPage] Saved {searchEngineName} - {searchEngineUrl}");
            
            // Notify about successful save (SettingsWindow will close itself after navigation)
            SettingsSaved?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[SearchEngineSettingsPage] Error saving settings: {ex}");
        }
    }

    private void OnBackClick(object? sender, RoutedEventArgs e)
    {
        BackRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnOpenVetaleSearch(object? sender, RoutedEventArgs e)
    {
        var homeUrl = "vetale://search";
        System.Diagnostics.Debug.WriteLine($"SearchEngineSettingsPage: Opening Vetale Search home page: {homeUrl}");
        NavigateRequested?.Invoke(this, homeUrl);
    }
}
