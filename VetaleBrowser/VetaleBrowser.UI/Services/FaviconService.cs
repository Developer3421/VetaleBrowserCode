// filepath: e:\VetaleBrowser\VetaleBrowser\VetaleBrowser.UI\Services\FaviconService.cs
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace VetaleBrowser.VetaleBrowser.UI.Services
{
    /// <summary>
    /// Downloads favicons via favicon.im (with optional fallbacks) and caches them in memory and on disk.
    /// </summary>
    public sealed class FaviconService : IFaviconService, IDisposable
    {
        private readonly HttpClient _http;
        private readonly ConcurrentDictionary<string, byte[]> _memCache = new();
        private readonly string _diskCacheDir;
        private readonly string _endpointTemplate;

        public FaviconService(HttpClient? httpClient = null, string? endpointTemplate = null)
        {
            _http = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            // Set a friendly UA to reduce chances of being blocked by some providers
            if (!_http.DefaultRequestHeaders.Contains("User-Agent"))
                _http.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "VetaleBrowser/1.0 (+https://example.local) Avalonia");

            _endpointTemplate = endpointTemplate ?? "https://favicon.im/{0}?size={1}";
            _diskCacheDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "VetaleBrowser", "favicon-cache");
            Directory.CreateDirectory(_diskCacheDir);
        }

        public async Task<IImage?> GetFaviconAsync(Uri pageUri, int size = 16, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(pageUri.Host))
                return null;

            if (!string.Equals(pageUri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(pageUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                Debug.WriteLine($"[FaviconService] Skip non-http(s) scheme: {pageUri}");
                return null;
            }

            var host = pageUri.Host.ToLowerInvariant();
            var key = $"{host}-{size}.png";
            var diskPath = Path.Combine(_diskCacheDir, key);

            if (_memCache.TryGetValue(key, out var bytesFromMem))
            {
                Debug.WriteLine($"[FaviconService] HIT MemoryCache host={host} size={size}");
                return BytesToBitmapSafe(bytesFromMem);
            }

            if (File.Exists(diskPath))
            {
                try
                {
                    var bytesDisk = await File.ReadAllBytesAsync(diskPath, ct).ConfigureAwait(false);
                    _memCache[key] = bytesDisk;
                    Debug.WriteLine($"[FaviconService] HIT DiskCache host={host} size={size} path={diskPath}");
                    return BytesToBitmapSafe(bytesDisk);
                }
                catch (Exception ioEx)
                {
                    Debug.WriteLine($"[FaviconService] DiskCache read failed: {ioEx.Message}");
                }
            }

            byte[]? bytes = null;

            // Try favicon.im, then Google S2, then DuckDuckGo, then direct /favicon.ico
            var imUrl = string.Format(_endpointTemplate, host, size);
            Debug.WriteLine($"[FaviconService] TRY favicon.im -> {imUrl}");
            bytes = await TryDownloadAsync(imUrl, ct).ConfigureAwait(false);
            if (bytes != null)
            {
                Debug.WriteLine($"[FaviconService] OK favicon.im host={host} size={size} bytes={bytes.Length}");
            }
            else
            {
                var s2Url = $"https://www.google.com/s2/favicons?sz={size}&domain={host}";
                Debug.WriteLine($"[FaviconService] TRY Google S2 -> {s2Url}");
                bytes = await TryDownloadAsync(s2Url, ct).ConfigureAwait(false);
                if (bytes != null)
                {
                    Debug.WriteLine($"[FaviconService] OK Google S2 host={host} size={size} bytes={bytes.Length}");
                }
                else
                {
                    var ddgUrl = $"https://icons.duckduckgo.com/ip3/{host}.ico";
                    Debug.WriteLine($"[FaviconService] TRY DuckDuckGo -> {ddgUrl}");
                    bytes = await TryDownloadAsync(ddgUrl, ct).ConfigureAwait(false);
                    if (bytes != null)
                    {
                        Debug.WriteLine($"[FaviconService] OK DuckDuckGo host={host} size={size} bytes={bytes.Length}");
                    }
                    else
                    {
                        // Final fallback: try direct /favicon.ico on the site
                        var scheme = string.Equals(pageUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ? "https" : "http";
                        var directUrl = $"{scheme}://{host}/favicon.ico";
                        Debug.WriteLine($"[FaviconService] TRY direct favicon -> {directUrl}");
                        bytes = await TryDownloadAsync(directUrl, ct).ConfigureAwait(false);
                        if (bytes != null)
                        {
                            Debug.WriteLine($"[FaviconService] OK direct favicon host={host} bytes={bytes.Length}");
                        }
                    }
                }
            }

            if (bytes == null || bytes.Length == 0)
            {
                Debug.WriteLine($"[FaviconService] FAIL all providers host={host} size={size}");
                return null;
            }

            try
            {
                await File.WriteAllBytesAsync(diskPath, bytes, ct).ConfigureAwait(false);
                Debug.WriteLine($"[FaviconService] Cache write OK path={diskPath} bytes={bytes.Length}");
            }
            catch (Exception ioEx)
            {
                Debug.WriteLine($"[FaviconService] Cache write FAILED path={diskPath} err={ioEx.Message}");
            }

            _memCache[key] = bytes;
            return BytesToBitmapSafe(bytes);
        }

        private static IImage? BytesToBitmapSafe(byte[] bytes)
        {
            try
            {
                if (bytes.Length == 0) return null;
                var ms = new MemoryStream(bytes, writable: false);
                // Avalonia Bitmap supports PNG, ICO, JPEG, etc. If unsupported/corrupt, it will throw.
                return new Bitmap(ms);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[FaviconService] Image decode failed: {ex.Message}");
                return null;
            }
        }

        private async Task<byte[]?> TryDownloadAsync(string url, CancellationToken ct)
        {
            try
            {
                using var resp = await _http.GetAsync(url, ct).ConfigureAwait(false);
                if (!resp.IsSuccessStatusCode)
                {
                    Debug.WriteLine($"[FaviconService] HTTP {resp.StatusCode} for {url}");
                    return null;
                }
                var bytes = await resp.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
                return bytes.Length == 0 ? null : bytes;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[FaviconService] HTTP error for {url}: {ex.Message}");
                return null;
            }
        }

        public void Dispose()
        {
            _http.Dispose();
        }
    }
}
