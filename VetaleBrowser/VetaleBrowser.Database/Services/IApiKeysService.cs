using System.Collections.Generic;
using System.Threading.Tasks;
using VetaleBrowser.VetaleBrowser.Database.Models;

namespace VetaleBrowser.VetaleBrowser.Database.Services;

/// <summary>
/// Інтерфейс для роботи з API ключами
/// </summary>
public interface IApiKeysService
{
    // ==================== Загальні методи ====================
    
    /// <summary>
    /// Отримує API ключ за ідентифікатором сервісу
    /// </summary>
    Task<string?> GetApiKeyAsync(string serviceId);
    
    /// <summary>
    /// Встановлює API ключ для сервісу
    /// </summary>
    Task SetApiKeyAsync(string serviceId, string serviceName, string apiKey);
    
    /// <summary>
    /// Видаляє API ключ для сервісу
    /// </summary>
    Task RemoveApiKeyAsync(string serviceId);
    
    /// <summary>
    /// Перевіряє чи існує ключ для сервісу
    /// </summary>
    Task<bool> HasApiKeyAsync(string serviceId);
    
    /// <summary>
    /// Отримує всі збережені API ключі
    /// </summary>
    Task<IReadOnlyList<ApiKeyItem>> GetAllApiKeysAsync();
    
    /// <summary>
    /// Оновлює статистику використання ключа (успішний запит)
    /// </summary>
    Task RecordSuccessfulUseAsync(string serviceId);
    
    /// <summary>
    /// Оновлює статистику використання ключа (невдалий запит)
    /// </summary>
    Task RecordFailedUseAsync(string serviceId);
    
    // ==================== Спеціалізовані методи для сервісів ====================
    
    /// <summary>
    /// Отримує API ключ Gemini
    /// </summary>
    Task<string?> GetGeminiApiKeyAsync();
    
    /// <summary>
    /// Встановлює API ключ Gemini
    /// </summary>
    Task SetGeminiApiKeyAsync(string apiKey);
    
    /// <summary>
    /// Отримує API ключ Pexels
    /// </summary>
    Task<string?> GetPexelsApiKeyAsync();
    
    /// <summary>
    /// Встановлює API ключ Pexels
    /// </summary>
    Task SetPexelsApiKeyAsync(string apiKey);
    
    /// <summary>
    /// Отримує API ключ Unsplash
    /// </summary>
    Task<string?> GetUnsplashApiKeyAsync();
    
    /// <summary>
    /// Встановлює API ключ Unsplash
    /// </summary>
    Task SetUnsplashApiKeyAsync(string apiKey);
    
    /// <summary>
    /// Отримує API ключ YouTube
    /// </summary>
    Task<string?> GetYouTubeApiKeyAsync();
    
    /// <summary>
    /// Встановлює API ключ YouTube
    /// </summary>
    Task SetYouTubeApiKeyAsync(string apiKey);
}

/// <summary>
/// Константи ідентифікаторів сервісів
/// </summary>
public static class ApiServiceIds
{
    public const string Gemini = "gemini";
    public const string Pexels = "pexels";
    public const string Unsplash = "unsplash";
    public const string YouTube = "youtube";
}

