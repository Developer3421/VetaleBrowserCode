using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using VetaleBrowser.VetaleBrowser.Database.Services;

namespace VetaleBrowser.VetaleBrowser.UI.Windows;

/// <summary>
/// Тип API сервісу для конфігурації
/// </summary>
public enum ApiServiceType
{
    Gemini,
    Pexels,
    Unsplash,
    YouTube
}

/// <summary>
/// Конфігурація для вікна API ключа
/// </summary>
public class ApiKeyWindowConfig
{
    public ApiServiceType ServiceType { get; set; }
    public string ServiceId { get; set; } = string.Empty;
    public string ServiceName { get; set; } = string.Empty;
    public string Icon { get; set; } = "🔑";
    public string WindowTitle { get; set; } = "API Key Configuration";
    public string Header { get; set; } = "Configure API Key";
    public string Description { get; set; } = "Enter your API key to enable this feature.";
    public string ApiKeyLabel { get; set; } = "API Key";
    public string Watermark { get; set; } = "Enter your API key...";
    public string HowToGetLabel { get; set; } = "📋 How to get an API key:";
    public string Step1 { get; set; } = "1. Go to the developer portal";
    public string Step2 { get; set; } = "2. Sign in or create an account";
    public string Step3 { get; set; } = "3. Generate an API key and copy it";
    public string PortalButtonText { get; set; } = "🔗 Open Developer Portal";
    public string PortalUrl { get; set; } = string.Empty;
    public string SaveButtonText { get; set; } = "Save API Key";
    public string SkipButtonText { get; set; } = "Skip";
    public string Note { get; set; } = "💡 Your API key is stored locally with encryption and never shared.";
    
    /// <summary>
    /// Створює конфігурацію для Gemini
    /// </summary>
    public static ApiKeyWindowConfig CreateGemini() => new()
    {
        ServiceType = ApiServiceType.Gemini,
        ServiceId = ApiServiceIds.Gemini,
        ServiceName = "Google Gemini",
        Icon = "✨",
        WindowTitle = "Налаштування Gemini API",
        Header = "Налаштування Google Gemini",
        Description = "Введіть ваш API ключ для використання AI-функцій Gemini у пошуку Vetale.",
        ApiKeyLabel = "Gemini API ключ",
        Watermark = "Введіть ваш Gemini API ключ...",
        HowToGetLabel = "📋 Як отримати безкоштовний API ключ:",
        Step1 = "1. Перейдіть на Google AI Studio",
        Step2 = "2. Увійдіть з вашим Google акаунтом",
        Step3 = "3. Натисніть 'Get API key' та скопіюйте його",
        PortalButtonText = "🔗 Відкрити Google AI Studio",
        PortalUrl = "https://aistudio.google.com/app/apikey",
        SaveButtonText = "Зберегти ключ",
        SkipButtonText = "Пропустити",
        Note = "💡 Ваш API ключ зберігається локально з шифруванням і ніколи не передається."
    };
    
    /// <summary>
    /// Створює конфігурацію для Pexels
    /// </summary>
    public static ApiKeyWindowConfig CreatePexels() => new()
    {
        ServiceType = ApiServiceType.Pexels,
        ServiceId = ApiServiceIds.Pexels,
        ServiceName = "Pexels",
        Icon = "📷",
        WindowTitle = "Налаштування Pexels API",
        Header = "Налаштування Pexels",
        Description = "Введіть ваш API ключ для пошуку зображень з Pexels.",
        ApiKeyLabel = "Pexels API ключ",
        Watermark = "Введіть ваш Pexels API ключ...",
        HowToGetLabel = "📋 Як отримати безкоштовний API ключ:",
        Step1 = "1. Перейдіть на Pexels.com",
        Step2 = "2. Зареєструйтесь або увійдіть",
        Step3 = "3. Перейдіть до API та створіть новий ключ",
        PortalButtonText = "🔗 Відкрити Pexels API",
        PortalUrl = "https://www.pexels.com/api/",
        SaveButtonText = "Зберегти ключ",
        SkipButtonText = "Пропустити",
        Note = "💡 Pexels API безкоштовний для особистого та комерційного використання."
    };
    
    /// <summary>
    /// Створює конфігурацію для Unsplash
    /// </summary>
    public static ApiKeyWindowConfig CreateUnsplash() => new()
    {
        ServiceType = ApiServiceType.Unsplash,
        ServiceId = ApiServiceIds.Unsplash,
        ServiceName = "Unsplash",
        Icon = "🖼️",
        WindowTitle = "Налаштування Unsplash API",
        Header = "Налаштування Unsplash",
        Description = "Введіть ваш Access Key для пошуку зображень з Unsplash.",
        ApiKeyLabel = "Unsplash Access Key",
        Watermark = "Введіть ваш Unsplash Access Key...",
        HowToGetLabel = "📋 Як отримати безкоштовний API ключ:",
        Step1 = "1. Перейдіть на Unsplash Developers",
        Step2 = "2. Зареєструйтесь та створіть додаток",
        Step3 = "3. Скопіюйте Access Key з налаштувань додатку",
        PortalButtonText = "🔗 Відкрити Unsplash Developers",
        PortalUrl = "https://unsplash.com/developers",
        SaveButtonText = "Зберегти ключ",
        SkipButtonText = "Пропустити",
        Note = "💡 Unsplash API безкоштовний з лімітом 50 запитів/годину для демо-додатків."
    };
    
    /// <summary>
    /// Створює конфігурацію для YouTube
    /// </summary>
    public static ApiKeyWindowConfig CreateYouTube() => new()
    {
        ServiceType = ApiServiceType.YouTube,
        ServiceId = ApiServiceIds.YouTube,
        ServiceName = "YouTube",
        Icon = "📺",
        WindowTitle = "Налаштування YouTube API",
        Header = "Налаштування YouTube Data API",
        Description = "Введіть ваш API ключ для пошуку відео на YouTube.",
        ApiKeyLabel = "YouTube API ключ",
        Watermark = "Введіть ваш YouTube API ключ...",
        HowToGetLabel = "📋 Як отримати API ключ:",
        Step1 = "1. Перейдіть на Google Cloud Console",
        Step2 = "2. Створіть проект та увімкніть YouTube Data API v3",
        Step3 = "3. Створіть API ключ в розділі Credentials",
        PortalButtonText = "🔗 Відкрити Google Cloud Console",
        PortalUrl = "https://console.cloud.google.com/apis/library/youtube.googleapis.com",
        SaveButtonText = "Зберегти ключ",
        SkipButtonText = "Пропустити",
        Note = "💡 YouTube Data API має безкоштовний ліміт 10,000 одиниць/день."
    };
}

/// <summary>
/// Результат діалогу API ключа
/// </summary>
public class ApiKeyConfigResult
{
    public bool Saved { get; set; }
    public bool Skipped { get; set; }
    public bool Cancelled { get; set; }
    public string? ApiKey { get; set; }
    public ApiServiceType ServiceType { get; set; }
    /// <summary>
    /// URL який потрібно відкрити у браузері (якщо користувач натиснув на посилання)
    /// </summary>
    public string? NavigateToUrl { get; set; }
}

/// <summary>
/// Універсальне вікно для конфігурації API ключів
/// </summary>
public partial class ApiKeyConfigWindow : Window
{
    private TextBlock? _serviceIcon;
    private TextBlock? _windowTitle;
    private TextBlock? _headerText;
    private TextBlock? _descriptionText;
    private TextBlock? _apiKeyLabel;
    private TextBox? _apiKeyTextBox;
    private TextBlock? _howToGetLabel;
    private TextBlock? _step1Text;
    private TextBlock? _step2Text;
    private TextBlock? _step3Text;
    private Button? _openPortalButton;
    private Button? _saveButton;
    private Button? _skipButton;
    private TextBlock? _noteText;
    
    private ApiKeyWindowConfig _config = new();
    private TaskCompletionSource<ApiKeyConfigResult>? _resultTcs;
    
    /// <summary>
    /// Подія для навігації до URL у браузері Vetale
    /// </summary>
    public event EventHandler<string>? NavigateRequested;
    
    /// <summary>
    /// Callback для навігації (альтернатива до події)
    /// </summary>
    private Action<string>? _navigateCallback;

    public ApiKeyConfigWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }
    
    public ApiKeyConfigWindow(ApiKeyWindowConfig config) : this()
    {
        _config = config;
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        _serviceIcon = this.FindControl<TextBlock>("ServiceIcon");
        _windowTitle = this.FindControl<TextBlock>("WindowTitle");
        _headerText = this.FindControl<TextBlock>("HeaderText");
        _descriptionText = this.FindControl<TextBlock>("DescriptionText");
        _apiKeyLabel = this.FindControl<TextBlock>("ApiKeyLabel");
        _apiKeyTextBox = this.FindControl<TextBox>("ApiKeyTextBox");
        _howToGetLabel = this.FindControl<TextBlock>("HowToGetLabel");
        _step1Text = this.FindControl<TextBlock>("Step1Text");
        _step2Text = this.FindControl<TextBlock>("Step2Text");
        _step3Text = this.FindControl<TextBlock>("Step3Text");
        _openPortalButton = this.FindControl<Button>("OpenPortalButton");
        _saveButton = this.FindControl<Button>("SaveButton");
        _skipButton = this.FindControl<Button>("SkipButton");
        _noteText = this.FindControl<TextBlock>("NoteText");
        
        ApplyConfig();
        _apiKeyTextBox?.Focus();
    }
    
    private void ApplyConfig()
    {
        if (_serviceIcon != null) _serviceIcon.Text = _config.Icon;
        if (_windowTitle != null) _windowTitle.Text = _config.WindowTitle;
        if (_headerText != null) _headerText.Text = _config.Header;
        if (_descriptionText != null) _descriptionText.Text = _config.Description;
        if (_apiKeyLabel != null) _apiKeyLabel.Text = _config.ApiKeyLabel;
        if (_apiKeyTextBox != null) _apiKeyTextBox.Watermark = _config.Watermark;
        if (_howToGetLabel != null) _howToGetLabel.Text = _config.HowToGetLabel;
        if (_step1Text != null) _step1Text.Text = _config.Step1;
        if (_step2Text != null) _step2Text.Text = _config.Step2;
        if (_step3Text != null) _step3Text.Text = _config.Step3;
        if (_openPortalButton != null) _openPortalButton.Content = _config.PortalButtonText;
        if (_saveButton != null) _saveButton.Content = _config.SaveButtonText;
        if (_skipButton != null) _skipButton.Content = _config.SkipButtonText;
        if (_noteText != null) _noteText.Text = _config.Note;
        
        Title = _config.WindowTitle;
    }
    
    /// <summary>
    /// Встановлює конфігурацію вікна
    /// </summary>
    public void SetConfig(ApiKeyWindowConfig config)
    {
        _config = config;
        ApplyConfig();
    }

    /// <summary>
    /// Показує діалог та повертає результат
    /// </summary>
    public Task<ApiKeyConfigResult> ShowDialogAsync(Window? parent = null)
    {
        _resultTcs = new TaskCompletionSource<ApiKeyConfigResult>();
        
        Closed += (s, e) =>
        {
            if (!_resultTcs.Task.IsCompleted)
            {
                _resultTcs.SetResult(new ApiKeyConfigResult 
                { 
                    Cancelled = true,
                    ServiceType = _config.ServiceType
                });
            }
        };

        if (parent != null)
        {
            _ = ShowDialog(parent);
        }
        else
        {
            Show();
        }

        return _resultTcs.Task;
    }

    private void TopBar_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginMoveDrag(e);
        }
    }

    private void CloseWindow_Click(object? sender, RoutedEventArgs e)
    {
        _resultTcs?.TrySetResult(new ApiKeyConfigResult 
        { 
            Cancelled = true,
            ServiceType = _config.ServiceType
        });
        Close();
    }

    private async void Save_Click(object? sender, RoutedEventArgs e)
    {
        var apiKey = _apiKeyTextBox?.Text?.Trim();
        
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            System.Diagnostics.Debug.WriteLine($"[ApiKeyConfigWindow] Empty API key for {_config.ServiceId}");
            return;
        }

        try
        {
            // Зберігаємо ключ в базу даних
            var apiKeysService = DatabaseServicesFactory.TryGetApiKeysService();
            if (apiKeysService != null)
            {
                await apiKeysService.SetApiKeyAsync(_config.ServiceId, _config.ServiceName, apiKey);
                System.Diagnostics.Debug.WriteLine($"[ApiKeyConfigWindow] Saved API key for {_config.ServiceId}");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ApiKeyConfigWindow] Error saving API key: {ex.Message}");
        }
        
        _resultTcs?.TrySetResult(new ApiKeyConfigResult 
        { 
            Saved = true,
            ApiKey = apiKey,
            ServiceType = _config.ServiceType
        });
        
        Close();
    }

    private void Skip_Click(object? sender, RoutedEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine($"[ApiKeyConfigWindow] Skipped API key for {_config.ServiceId}");
        
        _resultTcs?.TrySetResult(new ApiKeyConfigResult 
        { 
            Skipped = true,
            ServiceType = _config.ServiceType
        });
        
        Close();
    }

    private void OpenPortal_Click(object? sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_config.PortalUrl)) return;
        
        System.Diagnostics.Debug.WriteLine($"[ApiKeyConfigWindow] Opening portal: {_config.PortalUrl}");
        
        // Спочатку пробуємо callback
        if (_navigateCallback != null)
        {
            _navigateCallback(_config.PortalUrl);
            Close();
            return;
        }
        
        // Потім пробуємо подію
        if (NavigateRequested != null)
        {
            NavigateRequested.Invoke(this, _config.PortalUrl);
            Close();
            return;
        }
        
        // Fallback: відкриваємо в системному браузері
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = _config.PortalUrl,
                UseShellExecute = true
            };
            Process.Start(psi);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ApiKeyConfigWindow] Failed to open portal: {ex.Message}");
        }
    }
    
    // ==================== Статичні методи для швидкого виклику ====================
    
    /// <summary>
    /// Показує вікно для налаштування Gemini API
    /// </summary>
    /// <param name="parent">Батьківське вікно</param>
    /// <param name="navigateCallback">Callback для відкриття URL у браузері Vetale (опціонально)</param>
    public static Task<ApiKeyConfigResult> ShowGeminiConfigAsync(Window? parent = null, Action<string>? navigateCallback = null)
    {
        var window = new ApiKeyConfigWindow(ApiKeyWindowConfig.CreateGemini());
        window._navigateCallback = navigateCallback;
        return window.ShowDialogAsync(parent);
    }
    
    /// <summary>
    /// Показує вікно для налаштування Pexels API
    /// </summary>
    public static Task<ApiKeyConfigResult> ShowPexelsConfigAsync(Window? parent = null, Action<string>? navigateCallback = null)
    {
        var window = new ApiKeyConfigWindow(ApiKeyWindowConfig.CreatePexels());
        window._navigateCallback = navigateCallback;
        return window.ShowDialogAsync(parent);
    }
    
    /// <summary>
    /// Показує вікно для налаштування Unsplash API
    /// </summary>
    public static Task<ApiKeyConfigResult> ShowUnsplashConfigAsync(Window? parent = null, Action<string>? navigateCallback = null)
    {
        var window = new ApiKeyConfigWindow(ApiKeyWindowConfig.CreateUnsplash());
        window._navigateCallback = navigateCallback;
        return window.ShowDialogAsync(parent);
    }
    
    /// <summary>
    /// Показує вікно для налаштування YouTube API
    /// </summary>
    public static Task<ApiKeyConfigResult> ShowYouTubeConfigAsync(Window? parent = null, Action<string>? navigateCallback = null)
    {
        var window = new ApiKeyConfigWindow(ApiKeyWindowConfig.CreateYouTube());
        window._navigateCallback = navigateCallback;
        return window.ShowDialogAsync(parent);
    }
}

