using System.Collections.Generic;
using System.Threading.Tasks;
using VetaleBrowser.VetaleBrowser.Database.Models;

namespace VetaleBrowser.VetaleBrowser.Database.Services;

/// <summary>
/// Interface for working with API keys
/// </summary>
public interface IApiKeysService
{
    // ==================== General methods ====================
    
    /// <summary>
    /// Gets the API key by service identifier
    /// </summary>
    Task<string?> GetApiKeyAsync(string serviceId);
    
    /// <summary>
    /// Sets the API key for a service
    /// </summary>
    Task SetApiKeyAsync(string serviceId, string serviceName, string apiKey);
    
    /// <summary>
    /// Removes the API key for a service
    /// </summary>
    Task RemoveApiKeyAsync(string serviceId);
    
    /// <summary>
    /// Checks whether a key exists for a service
    /// </summary>
    Task<bool> HasApiKeyAsync(string serviceId);
    
    /// <summary>
    /// Gets all stored API keys
    /// </summary>
    Task<IReadOnlyList<ApiKeyItem>> GetAllApiKeysAsync();
    
    /// <summary>
    /// Updates key usage statistics (successful request)
    /// </summary>
    Task RecordSuccessfulUseAsync(string serviceId);
    
    /// <summary>
    /// Updates key usage statistics (failed request)
    /// </summary>
    Task RecordFailedUseAsync(string serviceId);
    
    // ==================== Specialized methods for services ====================
    
    /// <summary>
    /// Gets the Gemini API key
    /// </summary>
    Task<string?> GetGeminiApiKeyAsync();
    
    /// <summary>
    /// Sets the Gemini API key
    /// </summary>
    Task SetGeminiApiKeyAsync(string apiKey);
    
    /// <summary>
    /// Gets the Pexels API key
    /// </summary>
    Task<string?> GetPexelsApiKeyAsync();
    
    /// <summary>
    /// Sets the Pexels API key
    /// </summary>
    Task SetPexelsApiKeyAsync(string apiKey);
    
    /// <summary>
    /// Gets the Unsplash API key
    /// </summary>
    Task<string?> GetUnsplashApiKeyAsync();
    
    /// <summary>
    /// Sets the Unsplash API key
    /// </summary>
    Task SetUnsplashApiKeyAsync(string apiKey);
    
    /// <summary>
    /// Gets the YouTube API key
    /// </summary>
    Task<string?> GetYouTubeApiKeyAsync();
    
    /// <summary>
    /// Sets the YouTube API key
    /// </summary>
    Task SetYouTubeApiKeyAsync(string apiKey);
}

/// <summary>
/// Service identifier constants
/// </summary>
public static class ApiServiceIds
{
    public const string Gemini = "gemini";
    public const string Pexels = "pexels";
    public const string Unsplash = "unsplash";
    public const string YouTube = "youtube";
}

