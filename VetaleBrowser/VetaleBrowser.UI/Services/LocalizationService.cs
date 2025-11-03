using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using VetaleBrowser.VetaleBrowser.Database;
using VetaleBrowser.VetaleBrowser.Database.Services;

namespace VetaleBrowser.VetaleBrowser.UI.Services;

public static class LocalizationService
{
    public record LanguageOption(string Code, string DisplayName)
    {
        public override string ToString() => $"{DisplayName} ({Code})";
    }

    private static ResourceDictionary? _currentLanguageResources;

    public static IReadOnlyList<LanguageOption> SupportedLanguages { get; } = new List<LanguageOption>
    {
        new("en", "English"),
        new("uk", "Українська"),
        new("de", "Deutsch"),
        new("ru", "Русский"),
        new("tr", "Türkçe")
    };

    public static string CurrentLanguageCode { get; private set; } = "en";

    public static void ApplyLanguage(string code)
    {
        if (Application.Current == null)
            return;

        // Try to load dictionary for the given code
        ResourceDictionary? dict = LoadLanguageDictionary(code);
        if (dict == null)
        {
            // fallback to English
            dict = LoadLanguageDictionary("en");
            code = "en";
        }

        var appResources = Application.Current.Resources;

        // Remove previous language dictionary if exists
        if (_currentLanguageResources != null)
        {
            appResources.MergedDictionaries.Remove(_currentLanguageResources);
        }

        if (dict != null)
        {
            appResources.MergedDictionaries.Add(dict);
            _currentLanguageResources = dict;
            CurrentLanguageCode = code;
        }
    }

    private static ResourceDictionary? LoadLanguageDictionary(string code)
    {
        try
        {
            var uri = new Uri($"avares://VetaleBrowser/VetaleBrowser.UI/TranslationsDictionaries/Strings.{code}.axaml");
            var obj = AvaloniaXamlLoader.Load(uri);
            return obj as ResourceDictionary;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"LocalizationService: Failed to load dictionary for '{code}': {ex}");
            return null;
        }
    }

    public static async Task InitializeFromSettingsAsync()
    {
        try
        {
            var cfg = DatabaseConfiguration.CreateDefault();
            using var settings = new SettingsService(cfg.DatabasePath, cfg.EncryptionKey);
            var lang = await settings.GetLanguageAsync();
            ApplyLanguage(lang);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"LocalizationService: init from settings failed: {ex}");
            // Ensure at least English is applied
            ApplyLanguage("en");
        }
    }
}
