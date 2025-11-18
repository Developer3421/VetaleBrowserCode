using System;
using System.Collections.Concurrent;
using System.IO;
using System.Runtime.InteropServices;
using System.Reflection;
using Avalonia.Media.Imaging;

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
                if (OperatingSystem.IsWindows())
                {
                    var shgfi = new SHFILEINFO();
                    uint flags = SHGFI_ICON | SHGFI_USEFILEATTRIBUTES | (large ? SHGFI_LARGEICON : SHGFI_SMALLICON);
                    IntPtr hImg = SHGetFileInfo(path, FILE_ATTRIBUTE_NORMAL, ref shgfi, (uint)Marshal.SizeOf(shgfi), flags);
                    if (hImg != IntPtr.Zero && shgfi.hIcon != IntPtr.Zero)
                    {
                        using var ms = new MemoryStream();

                        // Рефлексивно викликаємо System.Drawing.Icon.FromHandle(hIcon) та зберігаємо у PNG через System.Drawing.Bitmap.Save
                        var iconType = Type.GetType("System.Drawing.Icon, System.Drawing.Common", throwOnError: false);
                        var imageFormatType = Type.GetType("System.Drawing.Imaging.ImageFormat, System.Drawing.Common", throwOnError: false);
                        if (iconType != null && imageFormatType != null)
                        {
                            var fromHandle = iconType.GetMethod("FromHandle", BindingFlags.Public | BindingFlags.Static, binder: null, types: new Type[] { typeof(IntPtr) }, modifiers: null);
                            var iconObj = fromHandle?.Invoke(null, new object[] { shgfi.hIcon });
                            if (iconObj != null)
                            {
                                var toBitmap = iconType.GetMethod("ToBitmap", BindingFlags.Public | BindingFlags.Instance, binder: null, types: Type.EmptyTypes, modifiers: null);
                                var bmpObj = toBitmap?.Invoke(iconObj, null);
                                if (bmpObj != null)
                                {
                                    var pngProp = imageFormatType.GetProperty("Png", BindingFlags.Public | BindingFlags.Static);
                                    var png = pngProp?.GetValue(null);
                                    var save = bmpObj.GetType().GetMethod("Save", BindingFlags.Public | BindingFlags.Instance, binder: null, types: new Type[] { typeof(Stream), imageFormatType }, modifiers: null);
                                    save?.Invoke(bmpObj, new object[] { ms, png! });
                                    ms.Position = 0;
                                    var avaloniaBmp = new Bitmap(ms);
                                    _cache[ext] = avaloniaBmp;
                                    (bmpObj as IDisposable)?.Dispose();
                                    (iconObj as IDisposable)?.Dispose();
                                    DestroyIcon(shgfi.hIcon);
                                    return avaloniaBmp;
                                }
                                (iconObj as IDisposable)?.Dispose();
                            }
                        }

                        DestroyIcon(shgfi.hIcon);
                    }
                }
            }
            catch { }
            _cache[ext] = null;
            return null;
        }

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
    }
}
