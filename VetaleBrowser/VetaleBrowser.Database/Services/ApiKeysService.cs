using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LiteDB;
using VetaleBrowser.VetaleBrowser.Database.Models;

namespace VetaleBrowser.VetaleBrowser.Database.Services;

/// <summary>
/// Service for working with API keys using LiteDB and AES encryption
/// </summary>
public class ApiKeysService : IApiKeysService, IDisposable
{
    private readonly LiteDatabase _database;
    private readonly DatabaseEncryptionService _encryptionService;
    private readonly ILiteCollection<ApiKeyItem> _apiKeysCollection;
    private bool _disposed;

    public ApiKeysService(string databasePath, string encryptionKey)
    {
        _encryptionService = new DatabaseEncryptionService(encryptionKey);
        
        System.Diagnostics.Debug.WriteLine($"[ApiKeysService] Creating with path: {databasePath}");
        
        // Use optimized connection with minimal RAM usage
        _database = DatabaseConfiguration.CreateOptimizedDatabase(databasePath);
        
        // Get collection
        _apiKeysCollection = _database.GetCollection<ApiKeyItem>("api_keys");
        
        // Unique index by ServiceId
        _apiKeysCollection.EnsureIndex(x => x.ServiceId, true);
        
        System.Diagnostics.Debug.WriteLine("[ApiKeysService] Initialized successfully");
    }

    // ==================== General methods ====================

    public async Task<string?> GetApiKeyAsync(string serviceId)
    {
        return await Task.Run(() =>
        {
            try
            {
                var item = _apiKeysCollection.FindOne(x => x.ServiceId == serviceId && x.IsEnabled);
                if (item == null || string.IsNullOrEmpty(item.EncryptedApiKey))
                    return null;
                
                return _encryptionService.DecryptString(item.EncryptedApiKey);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ApiKeysService] Error getting API key for {serviceId}: {ex.Message}");
                return null;
            }
        }).ConfigureAwait(false);
    }

    public async Task SetApiKeyAsync(string serviceId, string serviceName, string apiKey)
    {
        await Task.Run(() =>
        {
            try
            {
                var encryptedKey = _encryptionService.EncryptString(apiKey);
                var existing = _apiKeysCollection.FindOne(x => x.ServiceId == serviceId);
                
                if (existing != null)
                {
                    existing.EncryptedApiKey = encryptedKey;
                    existing.ServiceName = serviceName;
                    existing.IsEnabled = true;
                    existing.UpdatedAt = DateTime.UtcNow;
                    _apiKeysCollection.Update(existing);
                    System.Diagnostics.Debug.WriteLine($"[ApiKeysService] Updated API key for {serviceId}");
                }
                else
                {
                    var newItem = new ApiKeyItem
                    {
                        ServiceId = serviceId,
                        ServiceName = serviceName,
                        EncryptedApiKey = encryptedKey,
                        IsEnabled = true,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    _apiKeysCollection.Insert(newItem);
                    System.Diagnostics.Debug.WriteLine($"[ApiKeysService] Created API key for {serviceId}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ApiKeysService] Error setting API key for {serviceId}: {ex.Message}");
            }
        }).ConfigureAwait(false);
    }

    public async Task RemoveApiKeyAsync(string serviceId)
    {
        await Task.Run(() =>
        {
            try
            {
                _apiKeysCollection.DeleteMany(x => x.ServiceId == serviceId);
                System.Diagnostics.Debug.WriteLine($"[ApiKeysService] Removed API key for {serviceId}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ApiKeysService] Error removing API key for {serviceId}: {ex.Message}");
            }
        }).ConfigureAwait(false);
    }

    public async Task<bool> HasApiKeyAsync(string serviceId)
    {
        return await Task.Run(() =>
        {
            try
            {
                var item = _apiKeysCollection.FindOne(x => x.ServiceId == serviceId && x.IsEnabled);
                return item != null && !string.IsNullOrEmpty(item.EncryptedApiKey);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ApiKeysService] Error checking API key for {serviceId}: {ex.Message}");
                return false;
            }
        }).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ApiKeyItem>> GetAllApiKeysAsync()
    {
        return await Task.Run(() =>
        {
            try
            {
                return _apiKeysCollection.FindAll().ToList().AsReadOnly();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ApiKeysService] Error getting all API keys: {ex.Message}");
                return new List<ApiKeyItem>().AsReadOnly();
            }
        }).ConfigureAwait(false);
    }

    public async Task RecordSuccessfulUseAsync(string serviceId)
    {
        await Task.Run(() =>
        {
            try
            {
                var item = _apiKeysCollection.FindOne(x => x.ServiceId == serviceId);
                if (item != null)
                {
                    item.LastUsedAt = DateTime.UtcNow;
                    item.SuccessfulRequestsCount++;
                    _apiKeysCollection.Update(item);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ApiKeysService] Error recording successful use for {serviceId}: {ex.Message}");
            }
        }).ConfigureAwait(false);
    }

    public async Task RecordFailedUseAsync(string serviceId)
    {
        await Task.Run(() =>
        {
            try
            {
                var item = _apiKeysCollection.FindOne(x => x.ServiceId == serviceId);
                if (item != null)
                {
                    item.LastUsedAt = DateTime.UtcNow;
                    item.FailedRequestsCount++;
                    _apiKeysCollection.Update(item);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ApiKeysService] Error recording failed use for {serviceId}: {ex.Message}");
            }
        }).ConfigureAwait(false);
    }

    // ==================== Specialized methods for services ====================

    public Task<string?> GetGeminiApiKeyAsync()
    {
        return GetApiKeyAsync(ApiServiceIds.Gemini);
    }

    public Task SetGeminiApiKeyAsync(string apiKey)
    {
        return SetApiKeyAsync(ApiServiceIds.Gemini, "Google Gemini", apiKey);
    }

    public Task<string?> GetPexelsApiKeyAsync()
    {
        return GetApiKeyAsync(ApiServiceIds.Pexels);
    }

    public Task SetPexelsApiKeyAsync(string apiKey)
    {
        return SetApiKeyAsync(ApiServiceIds.Pexels, "Pexels", apiKey);
    }

    public Task<string?> GetUnsplashApiKeyAsync()
    {
        return GetApiKeyAsync(ApiServiceIds.Unsplash);
    }

    public Task SetUnsplashApiKeyAsync(string apiKey)
    {
        return SetApiKeyAsync(ApiServiceIds.Unsplash, "Unsplash", apiKey);
    }

    public Task<string?> GetYouTubeApiKeyAsync()
    {
        return GetApiKeyAsync(ApiServiceIds.YouTube);
    }

    public Task SetYouTubeApiKeyAsync(string apiKey)
    {
        return SetApiKeyAsync(ApiServiceIds.YouTube, "YouTube", apiKey);
    }

    // ==================== IDisposable ====================

    public void Dispose()
    {
        if (_disposed) return;
        
        try
        {
            _database?.Dispose();
            System.Diagnostics.Debug.WriteLine("[ApiKeysService] Disposed");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ApiKeysService] Error during dispose: {ex.Message}");
        }
        
        _disposed = true;
    }
}
