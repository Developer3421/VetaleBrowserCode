using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using VetaleBrowser.VetaleBrowser.Database;
using VetaleBrowser.VetaleBrowser.Database.Services;
using VetaleBrowser.VetaleBrowser.UI.Services;

namespace VetaleBrowser.VetaleBrowser.UI.Pages;

public partial class LanguageSettingsPage : UserControl
{
    public event EventHandler? BackRequested;
    public event EventHandler? SettingsSaved;

    private ComboBox? _combo;
    private ISettingsService? _settingsService;

    public LanguageSettingsPage()
    {
        InitializeComponent();
        InitializeAsync();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
        _combo = this.FindControl<ComboBox>("LanguageCombo");
    }

    private async void InitializeAsync()
    {
        try
        {
            var cfg = DatabaseConfiguration.CreateDefault();
            _settingsService = new SettingsService(cfg.DatabasePath, cfg.EncryptionKey);
            var current = await _settingsService.GetLanguageAsync();

            if (_combo != null)
            {
                var languages = LocalizationService.SupportedLanguages.ToList();
                _combo.ItemsSource = languages;
                
                var selected = languages.FirstOrDefault(x => x.Code == current) ?? languages.First();
                _combo.SelectedItem = selected;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"LanguageSettingsPage: init failed: {ex}");
        }
    }

    private async void OnSave(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        try
        {
            if (_settingsService == null || _combo?.SelectedItem is not LocalizationService.LanguageOption selected)
                return;

            await _settingsService.SetLanguageAsync(selected.Code);
            LocalizationService.ApplyLanguage(selected.Code);
            SettingsSaved?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"LanguageSettingsPage: save failed: {ex}");
        }
    }

    private void OnBack(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        BackRequested?.Invoke(this, EventArgs.Empty);
    }
}
