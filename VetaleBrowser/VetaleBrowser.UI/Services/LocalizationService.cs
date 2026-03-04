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
        System.Diagnostics.Debug.WriteLine($"[LocalizationService] ApplyLanguage called with code: {code}");
        
        if (Application.Current == null)
        {
            System.Diagnostics.Debug.WriteLine("[LocalizationService] Application.Current is null");
            return;
        }

        // Ensure we're on UI thread
        if (!Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() => ApplyLanguage(code));
            return;
        }

        // Try to load dictionary for the given code
        ResourceDictionary? dict = LoadLanguageDictionary(code);
        if (dict == null)
        {
            System.Diagnostics.Debug.WriteLine($"[LocalizationService] Failed to load dictionary for {code}, falling back to 'en'");
            // fallback to English
            dict = LoadLanguageDictionary("en");
            code = "en";
        }

        var appResources = Application.Current.Resources;

        // Remove previous language dictionary if exists
        if (_currentLanguageResources != null)
        {
            appResources.MergedDictionaries.Remove(_currentLanguageResources);
            System.Diagnostics.Debug.WriteLine("[LocalizationService] Removed previous language dictionary");
        }

        if (dict != null)
        {
            appResources.MergedDictionaries.Add(dict);
            _currentLanguageResources = dict;
            CurrentLanguageCode = code;
            System.Diagnostics.Debug.WriteLine($"[LocalizationService] Applied language: {code}");
        }
        else
        {
            System.Diagnostics.Debug.WriteLine("[LocalizationService] ERROR: Could not load any language dictionary");
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
            // Use a separate settings.db file
            var settingsDbPath = System.IO.Path.Combine(
                System.IO.Path.GetDirectoryName(cfg.DatabasePath) ?? "",
                "settings.db");
            System.Diagnostics.Debug.WriteLine($"[LocalizationService] Loading language from: {settingsDbPath}");
            
            using var settings = new SettingsService(settingsDbPath, cfg.EncryptionKey);
            var lang = await settings.GetLanguageAsync();
            System.Diagnostics.Debug.WriteLine($"[LocalizationService] Loaded language from settings: {lang}");
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
