using System;
using System.Collections.Concurrent;
using System.IO;
using System.Runtime.InteropServices;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace VetaleBrowser.VetaleBrowser.UI.Services
{
    /// <summary>
    /// Отримує системну іконку для файлу (Windows) і кешує в пам'яті.
    /// </summary>
    public static class FileIconService
    {
        private static readonly ConcurrentDictionary<string, Bitmap?> _cache = new(StringComparer.OrdinalIgnoreCase);

        public static Bitmap? GetFileIcon(string path, bool large = false)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;
            var ext = Path.GetExtension(path);
            if (string.IsNullOrEmpty(ext)) ext = path; // fallback
            if (_cache.TryGetValue(ext, out var bmp)) return bmp;
            try
            {
#if WINDOWS
                var shgfi = new SHFILEINFO();
                uint flags = SHGFI_ICON | SHGFI_USEFILEATTRIBUTES | (large ? SHGFI_LARGEICON : SHGFI_SMALLICON);
                IntPtr hImg = SHGetFileInfo(path, FILE_ATTRIBUTE_NORMAL, ref shgfi, (uint)Marshal.SizeOf(shgfi), flags);
                if (hImg != IntPtr.Zero && shgfi.hIcon != IntPtr.Zero)
                {
                    using var icon = System.Drawing.Icon.FromHandle(shgfi.hIcon);
                    using var ms = new MemoryStream();
                    icon.ToBitmap().Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                    ms.Position = 0;
                    var avaloniaBmp = new Bitmap(ms);
                    _cache[ext] = avaloniaBmp;
                    DestroyIcon(shgfi.hIcon);
                    return avaloniaBmp;
                }
#endif
            }
            catch { }
            _cache[ext] = null;
            return null;
        }

#if WINDOWS
        [StructLayout(LayoutKind.Sequential)]
        private struct SHFILEINFO
        {
            public IntPtr hIcon;
            public int iIcon;
            public uint dwAttributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string szDisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)] public string szTypeName;
        }
        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbFileInfo, uint uFlags);
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr hIcon);
        private const uint SHGFI_ICON = 0x100;
        private const uint SHGFI_USEFILEATTRIBUTES = 0x10;
        private const uint SHGFI_SMALLICON = 0x1;
        private const uint SHGFI_LARGEICON = 0x0;
        private const uint FILE_ATTRIBUTE_NORMAL = 0x80;
#endif
    }
}

