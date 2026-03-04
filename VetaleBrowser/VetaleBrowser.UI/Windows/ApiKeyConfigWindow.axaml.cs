using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using VetaleBrowser.VetaleBrowser.Database.Services;

namespace VetaleBrowser.VetaleBrowser.UI.Windows;

/// <summary>
/// API service type for configuration
/// </summary>
public enum ApiServiceType
{
    Gemini,
    Pexels,
    Unsplash,
    YouTube
}

/// <summary>
/// Configuration for the API key window
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
    /// Get localized string from resources
    /// </summary>
    private static string GetLocalizedString(string key, string fallback)
    {
        try
        {
            var app = Application.Current;
            if (app != null && app.TryFindResource(key, out var value) && value is string s)
                return s;
        }
        catch { }
        return fallback;
    }
    
    /// <summary>
    /// Creates a configuration for Gemini
    /// </summary>
    public static ApiKeyWindowConfig CreateGemini() => new()
    {
        ServiceType = ApiServiceType.Gemini,
        ServiceId = ApiServiceIds.Gemini,
        ServiceName = "Google Gemini",
        Icon = "✨",
        WindowTitle = GetLocalizedString("Gemini.ApiKey.Title", "Gemini API Key Configuration"),
        Header = GetLocalizedString("Gemini.ApiKey.Header", "Configure Gemini AI"),
        Description = GetLocalizedString("Gemini.ApiKey.Description", "To use Gemini AI features in Vetale Search, you need a Google API key."),
        ApiKeyLabel = GetLocalizedString("Gemini.ApiKey.Label", "Gemini API Key"),
        Watermark = GetLocalizedString("Gemini.ApiKey.Watermark", "Enter your Gemini API key..."),
        HowToGetLabel = GetLocalizedString("Gemini.ApiKey.HowToGet", "📋 How to get a free API key:"),
        Step1 = GetLocalizedString("Gemini.ApiKey.Step1", "1. Go to Google AI Studio"),
        Step2 = GetLocalizedString("Gemini.ApiKey.Step2", "2. Sign in with your Google account"),
        Step3 = GetLocalizedString("Gemini.ApiKey.Step3", "3. Click \"Get API key\" and copy it"),
        PortalButtonText = GetLocalizedString("Gemini.ApiKey.OpenAIStudio", "🔗 Open Google AI Studio"),
        PortalUrl = "https://aistudio.google.com/app/apikey",
        SaveButtonText = GetLocalizedString("ApiKey.Save", "Save Key"),
        SkipButtonText = GetLocalizedString("ApiKey.Skip", "Skip"),
        Note = GetLocalizedString("Gemini.ApiKey.Note", "💡 Your API key is stored locally and never shared.")
    };
    
    /// <summary>
    /// Creates a configuration for Pexels
    /// </summary>
    public static ApiKeyWindowConfig CreatePexels() => new()
    {
        ServiceType = ApiServiceType.Pexels,
        ServiceId = ApiServiceIds.Pexels,
        ServiceName = "Pexels",
        Icon = "📷",
        WindowTitle = GetLocalizedString("Pexels.ApiKey.Title", "Pexels API Configuration"),
        Header = GetLocalizedString("Pexels.ApiKey.Header", "Configure Pexels"),
        Description = GetLocalizedString("Pexels.ApiKey.Description", "Enter your API key to search high-quality images from Pexels."),
        ApiKeyLabel = GetLocalizedString("Pexels.ApiKey.Label", "Pexels API Key"),
        Watermark = GetLocalizedString("Pexels.ApiKey.Watermark", "Enter your Pexels API key..."),
        HowToGetLabel = GetLocalizedString("Pexels.ApiKey.HowToGet", "📋 How to get a free API key:"),
        Step1 = GetLocalizedString("Pexels.ApiKey.Step1", "1. Go to Pexels.com"),
        Step2 = GetLocalizedString("Pexels.ApiKey.Step2", "2. Sign up or log in"),
        Step3 = GetLocalizedString("Pexels.ApiKey.Step3", "3. Navigate to API and create a new key"),
        PortalButtonText = GetLocalizedString("Pexels.ApiKey.OpenPortal", "🔗 Open Pexels API"),
        PortalUrl = "https://www.pexels.com/api/",
        SaveButtonText = GetLocalizedString("ApiKey.Save", "Save Key"),
        SkipButtonText = GetLocalizedString("ApiKey.Skip", "Skip"),
        Note = GetLocalizedString("Pexels.ApiKey.Note", "💡 Pexels API is free for personal and commercial use.")
    };
    
    /// <summary>
    /// Creates a configuration for Unsplash
    /// </summary>
    public static ApiKeyWindowConfig CreateUnsplash() => new()
    {
        ServiceType = ApiServiceType.Unsplash,
        ServiceId = ApiServiceIds.Unsplash,
        ServiceName = "Unsplash",
        Icon = "🖼️",
        WindowTitle = GetLocalizedString("Unsplash.ApiKey.Title", "Unsplash API Configuration"),
        Header = GetLocalizedString("Unsplash.ApiKey.Header", "Configure Unsplash"),
        Description = GetLocalizedString("Unsplash.ApiKey.Description", "Enter your Access Key to search images from Unsplash."),
        ApiKeyLabel = GetLocalizedString("Unsplash.ApiKey.Label", "Unsplash Access Key"),
        Watermark = GetLocalizedString("Unsplash.ApiKey.Watermark", "Enter your Unsplash Access Key..."),
        HowToGetLabel = GetLocalizedString("Unsplash.ApiKey.HowToGet", "📋 How to get a free API key:"),
        Step1 = GetLocalizedString("Unsplash.ApiKey.Step1", "1. Go to Unsplash Developers"),
        Step2 = GetLocalizedString("Unsplash.ApiKey.Step2", "2. Sign up and create an application"),
        Step3 = GetLocalizedString("Unsplash.ApiKey.Step3", "3. Copy the Access Key from your app settings"),
        PortalButtonText = GetLocalizedString("Unsplash.ApiKey.OpenPortal", "🔗 Open Unsplash Developers"),
        PortalUrl = "https://unsplash.com/developers",
        SaveButtonText = GetLocalizedString("ApiKey.Save", "Save Key"),
        SkipButtonText = GetLocalizedString("ApiKey.Skip", "Skip"),
        Note = GetLocalizedString("Unsplash.ApiKey.Note", "💡 Unsplash API is free with a limit of 50 requests/hour for demo apps.")
    };
    
    /// <summary>
    /// Creates a configuration for YouTube
    /// </summary>
    public static ApiKeyWindowConfig CreateYouTube() => new()
    {
        ServiceType = ApiServiceType.YouTube,
        ServiceId = ApiServiceIds.YouTube,
        ServiceName = "YouTube",
        Icon = "📺",
        WindowTitle = GetLocalizedString("YouTube.ApiKey.Title", "YouTube API Configuration"),
        Header = GetLocalizedString("YouTube.ApiKey.Header", "Configure YouTube Data API"),
        Description = GetLocalizedString("YouTube.ApiKey.Description", "Enter your API key to search videos on YouTube."),
        ApiKeyLabel = GetLocalizedString("YouTube.ApiKey.Label", "YouTube API Key"),
        Watermark = GetLocalizedString("YouTube.ApiKey.Watermark", "Enter your YouTube API key..."),
        HowToGetLabel = GetLocalizedString("YouTube.ApiKey.HowToGet", "📋 How to get an API key:"),
        Step1 = GetLocalizedString("YouTube.ApiKey.Step1", "1. Go to Google Cloud Console"),
        Step2 = GetLocalizedString("YouTube.ApiKey.Step2", "2. Create a project and enable YouTube Data API v3"),
        Step3 = GetLocalizedString("YouTube.ApiKey.Step3", "3. Create an API key in the Credentials section"),
        PortalButtonText = GetLocalizedString("YouTube.ApiKey.OpenPortal", "🔗 Open Google Cloud Console"),
        PortalUrl = "https://console.cloud.google.com/apis/library/youtube.googleapis.com",
        SaveButtonText = GetLocalizedString("ApiKey.Save", "Save Key"),
        SkipButtonText = GetLocalizedString("ApiKey.Skip", "Skip"),
        Note = GetLocalizedString("YouTube.ApiKey.Note", "💡 YouTube Data API has a free limit of 10,000 units/day.")
    };
}

/// <summary>
/// API key dialog result
/// </summary>
public class ApiKeyConfigResult
{
    public bool Saved { get; set; }
    public bool Skipped { get; set; }
    public bool Cancelled { get; set; }
    public string? ApiKey { get; set; }
    public ApiServiceType ServiceType { get; set; }
    /// <summary>
    /// URL to open in the browser (if the user clicked a link)
    /// </summary>
    public string? NavigateToUrl { get; set; }
}

/// <summary>
/// Universal window for configuring API keys
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
    /// Event for navigation to a URL in the Vetale browser
    /// </summary>
    public event EventHandler<string>? NavigateRequested;
    
    /// <summary>
    /// Navigation callback (alternative to the event)
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
    /// Sets the window configuration
    /// </summary>
    public void SetConfig(ApiKeyWindowConfig config)
    {
        _config = config;
        ApplyConfig();
    }

    /// <summary>
    /// Shows the dialog and returns the result
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
            // Save the key to the database
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
        
        // First try callback
        if (_navigateCallback != null)
        {
            _navigateCallback(_config.PortalUrl);
            Close();
            return;
        }
        
        // Then try the event
        if (NavigateRequested != null)
        {
            NavigateRequested.Invoke(this, _config.PortalUrl);
            Close();
            return;
        }
        
        // Fallback: open in the system browser
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
    
    // ==================== Static methods for quick invocation ====================
    
    /// <summary>
    /// Shows the window for configuring the Gemini API
    /// </summary>
    /// <param name="parent">Parent window</param>
    /// <param name="navigateCallback">Callback for opening a URL in the Vetale browser (optional)</param>
    public static Task<ApiKeyConfigResult> ShowGeminiConfigAsync(Window? parent = null, Action<string>? navigateCallback = null)
    {
        var window = new ApiKeyConfigWindow(ApiKeyWindowConfig.CreateGemini());
        window._navigateCallback = navigateCallback;
        return window.ShowDialogAsync(parent);
    }
    
    /// <summary>
    /// Shows the window for configuring the Pexels API
    /// </summary>
    public static Task<ApiKeyConfigResult> ShowPexelsConfigAsync(Window? parent = null, Action<string>? navigateCallback = null)
    {
        var window = new ApiKeyConfigWindow(ApiKeyWindowConfig.CreatePexels());
        window._navigateCallback = navigateCallback;
        return window.ShowDialogAsync(parent);
    }
    
    /// <summary>
    /// Shows the window for configuring the Unsplash API
    /// </summary>
    public static Task<ApiKeyConfigResult> ShowUnsplashConfigAsync(Window? parent = null, Action<string>? navigateCallback = null)
    {
        var window = new ApiKeyConfigWindow(ApiKeyWindowConfig.CreateUnsplash());
        window._navigateCallback = navigateCallback;
        return window.ShowDialogAsync(parent);
    }
    
    /// <summary>
    /// Shows the window for configuring the YouTube API
    /// </summary>
    public static Task<ApiKeyConfigResult> ShowYouTubeConfigAsync(Window? parent = null, Action<string>? navigateCallback = null)
    {
        var window = new ApiKeyConfigWindow(ApiKeyWindowConfig.CreateYouTube());
        window._navigateCallback = navigateCallback;
        return window.ShowDialogAsync(parent);
    }
}

