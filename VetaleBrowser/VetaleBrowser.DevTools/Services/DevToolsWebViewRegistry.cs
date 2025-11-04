using WebViewControl;

namespace VetaleBrowser.VetaleBrowser.DevTools.Services
{
    /// <summary>
    /// Simple static registry to share a single DevTools-dedicated WebView across pages.
    /// WebViewWorkerPage registers its WebView here; other pages read it when capturing data.
    /// </summary>
    public static class DevToolsWebViewRegistry
    {
        private static WebView? _current;
        public static WebView? CurrentWebView
        {
            get => _current;
            set => _current = value;
        }
    }
}

