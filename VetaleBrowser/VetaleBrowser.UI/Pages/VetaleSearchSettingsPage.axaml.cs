using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using VetaleBrowser.VetaleBrowser.Database.Services;

namespace VetaleBrowser.VetaleBrowser.UI.Pages;

public partial class VetaleSearchSettingsPage : UserControl
{
    public event EventHandler? BackRequested;
    public event EventHandler? SettingsSaved;

    // API Key inputs
    private TextBox? _geminiApiKeyInput;
    private TextBox? _pexelsApiKeyInput;
    private TextBox? _unsplashApiKeyInput;
    private TextBox? _youTubeApiKeyInput;

    // Color inputs
    private TextBox? _gradientStartColorInput;
    private TextBox? _gradientEndColorInput;
    private TextBox? _cardBackgroundInput;
    private TextBox? _linkColorInput;

    // Color previews
    private Border? _gradientStartPreview;
    private Border? _gradientEndPreview;
    private Border? _cardBackgroundPreview;
    private Border? _linkColorPreview;

    private readonly IApiKeysService? _apiKeysService;

    // Setting keys for colors (stored as "api keys" but actually settings)
    private const string SettingGradientStart = "vetale_search_gradient_start";
    private const string SettingGradientEnd = "vetale_search_gradient_end";
    private const string SettingCardBackground = "vetale_search_card_bg";
    private const string SettingLinkColor = "vetale_search_link_color";

    public VetaleSearchSettingsPage()
    {
        _apiKeysService = DatabaseServicesFactory.TryGetApiKeysService();
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private async void OnLoaded(object? sender, RoutedEventArgs e)
    {
        // API Key inputs
        _geminiApiKeyInput = this.FindControl<TextBox>("GeminiApiKeyInput");
        _pexelsApiKeyInput = this.FindControl<TextBox>("PexelsApiKeyInput");
        _unsplashApiKeyInput = this.FindControl<TextBox>("UnsplashApiKeyInput");
        _youTubeApiKeyInput = this.FindControl<TextBox>("YouTubeApiKeyInput");

        // Color inputs
        _gradientStartColorInput = this.FindControl<TextBox>("GradientStartColorInput");
        _gradientEndColorInput = this.FindControl<TextBox>("GradientEndColorInput");
        _cardBackgroundInput = this.FindControl<TextBox>("CardBackgroundInput");
        _linkColorInput = this.FindControl<TextBox>("LinkColorInput");

        // Color previews
        _gradientStartPreview = this.FindControl<Border>("GradientStartPreview");
        _gradientEndPreview = this.FindControl<Border>("GradientEndPreview");
        _cardBackgroundPreview = this.FindControl<Border>("CardBackgroundPreview");
        _linkColorPreview = this.FindControl<Border>("LinkColorPreview");

        // Add text changed handlers for live color preview
        if (_gradientStartColorInput != null)
            _gradientStartColorInput.TextChanged += (_, _) => UpdateColorPreview(_gradientStartColorInput, _gradientStartPreview);
        if (_gradientEndColorInput != null)
            _gradientEndColorInput.TextChanged += (_, _) => UpdateColorPreview(_gradientEndColorInput, _gradientEndPreview);
        if (_cardBackgroundInput != null)
            _cardBackgroundInput.TextChanged += (_, _) => UpdateColorPreview(_cardBackgroundInput, _cardBackgroundPreview);
        if (_linkColorInput != null)
            _linkColorInput.TextChanged += (_, _) => UpdateColorPreview(_linkColorInput, _linkColorPreview);

        await LoadSettingsAsync();
    }

    private void UpdateColorPreview(TextBox? input, Border? preview)
    {
        if (input == null || preview == null) return;

        try
        {
            if (Color.TryParse(input.Text, out var color))
            {
                preview.Background = new SolidColorBrush(color);
            }
        }
        catch
        {
            // Ignore invalid colors
        }
    }

    private async Task LoadSettingsAsync()
    {
        try
        {
            // Load API Keys
            if (_apiKeysService != null)
            {
                if (_geminiApiKeyInput != null)
                {
                    var geminiKey = await _apiKeysService.GetGeminiApiKeyAsync();
                    _geminiApiKeyInput.Text = geminiKey ?? "";
                }

                if (_pexelsApiKeyInput != null)
                {
                    var pexelsKey = await _apiKeysService.GetPexelsApiKeyAsync();
                    _pexelsApiKeyInput.Text = pexelsKey ?? "";
                }

                if (_unsplashApiKeyInput != null)
                {
                    var unsplashKey = await _apiKeysService.GetUnsplashApiKeyAsync();
                    _unsplashApiKeyInput.Text = unsplashKey ?? "";
                }

                if (_youTubeApiKeyInput != null)
                {
                    var youtubeKey = await _apiKeysService.GetYouTubeApiKeyAsync();
                    _youTubeApiKeyInput.Text = youtubeKey ?? "";
                }

                // Load color settings (stored as api keys for simplicity)
                if (_gradientStartColorInput != null)
                {
                    var gradientStart = await _apiKeysService.GetApiKeyAsync(SettingGradientStart) ?? "#FF8A00";
                    _gradientStartColorInput.Text = gradientStart;
                    UpdateColorPreview(_gradientStartColorInput, _gradientStartPreview);
                }

                if (_gradientEndColorInput != null)
                {
                    var gradientEnd = await _apiKeysService.GetApiKeyAsync(SettingGradientEnd) ?? "#9C27B0";
                    _gradientEndColorInput.Text = gradientEnd;
                    UpdateColorPreview(_gradientEndColorInput, _gradientEndPreview);
                }

                if (_cardBackgroundInput != null)
                {
                    var cardBg = await _apiKeysService.GetApiKeyAsync(SettingCardBackground) ?? "#FFFFFF";
                    _cardBackgroundInput.Text = cardBg;
                    UpdateColorPreview(_cardBackgroundInput, _cardBackgroundPreview);
                }

                if (_linkColorInput != null)
                {
                    var linkColor = await _apiKeysService.GetApiKeyAsync(SettingLinkColor) ?? "#1565C0";
                    _linkColorInput.Text = linkColor;
                    UpdateColorPreview(_linkColorInput, _linkColorPreview);
                }
            }
            else
            {
                // Use defaults when service not available
                if (_gradientStartColorInput != null) _gradientStartColorInput.Text = "#FF8A00";
                if (_gradientEndColorInput != null) _gradientEndColorInput.Text = "#9C27B0";
                if (_cardBackgroundInput != null) _cardBackgroundInput.Text = "#FFFFFF";
                if (_linkColorInput != null) _linkColorInput.Text = "#1565C0";
            }

            Debug.WriteLine("[VetaleSearchSettingsPage] Settings loaded successfully");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[VetaleSearchSettingsPage] Error loading settings: {ex.Message}");
        }
    }

    private async void OnSaveClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            // Save API Keys
            if (_apiKeysService != null)
            {
                // Gemini
                if (_geminiApiKeyInput != null)
                {
                    var geminiKey = _geminiApiKeyInput.Text?.Trim();
                    if (!string.IsNullOrWhiteSpace(geminiKey))
                    {
                        await _apiKeysService.SetGeminiApiKeyAsync(geminiKey);
                        // Update static key in GeminiAiSummaryService for immediate effect
                        VetaleBrowser.Search.Services.GeminiAiSummaryService.SetCustomApiKey(geminiKey);
                        Debug.WriteLine("[VetaleSearchSettingsPage] Gemini API key saved and applied");
                    }
                    else
                    {
                        await _apiKeysService.RemoveApiKeyAsync(ApiServiceIds.Gemini);
                        VetaleBrowser.Search.Services.GeminiAiSummaryService.ClearCustomApiKey();
                        Debug.WriteLine("[VetaleSearchSettingsPage] Gemini API key cleared");
                    }
                }

                // Pexels
                if (_pexelsApiKeyInput != null)
                {
                    var pexelsKey = _pexelsApiKeyInput.Text?.Trim();
                    Debug.WriteLine($"[VetaleSearchSettingsPage] Pexels key from input: {(string.IsNullOrWhiteSpace(pexelsKey) ? "EMPTY" : pexelsKey.Substring(0, Math.Min(10, pexelsKey.Length)) + "...")}");
                    
                    if (!string.IsNullOrWhiteSpace(pexelsKey))
                    {
                        Debug.WriteLine("[VetaleSearchSettingsPage] Saving Pexels key to database...");
                        await _apiKeysService.SetPexelsApiKeyAsync(pexelsKey);
                        Debug.WriteLine("[VetaleSearchSettingsPage] Pexels key saved to DB");
                        
                        // Update static key in ImageSearchServiceFactory for immediate effect
                        Debug.WriteLine("[VetaleSearchSettingsPage] Setting Pexels key in factory...");
                        VetaleBrowser.Search.Services.ImageSearchServiceFactory.SetPexelsApiKey(pexelsKey);
                        Debug.WriteLine("[VetaleSearchSettingsPage] ✓ Pexels API key saved and applied");
                    }
                    else
                    {
                        await _apiKeysService.RemoveApiKeyAsync(ApiServiceIds.Pexels);
                        VetaleBrowser.Search.Services.ImageSearchServiceFactory.SetPexelsApiKey(null);
                        Debug.WriteLine("[VetaleSearchSettingsPage] Pexels API key cleared");
                    }
                }

                // Unsplash
                if (_unsplashApiKeyInput != null)
                {
                    var unsplashKey = _unsplashApiKeyInput.Text?.Trim();
                    if (!string.IsNullOrWhiteSpace(unsplashKey))
                    {
                        await _apiKeysService.SetUnsplashApiKeyAsync(unsplashKey);
                        // Update static key in ImageSearchServiceFactory for immediate effect
                        VetaleBrowser.Search.Services.ImageSearchServiceFactory.SetUnsplashApiKey(unsplashKey);
                        Debug.WriteLine("[VetaleSearchSettingsPage] Unsplash API key saved and applied");
                    }
                    else
                    {
                        await _apiKeysService.RemoveApiKeyAsync(ApiServiceIds.Unsplash);
                        VetaleBrowser.Search.Services.ImageSearchServiceFactory.SetUnsplashApiKey(null);
                        Debug.WriteLine("[VetaleSearchSettingsPage] Unsplash API key cleared");
                    }
                }

                // YouTube
                if (_youTubeApiKeyInput != null)
                {
                    var youtubeKey = _youTubeApiKeyInput.Text?.Trim();
                    if (!string.IsNullOrWhiteSpace(youtubeKey))
                    {
                        await _apiKeysService.SetYouTubeApiKeyAsync(youtubeKey);
                        // Update static key in ImageSearchServiceFactory for immediate effect
                        VetaleBrowser.Search.Services.ImageSearchServiceFactory.SetYouTubeApiKey(youtubeKey);
                        Debug.WriteLine("[VetaleSearchSettingsPage] YouTube API key saved and applied");
                    }
                    else
                    {
                        await _apiKeysService.RemoveApiKeyAsync(ApiServiceIds.YouTube);
                        VetaleBrowser.Search.Services.ImageSearchServiceFactory.SetYouTubeApiKey(null);
                        Debug.WriteLine("[VetaleSearchSettingsPage] YouTube API key cleared");
                    }
                }

                // Save Color Settings
                if (_gradientStartColorInput != null && !string.IsNullOrWhiteSpace(_gradientStartColorInput.Text))
                {
                    await _apiKeysService.SetApiKeyAsync(SettingGradientStart, "VetaleSearch Gradient Start", _gradientStartColorInput.Text.Trim());
                }

                if (_gradientEndColorInput != null && !string.IsNullOrWhiteSpace(_gradientEndColorInput.Text))
                {
                    await _apiKeysService.SetApiKeyAsync(SettingGradientEnd, "VetaleSearch Gradient End", _gradientEndColorInput.Text.Trim());
                }

                if (_cardBackgroundInput != null && !string.IsNullOrWhiteSpace(_cardBackgroundInput.Text))
                {
                    await _apiKeysService.SetApiKeyAsync(SettingCardBackground, "VetaleSearch Card Background", _cardBackgroundInput.Text.Trim());
                }

                if (_linkColorInput != null && !string.IsNullOrWhiteSpace(_linkColorInput.Text))
                {
                    await _apiKeysService.SetApiKeyAsync(SettingLinkColor, "VetaleSearch Link Color", _linkColorInput.Text.Trim());
                }

                Debug.WriteLine("[VetaleSearchSettingsPage] All settings saved to database and applied to services");
            }

            SettingsSaved?.Invoke(this, EventArgs.Empty);
            Debug.WriteLine("[VetaleSearchSettingsPage] All settings saved successfully");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[VetaleSearchSettingsPage] Error saving settings: {ex.Message}");
        }
    }

    private async void OnResetClick(object? sender, RoutedEventArgs e)
    {
        // Reset color inputs to defaults
        if (_gradientStartColorInput != null) _gradientStartColorInput.Text = "#FF8A00";
        if (_gradientEndColorInput != null) _gradientEndColorInput.Text = "#9C27B0";
        if (_cardBackgroundInput != null) _cardBackgroundInput.Text = "#FFFFFF";
        if (_linkColorInput != null) _linkColorInput.Text = "#1565C0";

        // Clear API keys from UI
        if (_geminiApiKeyInput != null) _geminiApiKeyInput.Text = "";
        if (_pexelsApiKeyInput != null) _pexelsApiKeyInput.Text = "";
        if (_unsplashApiKeyInput != null) _unsplashApiKeyInput.Text = "";
        if (_youTubeApiKeyInput != null) _youTubeApiKeyInput.Text = "";

        // Remove ALL values from database
        if (_apiKeysService != null)
        {
            try
            {
                // Remove API keys from database
                await _apiKeysService.RemoveApiKeyAsync(ApiServiceIds.Gemini);
                await _apiKeysService.RemoveApiKeyAsync(ApiServiceIds.Pexels);
                await _apiKeysService.RemoveApiKeyAsync(ApiServiceIds.Unsplash);
                await _apiKeysService.RemoveApiKeyAsync(ApiServiceIds.YouTube);
                
                // Remove color settings from database
                await _apiKeysService.RemoveApiKeyAsync(SettingGradientStart);
                await _apiKeysService.RemoveApiKeyAsync(SettingGradientEnd);
                await _apiKeysService.RemoveApiKeyAsync(SettingCardBackground);
                await _apiKeysService.RemoveApiKeyAsync(SettingLinkColor);
                
                // Clear static caches in services
                VetaleBrowser.Search.Services.GeminiAiSummaryService.ClearCustomApiKey();
                VetaleBrowser.Search.Services.ImageSearchServiceFactory.SetPexelsApiKey(null);
                VetaleBrowser.Search.Services.ImageSearchServiceFactory.SetUnsplashApiKey(null);
                VetaleBrowser.Search.Services.ImageSearchServiceFactory.SetYouTubeApiKey(null);
                VetaleBrowser.Search.Services.ImageSearchServiceFactory.InvalidateCache();
                
                Debug.WriteLine("[VetaleSearchSettingsPage] All API keys and settings removed from database");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[VetaleSearchSettingsPage] Error removing settings: {ex.Message}");
            }
        }

        Debug.WriteLine("[VetaleSearchSettingsPage] Settings reset to defaults");
    }

    private void OnBackClick(object? sender, RoutedEventArgs e)
    {
        BackRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnOpenGeminiPortal(object? sender, RoutedEventArgs e)
    {
        OpenUrl("https://aistudio.google.com/app/apikey");
    }

    private void OnOpenPexelsPortal(object? sender, RoutedEventArgs e)
    {
        OpenUrl("https://www.pexels.com/api/");
    }

    private void OnOpenUnsplashPortal(object? sender, RoutedEventArgs e)
    {
        OpenUrl("https://unsplash.com/developers");
    }

    private void OnOpenYouTubePortal(object? sender, RoutedEventArgs e)
    {
        OpenUrl("https://console.cloud.google.com/apis/library/youtube.googleapis.com");
    }

    private void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[VetaleSearchSettingsPage] Error opening URL: {ex.Message}");
        }
    }
}

