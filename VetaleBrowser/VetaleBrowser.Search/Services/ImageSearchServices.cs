using System;
using System.Threading.Tasks;
using VetaleBrowser.VetaleBrowser.Database.Services;

namespace VetaleBrowser.VetaleBrowser.Search.Services
{
    /// <summary>
    /// Default API keys for search
    /// </summary>
    public static class DefaultApiKeys
    {
        public const string YouTubeApiKey = "";
    }

    /// <summary>
    /// Holds the YouTube API key for video search (cached + DB + environment lookup).
    /// Image search (Pexels/Unsplash) has been removed.
    /// </summary>
    public static class ImageSearchServiceFactory
    {
        public const int MaxResultsPerPage = 50;

        private static string? _cachedYouTubeKey;

        public static void SetYouTubeApiKey(string? apiKey)
        {
            System.Diagnostics.Debug.WriteLine($"[ImageSearchServiceFactory] SetYouTubeApiKey: {(string.IsNullOrWhiteSpace(apiKey) ? "null/empty" : "***")}");
            _cachedYouTubeKey = apiKey;
        }

        public static void InvalidateCache()
        {
            _cachedYouTubeKey = null;
            System.Diagnostics.Debug.WriteLine("[ImageSearchServiceFactory] Cache invalidated");
        }

        private static string? LoadApiKeyFromDatabase(string serviceId)
        {
            try
            {
                var apiKeysService = DatabaseServicesFactory.TryGetApiKeysService();
                if (apiKeysService == null)
                    return null;

                var key = Task.Run(async () =>
                {
                    try
                    {
                        return await apiKeysService.GetApiKeyAsync(serviceId).ConfigureAwait(false);
                    }
                    catch
                    {
                        return null;
                    }
                }).GetAwaiter().GetResult();

                if (!string.IsNullOrWhiteSpace(key))
                {
                    System.Diagnostics.Debug.WriteLine($"[ImageSearchServiceFactory] Loaded {serviceId} from DB");
                    return key;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ImageSearchServiceFactory] Error: {ex.Message}");
            }
            return null;
        }

        public static bool HasApiKey(string serviceId)
        {
            if (serviceId == ApiServiceIds.YouTube)
            {
                if (!string.IsNullOrWhiteSpace(DefaultApiKeys.YouTubeApiKey))
                    return true;
                if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("YOUTUBE_API_KEY")))
                    return true;
            }

            var key = LoadApiKeyFromDatabase(serviceId);
            return !string.IsNullOrWhiteSpace(key);
        }

        public static string? GetYouTubeApiKey()
        {
            if (!string.IsNullOrWhiteSpace(_cachedYouTubeKey))
                return _cachedYouTubeKey;

            if (!string.IsNullOrWhiteSpace(DefaultApiKeys.YouTubeApiKey))
                return DefaultApiKeys.YouTubeApiKey;

            var dbKey = LoadApiKeyFromDatabase(ApiServiceIds.YouTube);
            if (!string.IsNullOrWhiteSpace(dbKey))
            {
                _cachedYouTubeKey = dbKey;
                return dbKey;
            }

            return Environment.GetEnvironmentVariable("YOUTUBE_API_KEY");
        }
    }
}
