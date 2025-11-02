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

    private RadioButton? _googleRadio;
    private RadioButton? _bingRadio;
    private RadioButton? _yahooRadio;
    private RadioButton? _baiduRadio;
    private RadioButton? _customRadio;
    private TextBox? _customUrlTextBox;

    private readonly ISettingsService _settingsService;
    private bool _isLoading = true;

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

            // Знаходимо відповідний RadioButton
            if (_searchEngines.TryGetValue(currentName, out var url) && url == currentUrl)
            {
                // Це одна з предустановлених пошукових систем
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

            if (_googleRadio?.IsChecked == true)
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
                    // TODO: Показати повідомлення про помилку
                    System.Diagnostics.Debug.WriteLine("SearchEngineSettingsPage: Custom URL is empty");
                    return;
                }

                if (!searchEngineUrl.Contains("{0}"))
                {
                    // TODO: Показати повідомлення про помилку
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
            
            // Повідомляємо про успішне збереження
            SettingsSaved?.Invoke(this, EventArgs.Empty);
            
            // Повертаємось назад
            OnBackClick(sender, e);
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
}

