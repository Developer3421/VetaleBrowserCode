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
/// Оптимізований завантажувач зображень з кешуванням та обмеженням розміру
/// для економії оперативної пам'яті та запобігання витокам
/// </summary>
public static class OptimizedImageLoader
{
    private static readonly HttpClient _httpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(15)
    };

    // Кеш для зображень (обмежений розмір)
    private static readonly ConcurrentDictionary<string, WeakReference<Bitmap>> _imageCache = new();
    
    // Максимальний розмір кешу
    private const int MaxCacheSize = 100;
    
    // Максимальна ширина/висота зображення для превью
    public const int MaxPreviewWidth = 300;
    public const int MaxPreviewHeight = 250;
    
    // Семафор для обмеження паралельних завантажень
    private static readonly SemaphoreSlim _loadSemaphore = new(4, 4);

    /// <summary>
    /// Завантажує зображення з URL з оптимізацією розміру
    /// </summary>
    public static async Task<Bitmap?> LoadImageAsync(string? url, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(url))
            return null;

        try
        {
            // Перевіряємо кеш
            if (_imageCache.TryGetValue(url, out var weakRef) && weakRef.TryGetTarget(out var cached))
            {
                return cached;
            }

            await _loadSemaphore.WaitAsync(ct);
            try
            {
                // Повторна перевірка після отримання семафору
                if (_imageCache.TryGetValue(url, out weakRef) && weakRef.TryGetTarget(out cached))
                {
                    return cached;
                }

                // Завантажуємо зображення
                var bytes = await _httpClient.GetByteArrayAsync(url, ct);
                
                if (bytes == null || bytes.Length == 0)
                    return null;

                // Декодуємо з обмеженням розміру
                using var stream = new MemoryStream(bytes);
                var bitmap = await Task.Run(() => DecodeWithSizeLimit(stream), ct);

                if (bitmap != null)
                {
                    // Очищуємо кеш якщо він переповнений
                    CleanupCacheIfNeeded();
                    
                    // Додаємо в кеш з WeakReference
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
    /// Декодує зображення з обмеженням розміру для економії пам'яті
    /// </summary>
    private static Bitmap? DecodeWithSizeLimit(Stream stream)
    {
        try
        {
            // Спочатку читаємо оригінальне зображення
            stream.Position = 0;
            var original = new Bitmap(stream);

            // Якщо зображення вже маленьке - повертаємо як є
            if (original.PixelSize.Width <= MaxPreviewWidth && original.PixelSize.Height <= MaxPreviewHeight)
            {
                return original;
            }

            // Обчислюємо новий розмір зі збереженням пропорцій
            var ratioX = (double)MaxPreviewWidth / original.PixelSize.Width;
            var ratioY = (double)MaxPreviewHeight / original.PixelSize.Height;
            var ratio = Math.Min(ratioX, ratioY);

            var newWidth = (int)(original.PixelSize.Width * ratio);
            var newHeight = (int)(original.PixelSize.Height * ratio);

            // Створюємо зменшену версію
            var scaled = original.CreateScaledBitmap(new PixelSize(newWidth, newHeight), BitmapInterpolationMode.MediumQuality);
            
            // Звільняємо оригінал
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
    /// Очищує кеш якщо він переповнений
    /// </summary>
    private static void CleanupCacheIfNeeded()
    {
        if (_imageCache.Count <= MaxCacheSize)
            return;

        // Видаляємо записи з мертвими посиланнями
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

        // Якщо все ще забагато - видаляємо половину
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
    /// Очищує весь кеш (викликати при закритті вкладки пошуку)
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

