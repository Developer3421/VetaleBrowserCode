using VetaleBrowser.VetaleBrowser.Core.Scripts.Browser;

namespace VetaleBrowser.VetaleBrowser.DevTools.Services
{
    /// <summary>
    /// Simple static registry to share a single DevTools-dedicated WebView across pages.
    /// WebViewWorkerPage registers its WebView here; other pages read it when capturing data.
    /// </summary>
    public static class DevToolsWebViewRegistry
    {
        private static IBrowserView? _current;
        public static IBrowserView? CurrentWebView
        {
            get => _current;
            set => _current = value;
        }
    }
}

