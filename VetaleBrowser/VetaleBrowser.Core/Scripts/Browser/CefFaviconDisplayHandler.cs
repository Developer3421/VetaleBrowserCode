using System;
using System.Collections.Generic;
using CefSharp;
using CefSharp.Handler;

namespace VetaleBrowser.VetaleBrowser.Core.Scripts.Browser;

/// <summary>
/// Іконки напряму від рушія CEF: OnFaviconUrlChange приходить із самого Chromium
/// (без DevTools-опитування і без вгадування /favicon.ico).
/// </summary>
public sealed class CefFaviconDisplayHandler : DisplayHandler
{
    public event EventHandler<IList<string>>? FaviconUrlsChanged;

    protected override void OnFaviconUrlChange(IWebBrowser chromiumWebBrowser, IBrowser browser, IList<string> urls)
    {
        try { FaviconUrlsChanged?.Invoke(this, urls); } catch { }
        try { base.OnFaviconUrlChange(chromiumWebBrowser, browser, urls); } catch { }
    }
}
