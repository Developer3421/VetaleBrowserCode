using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;

namespace VetaleBrowser.VetaleBrowser.UI.Helpers;

/// <summary>
/// Optimized image loader with caching and size limits
/// to save RAM and prevent memory leaks
/// </summary>
public static class OptimizedImageLoader
{
    private static readonly HttpClient _httpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(15)
    };

    // Image cache (limited size)
    private static readonly ConcurrentDictionary<string, WeakReference<Bitmap>> _imageCache = new();
    
    // Maximum cache size
    private const int MaxCacheSize = 100;
    
    // Maximum image width/height for previews
    public const int MaxPreviewWidth = 300;
    public const int MaxPreviewHeight = 250;
    
    // Semaphore to limit concurrent downloads
    private static readonly SemaphoreSlim _loadSemaphore = new(4, 4);

    /// <summary>
    /// Loads an image from a URL with size optimization
    /// </summary>
    public static async Task<Bitmap?> LoadImageAsync(string? url, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(url))
            return null;

        try
        {
            // Check cache
            if (_imageCache.TryGetValue(url, out var weakRef) && weakRef.TryGetTarget(out var cached))
            {
                return cached;
            }

            await _loadSemaphore.WaitAsync(ct);
            try
            {
                // Double-check after acquiring the semaphore
                if (_imageCache.TryGetValue(url, out weakRef) && weakRef.TryGetTarget(out cached))
                {
                    return cached;
                }

                // Download the image
                var bytes = await _httpClient.GetByteArrayAsync(url, ct);
                
                if (bytes == null || bytes.Length == 0)
                    return null;

                // Decode with size limit
                using var stream = new MemoryStream(bytes);
                var bitmap = await Task.Run(() => DecodeWithSizeLimit(stream), ct);

                if (bitmap != null)
                {
                    // Clear cache if it is full
                    CleanupCacheIfNeeded();
                    
                    // Add to cache with WeakReference
                    _imageCache[url] = new WeakReference<Bitmap>(bitmap);
                }

                return bitmap;
            }
            finally
            {
                _loadSemaphore.Release();
            }
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[OptimizedImageLoader] Error loading image: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Decodes an image with a size limit to save memory
    /// </summary>
    private static Bitmap? DecodeWithSizeLimit(Stream stream)
    {
        try
        {
            // First read the original image
            stream.Position = 0;
            var original = new Bitmap(stream);

            // If the image is already small - return as is
            if (original.PixelSize.Width <= MaxPreviewWidth && original.PixelSize.Height <= MaxPreviewHeight)
            {
                return original;
            }

            // Calculate new size preserving aspect ratio
            var ratioX = (double)MaxPreviewWidth / original.PixelSize.Width;
            var ratioY = (double)MaxPreviewHeight / original.PixelSize.Height;
            var ratio = Math.Min(ratioX, ratioY);

            var newWidth = (int)(original.PixelSize.Width * ratio);
            var newHeight = (int)(original.PixelSize.Height * ratio);

            // Create a scaled-down version
            var scaled = original.CreateScaledBitmap(new PixelSize(newWidth, newHeight), BitmapInterpolationMode.MediumQuality);
            
            // Release the original
            original.Dispose();

            return scaled;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[OptimizedImageLoader] Error decoding image: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Clears the cache if it is full
    /// </summary>
    private static void CleanupCacheIfNeeded()
    {
        if (_imageCache.Count <= MaxCacheSize)
            return;

        // Remove entries with dead references
        var keysToRemove = new System.Collections.Generic.List<string>();
        
        foreach (var kvp in _imageCache)
        {
            if (!kvp.Value.TryGetTarget(out _))
            {
                keysToRemove.Add(kvp.Key);
            }
        }

        foreach (var key in keysToRemove)
        {
            _imageCache.TryRemove(key, out _);
        }

        // If still too many - remove half
        if (_imageCache.Count > MaxCacheSize)
        {
            var count = 0;
            foreach (var key in _imageCache.Keys)
            {
                if (count++ > MaxCacheSize / 2)
                {
                    _imageCache.TryRemove(key, out _);
                }
            }
        }
    }

    /// <summary>
    /// Clears the entire cache (call when closing the search tab)
    /// </summary>
    public static void ClearCache()
    {
        foreach (var kvp in _imageCache)
        {
            if (kvp.Value.TryGetTarget(out var bitmap))
            {
                bitmap.Dispose();
            }
        }
        _imageCache.Clear();
        
        System.Diagnostics.Debug.WriteLine("[OptimizedImageLoader] Cache cleared");
    }
}

