using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Layout;
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
    private bool _isInitialized;

    public LanguageSettingsPage()
    {
        InitializeComponent();
        this.AttachedToVisualTree += OnAttachedToVisualTree;
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
        _combo = this.FindControl<ComboBox>("LanguageCombo");
        System.Diagnostics.Debug.WriteLine($"[LanguageSettingsPage] InitializeComponent: _combo found = {_combo != null}");
        
        // Create ItemTemplate programmatically - use object type to avoid null issues
        if (_combo != null)
        {
            _combo.ItemTemplate = new FuncDataTemplate<object>((data, _) =>
            {
                var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
                
                if (data is LocalizationService.LanguageOption item)
                {
                    var nameBlock = new TextBlock { Text = item.DisplayName };
                    var codeBlock = new TextBlock { Text = $"({item.Code})", Opacity = 0.6 };
                    
                    panel.Children.Add(nameBlock);
                    panel.Children.Add(codeBlock);
                }
                else if (data != null)
                {
                    panel.Children.Add(new TextBlock { Text = data.ToString() ?? "" });
                }
                
                return panel;
            });
        }
    }

    private async void OnAttachedToVisualTree(object? sender, Avalonia.VisualTreeAttachmentEventArgs e)
    {
        if (_isInitialized) return;
        _isInitialized = true;
        
        System.Diagnostics.Debug.WriteLine("[LanguageSettingsPage] OnAttachedToVisualTree - starting init");
        await InitializeAsync();
    }

    private async System.Threading.Tasks.Task InitializeAsync()
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("[LanguageSettingsPage] InitializeAsync started");
            
            // Завжди спочатку заповнюємо ComboBox мовами
            if (_combo != null)
            {
                var languages = LocalizationService.SupportedLanguages.ToList();
                System.Diagnostics.Debug.WriteLine($"[LanguageSettingsPage] Languages count: {languages.Count}");
                foreach (var lang in languages)
                {
                    System.Diagnostics.Debug.WriteLine($"  - {lang.DisplayName} ({lang.Code})");
                }
                
                // Переконуємось що ItemTemplate встановлено
                if (_combo.ItemTemplate == null)
                {
                    System.Diagnostics.Debug.WriteLine("[LanguageSettingsPage] Setting ItemTemplate manually");
                    _combo.ItemTemplate = new FuncDataTemplate<object>((data, _) =>
                    {
                        var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
                        
                        if (data is LocalizationService.LanguageOption item)
                        {
                            panel.Children.Add(new TextBlock { Text = item.DisplayName });
                            panel.Children.Add(new TextBlock { Text = $"({item.Code})", Opacity = 0.6 });
                        }
                        else if (data != null)
                        {
                            panel.Children.Add(new TextBlock { Text = data.ToString() ?? "" });
                        }
                        
                        return panel;
                    });
                }
                
                _combo.ItemsSource = languages;
                _combo.MaxDropDownHeight = 300;
                
                System.Diagnostics.Debug.WriteLine($"[LanguageSettingsPage] ItemsSource set, count: {languages.Count}");
                
                // Default selection
                string current = "en";
                
                // Спробуємо завантажити поточну мову з налаштувань
                try
                {
                    var cfg = DatabaseConfiguration.CreateDefault();
                    // Використовуємо окремий файл settings.db замість browser.db
                    var settingsDbPath = System.IO.Path.Combine(
                        System.IO.Path.GetDirectoryName(cfg.DatabasePath) ?? "",
                        "settings.db");
                    _settingsService = new SettingsService(settingsDbPath, cfg.EncryptionKey);
                    current = await _settingsService.GetLanguageAsync();
                    System.Diagnostics.Debug.WriteLine($"[LanguageSettingsPage] Loaded current language from settings: {current}");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[LanguageSettingsPage] Failed to load settings, using default: {ex.Message}");
                    current = LocalizationService.CurrentLanguageCode;
                }
                
                var selected = languages.FirstOrDefault(x => x.Code == current) ?? languages.First();
                _combo.SelectedItem = selected;
                
                System.Diagnostics.Debug.WriteLine($"[LanguageSettingsPage] Selected: {selected}, SelectedIndex: {_combo.SelectedIndex}");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("[LanguageSettingsPage] ERROR: _combo is null!");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LanguageSettingsPage] init failed: {ex}");
        }
    }

    private async void OnSave(object? sender, RoutedEventArgs e)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("[LanguageSettingsPage] OnSave called");
            
            // Перевіряємо вибір
            if (_combo?.SelectedItem is not LocalizationService.LanguageOption selected)
            {
                System.Diagnostics.Debug.WriteLine("[LanguageSettingsPage] Save: no language selected");
                return;
            }
            
            System.Diagnostics.Debug.WriteLine($"[LanguageSettingsPage] Saving language: {selected.Code}");
            
            // Ініціалізуємо settings service якщо ще не ініціалізований
            if (_settingsService == null)
            {
                try
                {
                    var cfg = DatabaseConfiguration.CreateDefault();
                    var settingsDbPath = System.IO.Path.Combine(
                        System.IO.Path.GetDirectoryName(cfg.DatabasePath) ?? "",
                        "settings.db");
                    _settingsService = new SettingsService(settingsDbPath, cfg.EncryptionKey);
                    System.Diagnostics.Debug.WriteLine("[LanguageSettingsPage] Settings service initialized in OnSave");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[LanguageSettingsPage] Failed to init settings service: {ex.Message}");
                }
            }
            
            // Зберігаємо в базу даних
            if (_settingsService != null)
            {
                await _settingsService.SetLanguageAsync(selected.Code);
                System.Diagnostics.Debug.WriteLine($"[LanguageSettingsPage] Language saved to DB: {selected.Code}");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("[LanguageSettingsPage] WARNING: Could not save to DB - settings service is null");
            }
            
            // Застосовуємо мову до UI (це працює навіть без збереження в БД)
            LocalizationService.ApplyLanguage(selected.Code);
            System.Diagnostics.Debug.WriteLine($"[LanguageSettingsPage] Language applied to UI: {selected.Code}");
            
            // Сповіщаємо про збереження
            SettingsSaved?.Invoke(this, EventArgs.Empty);
            
            System.Diagnostics.Debug.WriteLine("[LanguageSettingsPage] Save completed successfully");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LanguageSettingsPage] save failed: {ex}");
        }
    }

    private void OnBack(object? sender, RoutedEventArgs e)
    {
        BackRequested?.Invoke(this, EventArgs.Empty);
    }
}
