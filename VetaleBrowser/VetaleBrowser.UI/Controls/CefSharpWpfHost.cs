// Windows-only WPF-interop host: Avalonia NativeControlHost + CefSharp.Wpf ChromiumWebBrowser.
// Requires: <UseWPF>true</UseWPF> + TargetFramework net10.0-windows + CefSharp.Common.NETCore + CefSharp.Wpf.NETCore.
#if WINDOWS
using System;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.Threading;
using System.Windows.Interop;
using WpfControls = System.Windows.Controls;
using CefSharp.Wpf;

namespace VetaleBrowser.VetaleBrowser.UI.Controls;

/// <summary>
/// Одна вкладка = один ChromiumWebBrowser (CEF сам створює окремий render-процес).
/// Фікс чорного екрана:
///  1) браузер лежить у WPF Grid з розтягуванням + явна синхронізація Width/Height з Bounds хоста
///     (без розміру RootVisual має 0x0 і CEF малює чорне);
///  2) дочірній HWND позиціонується/ресайзиться під хост (SetWindowPos);
///  3) WPF Dispatcher прокачується таймером Avalonia (DoEvents), бо Avalonia його сама не крутить.
/// </summary>
public class CefSharpWpfHost : NativeControlHost
{
    private HwndSource? _hwndSource;
    private WpfControls.Grid? _root;
    private ChromiumWebBrowser? _browser;
    private int _lastW;
    private int _lastH;

    public string StartUrl { get; set; } = "https://example.com";
    public ChromiumWebBrowser? Browser => _browser;
    public bool IsMuted { get; private set; }

    // Події нового контрола для таб-системи / іконок / навігації.
    public event EventHandler? BrowserCreated;
    public event EventHandler<string?>? BrowserAddressChanged;
    public event EventHandler<string?>? BrowserTitleChanged;
    public event EventHandler<(bool CanGoBack, bool CanGoForward)>? BrowserLoadingStateChanged;
    public event EventHandler<string?>? BrowserFrameLoadEnd;
    /// <summary>URL іконок напряму від CEF (OnFaviconUrlChange).</summary>
    public event EventHandler<System.Collections.Generic.IList<string>>? BrowserFaviconUrlsChanged;

    public CefSharpWpfHost()
    {
        // Stretch щоб Grid-контейнер у вікні мав ненульовий розмір.
        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch;
        VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch;
        PropertyChanged += (_, e) =>
        {
            if (e.Property == BoundsProperty)
                SyncChildSize();
        };
    }

    protected override IPlatformHandle CreateNativeControlCore(IPlatformHandle parent)
    {
        if (_browser == null)
        {
            _browser = new ChromiumWebBrowser(StartUrl)
            {
                HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch,
                VerticalAlignment = System.Windows.VerticalAlignment.Stretch,
            };
            try
            {
                // Хендлер іконок від самого CEF.
                var faviconHandler = new global::VetaleBrowser.VetaleBrowser.Core.Scripts.Browser.CefFaviconDisplayHandler();
                faviconHandler.FaviconUrlsChanged += (_, urls) =>
                {
                    try { BrowserFaviconUrlsChanged?.Invoke(this, urls); } catch { }
                };
                _browser.DisplayHandler = faviconHandler;
            }
            catch { }
            WireBrowserEvents(_browser);
        }
        if (_root == null)
        {
            _root = new WpfControls.Grid();
            _root.Children.Add(_browser);
        }

        // Початковий розмір з Bounds хоста (інакше 0x0 -> чорний екран).
        var w = Math.Max(100, (int)Bounds.Width);
        var h = Math.Max(100, (int)Bounds.Height);
        SyncWpfSize(w, h);

        var parameters = new HwndSourceParameters("CefSharpHost")
        {
            ParentWindow = parent.Handle,
            Width = w,
            Height = h,
            WindowStyle = unchecked((int)(0x40000000 | 0x10000000 | 0x02000000 | 0x04000000)), // WS_CHILD|WS_VISIBLE|WS_CLIPCHILDREN|WS_CLIPSIBLINGS
        };

        _hwndSource = new HwndSource(parameters) { RootVisual = _root };
        _lastW = 0;
        _lastH = 0;
        SyncChildSize();

        return new PlatformHandle(_hwndSource.Handle, "HWND");
    }

    protected override void DestroyNativeControlCore(IPlatformHandle control)
    {
        // ВАЖЛИВО для вкладок: браузер НЕ вбиваємо — тільки від'єднуємо від старого HwndSource.
        // Інакше кожне перемикання вкладок (detach зі старого контейнера) знищувало б сторінку.
        // CEF-браузер переживає пересадку в новий HwndSource при наступному CreateNativeControlCore.
        try
        {
            if (_root != null && _browser != null)
                _root.Children.Remove(_browser);
        }
        catch { }
        try { _hwndSource?.Dispose(); } catch { }
        _root = null;
        _hwndSource = null;
        base.DestroyNativeControlCore(control);
    }

    /// <summary>Остаточне знищення вкладки (закриття): вбиває і браузер.</summary>
    public void DestroyBrowser()
    {
        try { _browser?.Dispose(); } catch { }
        _browser = null;
        try { _hwndSource?.Dispose(); } catch { }
        _root = null;
        _hwndSource = null;
    }

    public void Navigate(string url)
    {
        if (_browser != null && !string.IsNullOrWhiteSpace(url))
        {
            var current = _browser.Address;
            if (!string.Equals(current, url, StringComparison.OrdinalIgnoreCase))
                _browser.Address = url;
        }
        else StartUrl = url;
    }

    public void SetMuted(bool muted)
    {
        IsMuted = muted;
        try { _browser?.GetBrowser()?.GetHost()?.SetAudioMuted(muted); } catch { }
    }

    private void WireBrowserEvents(ChromiumWebBrowser browser)
    {
        try
        {
            browser.AddressChanged += (_, e) =>
            {
                try { BrowserAddressChanged?.Invoke(this, e.NewValue as string); } catch { }
            };
            browser.TitleChanged += (_, e) =>
            {
                try { BrowserTitleChanged?.Invoke(this, e.NewValue as string); } catch { }
            };
            browser.LoadingStateChanged += (_, e) =>
            {
                try { BrowserLoadingStateChanged?.Invoke(this, (e.CanGoBack, e.CanGoForward)); } catch { }
            };
            browser.FrameLoadEnd += (_, e) =>
            {
                try
                {
                    if (e.Frame?.IsMain == true)
                        BrowserFrameLoadEnd?.Invoke(this, e.Url);
                }
                catch { }
            };
            BrowserCreated?.Invoke(this, EventArgs.Empty);
        }
        catch { }
    }

    private void SyncWpfSize(int w, int h)
    {
        try
        {
            if (_root != null) { _root.Width = w; _root.Height = h; }
            if (_browser != null) { _browser.Width = w; _browser.Height = h; }
            _root?.UpdateLayout();
        }
        catch { }
    }

    private void SyncChildSize()
    {
        try
        {
            if (_hwndSource == null) return;
            var w = Math.Max(1, (int)Bounds.Width);
            var h = Math.Max(1, (int)Bounds.Height);
            if (w < 10 || h < 10) return;
            // Тільки при реальній зміні розміру: безперервний SetWindowPos кожен тік
            // бився з ОС-перетягуванням вікна (скидання фокуса, підвисання).
            if (w == _lastW && h == _lastH) return;
            _lastW = w;
            _lastH = h;
            SyncWpfSize(w, h);
            // Дочірній HWND на весь хост (без активації/зміни z-порядку):
            SetWindowPos(_hwndSource.Handle, IntPtr.Zero, 0, 0, w, h, 0x0014); // SWP_NOZORDER|SWP_NOACTIVATE
        }
        catch { }
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);
}
#endif
