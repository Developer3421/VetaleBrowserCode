using System;
using System.IO;
using System.Threading.Tasks;
using LiteDB;
using VetaleBrowser.VetaleBrowser.Database.Models;

namespace VetaleBrowser.VetaleBrowser.Database.Services;

/// <summary>
/// Сервіс для роботи з налаштуваннями вигляду браузера з AES шифруванням
/// </summary>
public class AppearanceSettingsService : IAppearanceSettingsService, IDisposable
{
    private readonly LiteDatabase _database;
    private readonly DatabaseEncryptionService _encryptionService;
    private readonly ILiteCollection<SettingItem> _settingsCollection;
    
    // Default Tab Appearance values
    private const double DefaultTabWidth = 200.0;
    private const double DefaultTabElementSize = 16.0;
    private const string DefaultTabBackgroundColor = "#F5F5F5";
    private const string DefaultTabTextColor = "#000000";
    private const string DefaultTabActiveColor = "#9A1CE8";
    private const double DefaultTabIconSize = 16.0;
    
    // Default Main Window values
    private const string DefaultNavigationBarColor = ""; // keep XAML PurpleGradient by default
    private const double DefaultNavigationBarHeight = 40.0;
    private const double DefaultMainWindowWidth = 1200.0;
    private const double DefaultMainWindowHeight = 800.0;
    private const string DefaultTopBarBackgroundColor = ""; // keep XAML GrayGradient by default
    private const double DefaultButtonSize = 32.0;
    private const double DefaultButtonIconSize = 16.0;
    
    // Default Other Windows values
    private const string DefaultOtherWindowsBackgroundColor = "#FFFFFF";
    private const string DefaultOtherWindowsTopBarColor = ""; // keep XAML GrayGradient by default

    // Legacy defaults (for migrating previously saved values so new defaults "win")
    private const string LegacyTopBarBackgroundColor = "#8B4513";
    private const string LegacyOtherWindowsTopBarColor = "#9A1CE8";

    public AppearanceSettingsService(string databasePath, string encryptionKey)
    {
        _encryptionService = new DatabaseEncryptionService(encryptionKey);
        
        // Створюємо директорію для бази даних якщо не існує
        var directory = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // Ініціалізуємо базу даних
        var connectionString = new ConnectionString
        {
            Filename = databasePath,
            Connection = ConnectionType.Shared
        };

        _database = new LiteDatabase(connectionString);
        
        // Отримуємо колекцію
        _settingsCollection = _database.GetCollection<SettingItem>("appearance_settings");
        
        // Створюємо індекс для ключа
        _settingsCollection.EnsureIndex(x => x.Key, true); // true = unique
    }

    #region Helper Methods

    private async Task<string> GetStringSettingAsync(string key, string defaultValue)
    {
        return await Task.Run(() =>
        {
            var setting = _settingsCollection.FindOne(x => x.Key == key);
            if (setting == null)
                return defaultValue;
                
            return _encryptionService.DecryptString(setting.EncryptedValue);
        });
    }

    private async Task SetStringSettingAsync(string key, string value)
    {
        await Task.Run(() =>
        {
            var encryptedValue = _encryptionService.EncryptString(value);
            var existing = _settingsCollection.FindOne(x => x.Key == key);
            if (existing != null)
            {
                existing.EncryptedValue = encryptedValue;
                existing.UpdatedAt = DateTime.UtcNow;
                _settingsCollection.Update(existing);
            }
            else
            {
                var setting = new SettingItem
                {
                    Key = key,
                    EncryptedValue = encryptedValue,
                    UpdatedAt = DateTime.UtcNow
                };
                _settingsCollection.Insert(setting);
            }
        });
    }

    private async Task<double> GetDoubleSettingAsync(string key, double defaultValue)
    {
        return await Task.Run(() =>
        {
            var setting = _settingsCollection.FindOne(x => x.Key == key);
            if (setting == null)
                return defaultValue;
                
            var decryptedValue = _encryptionService.DecryptString(setting.EncryptedValue);
            return double.TryParse(decryptedValue, out var result) ? result : defaultValue;
        });
    }

    private async Task SetDoubleSettingAsync(string key, double value)
    {
        await SetStringSettingAsync(key, value.ToString());
    }

    #endregion

    #region Tab Appearance Settings

    public async Task<double> GetTabWidthAsync()
    {
        return await GetDoubleSettingAsync("TabWidth", DefaultTabWidth);
    }

    public async Task SetTabWidthAsync(double width)
    {
        await SetDoubleSettingAsync("TabWidth", width);
    }

    public async Task<double> GetTabElementSizeAsync()
    {
        return await GetDoubleSettingAsync("TabElementSize", DefaultTabElementSize);
    }

    public async Task SetTabElementSizeAsync(double size)
    {
        await SetDoubleSettingAsync("TabElementSize", size);
    }

    public async Task<string> GetTabBackgroundColorAsync()
    {
        return await GetStringSettingAsync("TabBackgroundColor", DefaultTabBackgroundColor);
    }

    public async Task SetTabBackgroundColorAsync(string color)
    {
        await SetStringSettingAsync("TabBackgroundColor", color);
    }

    public async Task<string> GetTabTextColorAsync()
    {
        return await GetStringSettingAsync("TabTextColor", DefaultTabTextColor);
    }

    public async Task SetTabTextColorAsync(string color)
    {
        await SetStringSettingAsync("TabTextColor", color);
    }

    public async Task<string> GetTabActiveColorAsync()
    {
        return await GetStringSettingAsync("TabActiveColor", DefaultTabActiveColor);
    }

    public async Task SetTabActiveColorAsync(string color)
    {
        await SetStringSettingAsync("TabActiveColor", color);
    }

    public async Task<double> GetTabIconSizeAsync()
    {
        return await GetDoubleSettingAsync("TabIconSize", DefaultTabIconSize);
    }

    public async Task SetTabIconSizeAsync(double size)
    {
        await SetDoubleSettingAsync("TabIconSize", size);
    }

    #endregion

    #region Main Window Appearance Settings

    public async Task<string> GetNavigationBarColorAsync()
    {
        var value = await GetStringSettingAsync("NavigationBarColor", DefaultNavigationBarColor);
        // If legacy default #E0E0E0 was stored, treat as empty to prefer gradient
        if (string.Equals(value, "#E0E0E0", StringComparison.OrdinalIgnoreCase))
            return string.Empty;
        return value;
    }

    public async Task SetNavigationBarColorAsync(string color)
    {
        await SetStringSettingAsync("NavigationBarColor", color);
    }

    public async Task<double> GetNavigationBarHeightAsync()
    {
        return await GetDoubleSettingAsync("NavigationBarHeight", DefaultNavigationBarHeight);
    }

    public async Task SetNavigationBarHeightAsync(double height)
    {
        await SetDoubleSettingAsync("NavigationBarHeight", height);
    }

    public async Task<double> GetMainWindowWidthAsync()
    {
        return await GetDoubleSettingAsync("MainWindowWidth", DefaultMainWindowWidth);
    }

    public async Task SetMainWindowWidthAsync(double width)
    {
        await SetDoubleSettingAsync("MainWindowWidth", width);
    }

    public async Task<double> GetMainWindowHeightAsync()
    {
        return await GetDoubleSettingAsync("MainWindowHeight", DefaultMainWindowHeight);
    }

    public async Task SetMainWindowHeightAsync(double height)
    {
        await SetDoubleSettingAsync("MainWindowHeight", height);
    }

    public async Task<string> GetTopBarBackgroundColorAsync()
    {
        var value = await GetStringSettingAsync("TopBarBackgroundColor", DefaultTopBarBackgroundColor);
        // Migrate legacy default to new default (empty) so XAML GrayGradient stays by default
        if (string.Equals(value, LegacyTopBarBackgroundColor, StringComparison.OrdinalIgnoreCase))
            return string.Empty;
        return value;
    }

    public async Task SetTopBarBackgroundColorAsync(string color)
    {
        await SetStringSettingAsync("TopBarBackgroundColor", color);
    }

    public async Task<double> GetButtonSizeAsync()
    {
        return await GetDoubleSettingAsync("ButtonSize", DefaultButtonSize);
    }

    public async Task SetButtonSizeAsync(double size)
    {
        await SetDoubleSettingAsync("ButtonSize", size);
    }

    public async Task<double> GetButtonIconSizeAsync()
    {
        return await GetDoubleSettingAsync("ButtonIconSize", DefaultButtonIconSize);
    }

    public async Task SetButtonIconSizeAsync(double size)
    {
        await SetDoubleSettingAsync("ButtonIconSize", size);
    }

    #endregion

    #region Other Windows Appearance Settings

    public async Task<string> GetOtherWindowsBackgroundColorAsync()
    {
        return await GetStringSettingAsync("OtherWindowsBackgroundColor", DefaultOtherWindowsBackgroundColor);
    }

    public async Task SetOtherWindowsBackgroundColorAsync(string color)
    {
        await SetStringSettingAsync("OtherWindowsBackgroundColor", color);
    }

    public async Task<string> GetOtherWindowsTopBarColorAsync()
    {
        var value = await GetStringSettingAsync("OtherWindowsTopBarColor", DefaultOtherWindowsTopBarColor);
        if (string.Equals(value, LegacyOtherWindowsTopBarColor, StringComparison.OrdinalIgnoreCase))
            return string.Empty;
        return value;
    }

    public async Task SetOtherWindowsTopBarColorAsync(string color)
    {
        await SetStringSettingAsync("OtherWindowsTopBarColor", color);
    }

    #endregion

    public void Dispose()
    {
        _database?.Dispose();
    }
}
