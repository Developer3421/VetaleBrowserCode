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
        
        System.Diagnostics.Debug.WriteLine($"[SettingsService] Creating with path: {databasePath}");
        
        // Використовуємо оптимізоване з'єднання з мінімальним споживанням RAM
        _database = DatabaseConfiguration.CreateOptimizedDatabase(databasePath);
        
        // Отримуємо колекцію
        _settingsCollection = _database.GetCollection<SettingItem>("settings");
        
        // Один унікальний індекс
        _settingsCollection.EnsureIndex(x => x.Key, true);
        
        System.Diagnostics.Debug.WriteLine("[SettingsService] Initialized successfully");
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
            var existing = _settingsCollection.FindOne(x => x.Key == "SearchEngineUrl");
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
                    Key = "SearchEngineUrl",
                    EncryptedValue = encryptedValue,
                    UpdatedAt = DateTime.UtcNow
                };
                _settingsCollection.Insert(setting);
            }
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
            var existing = _settingsCollection.FindOne(x => x.Key == "SearchEngineName");
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
                    Key = "SearchEngineName",
                    EncryptedValue = encryptedValue,
                    UpdatedAt = DateTime.UtcNow
                };
                _settingsCollection.Insert(setting);
            }
        });
    }

    public async Task SetSearchEngineAsync(string name, string url)
    {
        await SetSearchEngineNameAsync(name);
        await SetSearchEngineUrlAsync(url);
    }

    public async Task<string> GetLanguageAsync()
    {
        return await Task.Run(() =>
        {
            var setting = _settingsCollection.FindOne(x => x.Key == "UILanguage");
            if (setting == null)
                return "en";

            var code = _encryptionService.DecryptString(setting.EncryptedValue);
            return string.IsNullOrWhiteSpace(code) ? "en" : code;
        });
    }

    public async Task SetLanguageAsync(string code)
    {
        await Task.Run(() =>
        {
            if (string.IsNullOrWhiteSpace(code)) code = "en";
            var encryptedValue = _encryptionService.EncryptString(code);
            var existing = _settingsCollection.FindOne(x => x.Key == "UILanguage");
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
                    Key = "UILanguage",
                    EncryptedValue = encryptedValue,
                    UpdatedAt = DateTime.UtcNow
                };
                _settingsCollection.Insert(setting);
            }
        });
    }

    public async Task<DateTime?> GetLastDownloadsScanUtcAsync()
    {
        return await Task.Run(() =>
        {
            var setting = _settingsCollection.FindOne(x => x.Key == "LastDownloadsScanUtc");
            if (setting == null) return (DateTime?)null;
            var decrypted = _encryptionService.DecryptString(setting.EncryptedValue);
            if (DateTime.TryParse(decrypted, out var dt))
                return dt;
            return (DateTime?)null;
        });
    }

    public async Task SetLastDownloadsScanUtcAsync(DateTime utc)
    {
        await Task.Run(() =>
        {
            var value = utc.ToString("o"); // ISO 8601
            var encryptedValue = _encryptionService.EncryptString(value);
            var existing = _settingsCollection.FindOne(x => x.Key == "LastDownloadsScanUtc");
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
                    Key = "LastDownloadsScanUtc",
                    EncryptedValue = encryptedValue,
                    UpdatedAt = DateTime.UtcNow
                };
                _settingsCollection.Insert(setting);
            }
        });
    }

    public async Task<bool> IsUserAgreementAcceptedAsync()
    {
        return await Task.Run(() =>
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[SettingsService] IsUserAgreementAcceptedAsync: Checking...");
                var setting = _settingsCollection.FindOne(x => x.Key == "UserAgreementAccepted");
                
                if (setting == null)
                {
                    System.Diagnostics.Debug.WriteLine($"[SettingsService] IsUserAgreementAcceptedAsync: Setting not found, returning false");
                    return false;
                }

                var decrypted = _encryptionService.DecryptString(setting.EncryptedValue);
                var result = decrypted == "true";
                System.Diagnostics.Debug.WriteLine($"[SettingsService] IsUserAgreementAcceptedAsync: Found setting, decrypted='{decrypted}', result={result}");
                return result;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SettingsService] IsUserAgreementAcceptedAsync ERROR: {ex.Message}");
                return false;
            }
        });
    }

    public async Task SetUserAgreementAcceptedAsync(bool accepted)
    {
        await Task.Run(() =>
        {
            var value = accepted ? "true" : "false";
            var encryptedValue = _encryptionService.EncryptString(value);
            var existing = _settingsCollection.FindOne(x => x.Key == "UserAgreementAccepted");
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
                    Key = "UserAgreementAccepted",
                    EncryptedValue = encryptedValue,
                    UpdatedAt = DateTime.UtcNow
                };
                _settingsCollection.Insert(setting);
            }

            // Also save the acceptance date
            if (accepted)
            {
                var dateValue = DateTime.UtcNow.ToString("o");
                var encryptedDate = _encryptionService.EncryptString(dateValue);
                var existingDate = _settingsCollection.FindOne(x => x.Key == "UserAgreementDate");
                if (existingDate != null)
                {
                    existingDate.EncryptedValue = encryptedDate;
                    existingDate.UpdatedAt = DateTime.UtcNow;
                    _settingsCollection.Update(existingDate);
                }
                else
                {
                    var dateSetting = new SettingItem
                    {
                        Key = "UserAgreementDate",
                        EncryptedValue = encryptedDate,
                        UpdatedAt = DateTime.UtcNow
                    };
                    _settingsCollection.Insert(dateSetting);
                }
            }
            
            // Force checkpoint to ensure data is written to disk
            _database.Checkpoint();
            System.Diagnostics.Debug.WriteLine($"[SettingsService] UserAgreementAccepted set to {accepted} and checkpointed");
        });
    }

    public async Task<DateTime?> GetUserAgreementDateAsync()
    {
        return await Task.Run(() =>
        {
            var setting = _settingsCollection.FindOne(x => x.Key == "UserAgreementDate");
            if (setting == null) return (DateTime?)null;
            var decrypted = _encryptionService.DecryptString(setting.EncryptedValue);
            if (DateTime.TryParse(decrypted, out var dt))
                return dt;
            return (DateTime?)null;
        });
    }

    public void Dispose()
    {
        _database.Dispose();
    }
}
