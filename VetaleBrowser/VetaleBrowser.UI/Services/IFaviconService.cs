// filepath: e:\VetaleBrowser\VetaleBrowser\VetaleBrowser.UI\Services\IFaviconService.cs
using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media;

namespace VetaleBrowser.VetaleBrowser.UI.Services
{
    /// <summary>
    /// Fetches and caches site favicons for use in tabs.
    /// </summary>
    public interface IFaviconService
    {
        /// <summary>
        /// Get favicon image for the given page Uri. Returns null if not available.
        /// </summary>
        Task<IImage?> GetFaviconAsync(Uri pageUri, int size = 16, CancellationToken ct = default);
    }
}

