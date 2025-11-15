using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using VetaleBrowser.VetaleBrowser.Database.Services;

namespace VetaleBrowser.VetaleBrowser.UI.Pages;

public partial class SearchEngineSettingsPage : UserControl
{
    public event EventHandler? BackRequested;
    public event EventHandler? SettingsSaved;
    public event EventHandler<string>? NavigateRequested; // Нова подія для навігації

    private RadioButton? _vetaleRadio;
    private RadioButton? _googleRadio;
    private RadioButton? _bingRadio;
    private RadioButton? _yahooRadio;
    private RadioButton? _baiduRadio;
    private RadioButton? _customRadio;
    private TextBox? _customUrlTextBox;

    private readonly ISettingsService _settingsService;
    private bool _isLoading = true;

    private const string VetaleSearchName = "Vetale Search";
    private const string VetaleSearchUrl = ""; // маркер локального пошуку, без зовнішнього URL

    private readonly Dictionary<string, string> _searchEngines = new()
    {
        { "Google", "https://www.google.com/search?q={0}" },
        { "Bing", "https://www.bing.com/search?q={0}" },
        { "Yahoo", "https://search.yahoo.com/search?p={0}" },
        { "Baidu", "https://www.baidu.com/s?wd={0}" }
    };

    // Конструктор для XAML
    public SearchEngineSettingsPage() : this(null!)
    {
    }

    public SearchEngineSettingsPage(ISettingsService settingsService)
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
        _customUrlTextBox = this.FindControl<TextBox>("CustomUrlTextBox");

        // Завантажуємо поточні налаштування
        await LoadCurrentSettings();
        _isLoading = false;
    }

    private async System.Threading.Tasks.Task LoadCurrentSettings()
    {
        if (_settingsService == null)
            return;

        try
        {
            var currentName = await _settingsService.GetSearchEngineNameAsync();
            var currentUrl = await _settingsService.GetSearchEngineUrlAsync();

            // Спеціальний випадок: Vetale Search як локальний пошук
            if (string.Equals(currentName, VetaleSearchName, StringComparison.OrdinalIgnoreCase))
            {
                if (_vetaleRadio != null)
                {
                    _vetaleRadio.IsChecked = true;
                }
                return;
            }

            // Знаходимо відповідний RadioButton для вбудованих веб-пошукових систем
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
                // Це кастомна пошукова система
                if (_customRadio != null)
                {
                    _customRadio.IsChecked = true;
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

        // Вмикаємо/вимикаємо TextBox в залежності від вибору
        if (_customUrlTextBox != null)
        {
            _customUrlTextBox.IsEnabled = _customRadio?.IsChecked == true;
        }
    }

    private void OnCustomUrlChanged(object? sender, TextChangedEventArgs e)
    {
        // Можна додати валідацію URL тут
    }

    private async void OnSaveClick(object? sender, RoutedEventArgs e)
    {
        if (_settingsService == null)
            return;

        try
        {
            string searchEngineName;
            string searchEngineUrl;

            if (_vetaleRadio?.IsChecked == true)
            {
                // Вибрано Vetale Search як локальний пошук
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
                searchEngineName = "Custom";
                searchEngineUrl = _customUrlTextBox?.Text?.Trim() ?? "";

                // Валідація кастомного URL
                if (string.IsNullOrWhiteSpace(searchEngineUrl))
                {
                    System.Diagnostics.Debug.WriteLine("SearchEngineSettingsPage: Custom URL is empty");
                    return;
                }

                if (!searchEngineUrl.Contains("{0}"))
                {
                    System.Diagnostics.Debug.WriteLine("SearchEngineSettingsPage: Custom URL must contain {0}");
                    return;
                }
            }
            else
            {
                // Нічого не вибрано
                return;
            }

            // Зберігаємо налаштування
            await _settingsService.SetSearchEngineAsync(searchEngineName, searchEngineUrl);
            
            System.Diagnostics.Debug.WriteLine($"SearchEngineSettingsPage: Saved {searchEngineName} - {searchEngineUrl}");
            
            // Повідомляємо про успішне збереження (SettingsWindow сам закриється після навігації)
            SettingsSaved?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"SearchEngineSettingsPage: Error saving settings: {ex}");
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
