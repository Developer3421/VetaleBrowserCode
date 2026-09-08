// Windows-only: одна вкладка = один ChromiumWebBrowser (свій render-процес CEF).
#if WINDOWS
using System;
using CefSharp.Wpf;

namespace VetaleBrowser.VetaleBrowser.Core.Scripts.Browser;

public sealed class BrowserTab : IDisposable
{
    public string Title { get; set; } = "New Tab";
    public ChromiumWebBrowser Browser { get; }
    public bool IsMuted { get; private set; }
    private bool _disposed;

    public BrowserTab(string url = "https://example.com")
    {
        CefWpfBootstrapper.EnsureInitialized();
        Browser = new ChromiumWebBrowser(url);
    }

    public void Navigate(string url)
    {
        if (!string.IsNullOrWhiteSpace(url))
            Browser.Address = url;
    }

    public void SetMuted(bool muted)
    {
        var host = Browser.GetBrowser()?.GetHost();
        if (host == null) return;
        host.SetAudioMuted(muted);
        IsMuted = muted;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try { Browser.Dispose(); } catch { }
    }
}
#endif
