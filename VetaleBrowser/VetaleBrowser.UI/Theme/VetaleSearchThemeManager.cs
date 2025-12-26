using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using VetaleBrowser.VetaleBrowser.Database.Services;

namespace VetaleBrowser.VetaleBrowser.UI.Theme;

/// <summary>
/// Loads and broadcasts Vetale Search theme colors stored in the local ApiKeys storage.
/// </summary>
public static class VetaleSearchThemeManager
{
    // Keys (must match VetaleSearchSettingsPage)
    private const string SettingGradientStart = "vetale_search_gradient_start";
    private const string SettingGradientEnd = "vetale_search_gradient_end";
    private const string SettingCardBackground = "vetale_search_card_bg";
    private const string SettingLinkColor = "vetale_search_link_color";

    // Defaults
    private const string DefaultGradientStart = "#FF8A00";
    private const string DefaultGradientEnd = "#9C27B0";
    private const string DefaultCardBackground = "#FFFFFF";
    private const string DefaultLinkColor = "#1565C0";

    public static event EventHandler? ThemeChanged;

    public static void NotifyThemeChanged()
    {
        try
        {
            ThemeChanged?.Invoke(null, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[VetaleSearchThemeManager] NotifyThemeChanged error: {ex}");
        }
    }

    public static async Task<VetaleSearchTheme> LoadAsync()
    {
        try
        {
            var apiKeys = DatabaseServicesFactory.TryGetApiKeysService();
            if (apiKeys == null)
                return VetaleSearchTheme.Default;

            var gradientStart = await apiKeys.GetApiKeyAsync(SettingGradientStart) ?? DefaultGradientStart;
            var gradientEnd = await apiKeys.GetApiKeyAsync(SettingGradientEnd) ?? DefaultGradientEnd;
            var cardBg = await apiKeys.GetApiKeyAsync(SettingCardBackground) ?? DefaultCardBackground;
            var linkColor = await apiKeys.GetApiKeyAsync(SettingLinkColor) ?? DefaultLinkColor;

            return new VetaleSearchTheme(
                gradientStart,
                gradientEnd,
                cardBg,
                linkColor);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[VetaleSearchThemeManager] LoadAsync error: {ex}");
            return VetaleSearchTheme.Default;
        }
    }

    /// <summary>
    /// Apply theme values into a resource container so XAML can use them via DynamicResource.
    ///
    /// We accept AvaloniaObject because different Avalonia versions expose resources on different
    /// interfaces (Application/Window/Control all work in this app).
    /// </summary>
    public static async Task ApplyToResourceHostAsync(AvaloniaObject? resourceHost)
    {
        VetaleSearchTheme theme;
        try
        {
            theme = await LoadAsync();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[VetaleSearchThemeManager] ApplyToResourceHostAsync: LoadAsync failed: {ex}");
            theme = VetaleSearchTheme.Default;
        }

        var host = resourceHost ?? (AvaloniaObject?)Application.Current;
        if (host == null)
            return;

        // Parse colors (fallback to defaults if DB contains a bad value)
        var startColor = Color.TryParse(theme.GradientStart, out var sc) ? sc : Color.Parse(DefaultGradientStart);
        var endColor = Color.TryParse(theme.GradientEnd, out var ec) ? ec : Color.Parse(DefaultGradientEnd);
        var cardColor = Color.TryParse(theme.CardBackground, out var cc) ? cc : Color.Parse(DefaultCardBackground);
        var linkColor = Color.TryParse(theme.LinkColor, out var lc) ? lc : Color.Parse(DefaultLinkColor);

        // IMPORTANT: apply resources inside UI thread.
        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
        {
            try
            {
                dynamic d = host;

                // === Canonical keys used by XAML ===
                // VetaleSearchResultsPage.axaml uses these as Color for GradientStop.Color.
                d.Resources["VetaleSearch.Theme.GradientStart"] = startColor;
                d.Resources["VetaleSearch.Theme.GradientEnd"] = endColor;
                d.Resources["VetaleSearch.Theme.CardBackground"] = new SolidColorBrush(cardColor);
                d.Resources["VetaleSearch.Theme.LinkColor"] = new SolidColorBrush(linkColor);

                // === Explicit typed keys (safe for future usage) ===
                d.Resources["VetaleSearch.Theme.GradientStart.Color"] = startColor;
                d.Resources["VetaleSearch.Theme.GradientEnd.Color"] = endColor;
                d.Resources["VetaleSearch.Theme.CardBackground.Color"] = cardColor;
                d.Resources["VetaleSearch.Theme.LinkColor.Color"] = linkColor;

                d.Resources["VetaleSearch.Theme.GradientStart.Brush"] = new SolidColorBrush(startColor);
                d.Resources["VetaleSearch.Theme.GradientEnd.Brush"] = new SolidColorBrush(endColor);
                d.Resources["VetaleSearch.Theme.CardBackground.Brush"] = new SolidColorBrush(cardColor);
                d.Resources["VetaleSearch.Theme.LinkColor.Brush"] = new SolidColorBrush(linkColor);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[VetaleSearchThemeManager] ApplyToResourceHostAsync: setting resources failed: {ex}");
            }
        });
    }

    public readonly record struct VetaleSearchTheme(string GradientStart, string GradientEnd, string CardBackground, string LinkColor)
    {
        public static VetaleSearchTheme Default => new(DefaultGradientStart, DefaultGradientEnd, DefaultCardBackground, DefaultLinkColor);

        public Color? TryParseGradientStart() => Color.TryParse(GradientStart, out var c) ? c : null;
        public Color? TryParseGradientEnd() => Color.TryParse(GradientEnd, out var c) ? c : null;
        public Color? TryParseCardBackground() => Color.TryParse(CardBackground, out var c) ? c : null;
        public Color? TryParseLinkColor() => Color.TryParse(LinkColor, out var c) ? c : null;
    }
}
