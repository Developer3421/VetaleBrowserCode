using System;
using System.Collections.Concurrent;
using System.IO;
using System.Runtime.InteropServices;
using Avalonia.Media.Imaging;

namespace VetaleBrowser.VetaleBrowser.UI.Services
{
    /// <summary>
    /// Отримує системну іконку для файлу (Windows) і кешує в пам'яті.
    /// На Linux/macOS повертає null (UI має показати дефолтну іконку).
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
                if (!OperatingSystem.IsWindows())
                {
                    _cache[ext] = null;
                    return null;
                }

                var shgfi = new SHFILEINFO();
                uint flags = SHGFI_ICON | SHGFI_USEFILEATTRIBUTES | (large ? SHGFI_LARGEICON : SHGFI_SMALLICON);
                IntPtr hImg = SHGetFileInfo(path, FILE_ATTRIBUTE_NORMAL, ref shgfi, (uint)Marshal.SizeOf(shgfi), flags);

                if (hImg != IntPtr.Zero && shgfi.hIcon != IntPtr.Zero)
                {
                    try
                    {
                        var avaloniaBmp = CreateBitmapFromHIcon(shgfi.hIcon);
                        _cache[ext] = avaloniaBmp;
                        return avaloniaBmp;
                    }
                    finally
                    {
                        DestroyIcon(shgfi.hIcon);
                    }
                }
            }
            catch
            {
                // ignored
            }

            _cache[ext] = null;
            return null;
        }

        private static Bitmap? CreateBitmapFromHIcon(IntPtr hIcon)
        {
            // Convert HICON -> HBITMAP -> RGBA -> Avalonia Bitmap
            if (hIcon == IntPtr.Zero) return null;

            if (!GetIconInfo(hIcon, out var iconInfo))
                return null;

            try
            {
                // Prefer the color bitmap if present; fallback to mask.
                var hBmp = iconInfo.hbmColor != IntPtr.Zero ? iconInfo.hbmColor : iconInfo.hbmMask;
                if (hBmp == IntPtr.Zero)
                    return null;

                if (GetObject(hBmp, Marshal.SizeOf<BITMAP>(), out var bmp) == 0)
                    return null;

                var width = bmp.bmWidth;
                var height = Math.Abs(bmp.bmHeight);
                if (width <= 0 || height <= 0)
                    return null;

                // Prepare a 32-bit BGRA DIB.
                var bi = new BITMAPINFOHEADER
                {
                    biSize = (uint)Marshal.SizeOf<BITMAPINFOHEADER>(),
                    biWidth = width,
                    biHeight = -height, // top-down
                    biPlanes = 1,
                    biBitCount = 32,
                    biCompression = BI_RGB,
                    biSizeImage = (uint)(width * height * 4)
                };

                var bmi = new BITMAPINFO { bmiHeader = bi };

                var hdc = GetDC(IntPtr.Zero);
                if (hdc == IntPtr.Zero)
                    return null;

                try
                {
                    var bufferSize = (int)bi.biSizeImage;
                    var buffer = new byte[bufferSize];

                    var lines = GetDIBits(hdc, hBmp, 0, (uint)height, buffer, ref bmi, DIB_RGB_COLORS);
                    if (lines == 0)
                        return null;

                    // Convert BGRA -> RGBA for Avalonia.
                    for (int i = 0; i < buffer.Length; i += 4)
                    {
                        byte b = buffer[i + 0];
                        byte g = buffer[i + 1];
                        byte r = buffer[i + 2];
                        byte a = buffer[i + 3];
                        buffer[i + 0] = r;
                        buffer[i + 1] = g;
                        buffer[i + 2] = b;
                        buffer[i + 3] = a;
                    }

                    using var ms = new MemoryStream();
                    // Encode as PNG in-memory via Avalonia (avoid System.Drawing)
                    // Bitmap expects an encoded stream; easiest is to create from PixelSize via WriteableBitmap,
                    // but keep changes minimal: use WriteableBitmap and then return as Bitmap.

                    var wb = new WriteableBitmap(
                        new Avalonia.PixelSize(width, height),
                        new Avalonia.Vector(96, 96),
                        Avalonia.Platform.PixelFormat.Rgba8888,
                        Avalonia.Platform.AlphaFormat.Premul);

                    using (var fb = wb.Lock())
                    {
                        // Copy row by row respecting stride
                        int srcStride = width * 4;
                        int dstStride = fb.RowBytes;
                        unsafe
                        {
                            fixed (byte* srcPtr = buffer)
                            {
                                byte* dstPtr = (byte*)fb.Address;
                                for (int y = 0; y < height; y++)
                                {
                                    Buffer.MemoryCopy(srcPtr + y * srcStride, dstPtr + y * dstStride, dstStride, srcStride);
                                }
                            }
                        }
                    }

                    return wb;
                }
                finally
                {
                    ReleaseDC(IntPtr.Zero, hdc);
                }
            }
            finally
            {
                if (iconInfo.hbmColor != IntPtr.Zero) DeleteObject(iconInfo.hbmColor);
                if (iconInfo.hbmMask != IntPtr.Zero) DeleteObject(iconInfo.hbmMask);
            }
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

        [StructLayout(LayoutKind.Sequential)]
        private struct ICONINFO
        {
            public bool fIcon;
            public int xHotspot;
            public int yHotspot;
            public IntPtr hbmMask;
            public IntPtr hbmColor;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct BITMAP
        {
            public int bmType;
            public int bmWidth;
            public int bmHeight;
            public int bmWidthBytes;
            public ushort bmPlanes;
            public ushort bmBitsPixel;
            public IntPtr bmBits;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct BITMAPINFO
        {
            public BITMAPINFOHEADER bmiHeader;
            public uint bmiColors; // not used for BI_RGB 32bpp
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct BITMAPINFOHEADER
        {
            public uint biSize;
            public int biWidth;
            public int biHeight;
            public ushort biPlanes;
            public ushort biBitCount;
            public uint biCompression;
            public uint biSizeImage;
            public int biXPelsPerMeter;
            public int biYPelsPerMeter;
            public uint biClrUsed;
            public uint biClrImportant;
        }

        private const uint BI_RGB = 0;
        private const uint DIB_RGB_COLORS = 0;

        [DllImport("shell32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbFileInfo, uint uFlags);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr hIcon);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool GetIconInfo(IntPtr hIcon, out ICONINFO piconinfo);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern int GetObject(IntPtr hgdiobj, int cbBuffer, out BITMAP lpvObject);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern int GetDIBits(IntPtr hdc, IntPtr hbmp, uint uStartScan, uint cScanLines, [Out] byte[] lpvBits, ref BITMAPINFO lpbi, uint uUsage);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern bool DeleteObject(IntPtr hObject);

        private const uint SHGFI_ICON = 0x100;
        private const uint SHGFI_USEFILEATTRIBUTES = 0x10;
        private const uint SHGFI_SMALLICON = 0x1;
        private const uint SHGFI_LARGEICON = 0x0;
        private const uint FILE_ATTRIBUTE_NORMAL = 0x80;
    }
}
