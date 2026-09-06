using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using VetaleBrowser.VetaleBrowser.Database.Services;
using VetaleBrowser.VetaleBrowser.UI.Theme;

namespace VetaleBrowser.VetaleBrowser.UI.Pages;

public partial class VetaleSearchSettingsPage : UserControl
{
    public event EventHandler? BackRequested;
    public event EventHandler? SettingsSaved;

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

    private static bool IsValidColorString(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        // Accept anything Avalonia can parse as a color (#RRGGBB, #AARRGGBB, named colors, etc.)
        return Color.TryParse(value.Trim(), out _);
    }

    private static string CoerceValidColorOrDefault(string? value, string defaultValue)
    {
        return IsValidColorString(value) ? value!.Trim() : defaultValue;
    }

    private async Task LoadSettingsAsync()
    {
        try
        {
            // Appearance (colors stored via API keys service for simplicity)
            if (_apiKeysService != null)
            {
                if (_gradientStartColorInput != null)
                {
                    var raw = await _apiKeysService.GetApiKeyAsync(SettingGradientStart);
                    var gradientStart = CoerceValidColorOrDefault(raw, "#FF8A00");
                    _gradientStartColorInput.Text = gradientStart;
                    UpdateColorPreview(_gradientStartColorInput, _gradientStartPreview);

                    if (!IsValidColorString(raw))
                        await _apiKeysService.SetApiKeyAsync(SettingGradientStart, "Gradient start", gradientStart);
                }

                if (_gradientEndColorInput != null)
                {
                    var raw = await _apiKeysService.GetApiKeyAsync(SettingGradientEnd);
                    var gradientEnd = CoerceValidColorOrDefault(raw, "#9C27B0");
                    _gradientEndColorInput.Text = gradientEnd;
                    UpdateColorPreview(_gradientEndColorInput, _gradientEndPreview);

                    if (!IsValidColorString(raw))
                        await _apiKeysService.SetApiKeyAsync(SettingGradientEnd, "Gradient end", gradientEnd);
                }

                if (_cardBackgroundInput != null)
                {
                    var raw = await _apiKeysService.GetApiKeyAsync(SettingCardBackground);
                    var cardBg = CoerceValidColorOrDefault(raw, "#FFFFFF");
                    _cardBackgroundInput.Text = cardBg;
                    UpdateColorPreview(_cardBackgroundInput, _cardBackgroundPreview);

                    if (!IsValidColorString(raw))
                        await _apiKeysService.SetApiKeyAsync(SettingCardBackground, "Card background", cardBg);
                }

                if (_linkColorInput != null)
                {
                    var raw = await _apiKeysService.GetApiKeyAsync(SettingLinkColor);
                    var linkColor = CoerceValidColorOrDefault(raw, "#1565C0");
                    _linkColorInput.Text = linkColor;
                    UpdateColorPreview(_linkColorInput, _linkColorPreview);

                    if (!IsValidColorString(raw))
                        await _apiKeysService.SetApiKeyAsync(SettingLinkColor, "Link color", linkColor);
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
            // Save appearance (colors)
            if (_apiKeysService != null)
            {
                // Save color settings (stored as api keys)
                await SaveColorSettingAsync(SettingGradientStart, _gradientStartColorInput?.Text, "Gradient start");
                await SaveColorSettingAsync(SettingGradientEnd, _gradientEndColorInput?.Text, "Gradient end");
                await SaveColorSettingAsync(SettingCardBackground, _cardBackgroundInput?.Text, "Card background");
                await SaveColorSettingAsync(SettingLinkColor, _linkColorInput?.Text, "Link color");

                SettingsSaved?.Invoke(this, EventArgs.Empty);
            }

            // Show success message
            ShowStatusMessage(GetLocalizedString("Common.Saved", "Saved!"), false);

            // Apply immediately
            try
            {
                if (Application.Current != null)
                    await VetaleSearchThemeManager.ApplyToResourceHostAsync(Application.Current);

                VetaleSearchThemeManager.NotifyThemeChanged();
            }
            catch (Exception themeEx)
            {
                Debug.WriteLine($"[VetaleSearchSettingsPage] Apply theme after save failed: {themeEx.Message}");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[VetaleSearchSettingsPage] Error saving settings: {ex.Message}");
            ShowStatusMessage($"Error: {ex.Message}", true);
        }
    }

    private async void OnResetClick(object? sender, RoutedEventArgs e)
    {
        // Reset color inputs to defaults
        if (_gradientStartColorInput != null) _gradientStartColorInput.Text = "#FF8A00";
        if (_gradientEndColorInput != null) _gradientEndColorInput.Text = "#9C27B0";
        if (_cardBackgroundInput != null) _cardBackgroundInput.Text = "#FFFFFF";
        if (_linkColorInput != null) _linkColorInput.Text = "#1565C0";

        // Remove appearance values from database
        if (_apiKeysService != null)
        {
            try
            {
                // Remove color settings from database
                await _apiKeysService.RemoveApiKeyAsync(SettingGradientStart);
                await _apiKeysService.RemoveApiKeyAsync(SettingGradientEnd);
                await _apiKeysService.RemoveApiKeyAsync(SettingCardBackground);
                await _apiKeysService.RemoveApiKeyAsync(SettingLinkColor);

                Debug.WriteLine("[VetaleSearchSettingsPage] Appearance settings removed from database");
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

    private async Task SaveColorSettingAsync(string key, string? value, string debugName)
    {
        if (_apiKeysService == null)
            return;

        try
        {
            var trimmed = value?.Trim();
            if (!IsValidColorString(trimmed))
            {
                Debug.WriteLine($"[VetaleSearchSettingsPage] {debugName} invalid color '{trimmed}', saving defaults instead");

                // Use matching defaults per key
                var fallback = key switch
                {
                    SettingGradientStart => "#FF8A00",
                    SettingGradientEnd => "#9C27B0",
                    SettingCardBackground => "#FFFFFF",
                    SettingLinkColor => "#1565C0",
                    _ => "#FFFFFF"
                };

                await _apiKeysService.SetApiKeyAsync(key, debugName, fallback);
                return;
            }

            await _apiKeysService.SetApiKeyAsync(key, debugName, trimmed!);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[VetaleSearchSettingsPage] Error saving {debugName}: {ex.Message}");
        }
    }

    private string GetLocalizedString(string key, string fallback)
    {
        try
        {
            if (Application.Current?.TryFindResource(key, out var value) == true && value is string s)
                return s;
        }
        catch
        {
            // ignore
        }

        return fallback;
    }

    private void ShowStatusMessage(string message, bool isError)
    {
        try
        {
            // Try find a status text block if the XAML has it (optional)
            var status = this.FindControl<TextBlock>("StatusTextBlock");
            if (status != null)
            {
                status.IsVisible = true;
                status.Text = message;
                status.Foreground = isError
                    ? new SolidColorBrush(Color.Parse("#C62828"))
                    : new SolidColorBrush(Color.Parse("#2E7D32"));
            }
            else
            {
                Debug.WriteLine($"[VetaleSearchSettingsPage] Status: {message}");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[VetaleSearchSettingsPage] Failed to show status: {ex.Message}");
        }
    }
}
