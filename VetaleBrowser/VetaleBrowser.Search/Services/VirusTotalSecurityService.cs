using System;
using System.Collections.Concurrent;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using VetaleBrowser.VetaleBrowser.Search.Models;

namespace VetaleBrowser.VetaleBrowser.Search.Services;

/// <summary>
/// Клієнт VirusTotal Public API v3 для перевірки URL
/// </summary>
public class VirusTotalSecurityService
{
    private const string BaseUrl = "https://www.virustotal.com/api/v3";

    // TODO: винести в settings, зараз – константа-заглушка
    private const string ApiKey = "a0c5bf9c0c5547525f5fb9799ae52006ff3d2951f10a0730d5acbe8fd6f9e42c"; // Якщо порожній – VT не використовується

    private readonly HttpClient _httpClient;

    // Простий кеш результатів VT
    private readonly ConcurrentDictionary<string, VirusTotalCacheEntry> _cache = new();
    private readonly TimeSpan _cacheTtl = TimeSpan.FromHours(6);

    private DateTimeOffset _rateLimitedUntil = DateTimeOffset.MinValue;

    public VirusTotalSecurityService(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(10)
        };
    }

    public async Task<VirusTotalDetails?> CheckUrlAsync(string url)
    {
        // Якщо ключа немає – просто не використовуємо VT
        if (string.IsNullOrWhiteSpace(ApiKey))
            return null;

        if (DateTimeOffset.UtcNow < _rateLimitedUntil)
            return null; // тимчасово не звертаємося до VT

        var normalized = NormalizeForCache(url);

        if (_cache.TryGetValue(normalized, out var cached))
        {
            if (DateTimeOffset.UtcNow - cached.StoredAt < _cacheTtl)
                return cached.Details;

            _cache.TryRemove(normalized, out _);
        }

        try
        {
            var details = await QueryVirusTotalAsync(url);

            _cache[normalized] = new VirusTotalCacheEntry
            {
                Details = details,
                StoredAt = DateTimeOffset.UtcNow
            };

            return details;
        }
        catch (RateLimitedException ex)
        {
            System.Diagnostics.Debug.WriteLine($"[VirusTotal] Rate limited: {ex.Message}");
            _rateLimitedUntil = DateTimeOffset.UtcNow.AddMinutes(5);
            return null;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[VirusTotal] Error: {ex.Message}");
            return null;
        }
    }

    private async Task<VirusTotalDetails?> QueryVirusTotalAsync(string url)
    {
        // VT v3 вимагає кодувати URL у base64 без '=' в кінці
        var urlBytes = Encoding.UTF8.GetBytes(url);
        var urlId = Convert.ToBase64String(urlBytes).TrimEnd('=');

        using var request = new HttpRequestMessage(HttpMethod.Get, $"{BaseUrl}/urls/{urlId}");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Add("x-apikey", ApiKey);

        using var response = await _httpClient.SendAsync(request);

        if ((int)response.StatusCode == 429)
            throw new RateLimitedException("VirusTotal rate limit exceeded");

        if (!response.IsSuccessStatusCode)
            return null;

        var json = await response.Content.ReadAsStringAsync();
        var vtResponse = JsonSerializer.Deserialize<VtUrlResponse>(json);
        var attrs = vtResponse?.Data?.Attributes;
        if (attrs == null)
            return null;

        var stats = attrs.LastAnalysisStats;

        return new VirusTotalDetails
        {
            Id = vtResponse.Data.Id,
            Harmless = stats?.Harmless ?? 0,
            Malicious = stats?.Malicious ?? 0,
            Suspicious = stats?.Suspicious ?? 0,
            Undetected = stats?.Undetected ?? 0,
            Timeout = stats?.Timeout ?? 0,
            LastAnalysisDate = attrs.LastAnalysisDateUnix.HasValue
                ? DateTimeOffset.FromUnixTimeSeconds(attrs.LastAnalysisDateUnix.Value).DateTime
                : null,
            RawLabel = BuildLabel(stats)
        };
    }

    private static string NormalizeForCache(string url)
    {
        try
        {
            if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                url = "https://" + url;
            }

            var uri = new Uri(url);
            // Для кешу – хост + шлях без query, щоб уникнути вибуху комбінацій
            return uri.GetLeftPart(UriPartial.Path);
        }
        catch
        {
            return url;
        }
    }

    private static string? BuildLabel(LastAnalysisStats? stats)
    {
        if (stats == null) return null;

        if (stats.Malicious > 0 || stats.Suspicious > 0)
            return $"Malicious: {stats.Malicious}, Suspicious: {stats.Suspicious}";

        if (stats.Harmless > 0 && stats.Malicious == 0 && stats.Suspicious == 0)
            return "Harmless";

        return null;
    }

    private class VirusTotalCacheEntry
    {
        public VirusTotalDetails? Details { get; set; }
        public DateTimeOffset StoredAt { get; set; }
    }

    private class RateLimitedException : Exception
    {
        public RateLimitedException(string message) : base(message) { }
    }

    #region DTO
    private class VtUrlResponse
    {
        [JsonPropertyName("data")]
        public VtData? Data { get; set; }
    }

    private class VtData
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("attributes")]
        public VtAttributes? Attributes { get; set; }
    }

    private class VtAttributes
    {
        [JsonPropertyName("last_analysis_stats")]
        public LastAnalysisStats? LastAnalysisStats { get; set; }

        [JsonPropertyName("last_analysis_date")]
        public long? LastAnalysisDateUnix { get; set; }
    }

    private class LastAnalysisStats
    {
        [JsonPropertyName("harmless")]
        public int Harmless { get; set; }

        [JsonPropertyName("malicious")]
        public int Malicious { get; set; }

        [JsonPropertyName("suspicious")]
        public int Suspicious { get; set; }

        [JsonPropertyName("undetected")]
        public int Undetected { get; set; }

        [JsonPropertyName("timeout")]
        public int Timeout { get; set; }
    }
    #endregion
}

