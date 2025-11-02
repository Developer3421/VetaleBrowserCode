using System;
using System.IO;
using System.Threading.Tasks;
using LiteDB;
using VetaleBrowser.VetaleBrowser.Database.Models;

namespace VetaleBrowser.VetaleBrowser.Database.Services;

/// <summary>
/// Сервіс для роботи з налаштуваннями браузера
/// </summary>
public class SettingsService : ISettingsService, IDisposable
{
    private readonly LiteDatabase _database;
    private readonly DatabaseEncryptionService _encryptionService;
    private readonly ILiteCollection<SettingItem> _settingsCollection;
    
    private const string DefaultSearchEngine = "Google";
    private const string DefaultSearchEngineUrl = "https://www.google.com/search?q={0}";

    public SettingsService(string databasePath, string encryptionKey)
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
        _settingsCollection = _database.GetCollection<SettingItem>("settings");
        
        // Створюємо індекс для ключа
        _settingsCollection.EnsureIndex(x => x.Key, true); // true = unique
    }

    public async Task<string> GetSearchEngineUrlAsync()
    {
        return await Task.Run(() =>
        {
            var setting = _settingsCollection.FindOne(x => x.Key == "SearchEngineUrl");
            if (setting == null)
                return DefaultSearchEngineUrl;
                
            return _encryptionService.DecryptString(setting.EncryptedValue);
        });
    }

    public async Task SetSearchEngineUrlAsync(string url)
    {
        await Task.Run(() =>
        {
            var encryptedValue = _encryptionService.EncryptString(url);
            var setting = new SettingItem
            {
                Key = "SearchEngineUrl",
                EncryptedValue = encryptedValue,
                UpdatedAt = DateTime.UtcNow
            };
            
            _settingsCollection.Upsert(setting);
        });
    }

    public async Task<string> GetSearchEngineNameAsync()
    {
        return await Task.Run(() =>
        {
            var setting = _settingsCollection.FindOne(x => x.Key == "SearchEngineName");
            if (setting == null)
                return DefaultSearchEngine;
                
            return _encryptionService.DecryptString(setting.EncryptedValue);
        });
    }

    public async Task SetSearchEngineNameAsync(string name)
    {
        await Task.Run(() =>
        {
            var encryptedValue = _encryptionService.EncryptString(name);
            var setting = new SettingItem
            {
                Key = "SearchEngineName",
                EncryptedValue = encryptedValue,
                UpdatedAt = DateTime.UtcNow
            };
            
            _settingsCollection.Upsert(setting);
        });
    }

    public async Task SetSearchEngineAsync(string name, string url)
    {
        await SetSearchEngineNameAsync(name);
        await SetSearchEngineUrlAsync(url);
    }

    public void Dispose()
    {
        _database.Dispose();
    }
}
