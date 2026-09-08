// Windows-only Cef.Initialize / Cef.Shutdown для CefSharp.Wpf варіанту.
// Викликати EnsureInitialized() ДО першого ChromiumWebBrowser, Shutdown() при закритті.
#if WINDOWS
using System;
using System.IO;
using CefSharp;
using CefSharp.Wpf;

namespace VetaleBrowser.VetaleBrowser.Core.Scripts.Browser;

public static class CefWpfBootstrapper
{
    private static readonly object _lock = new();
    private static bool _initialized;

    public static void EnsureInitialized()
    {
        lock (_lock)
        {
            if (_initialized || Cef.IsInitialized == true)
            {
                _initialized = true;
                return;
            }

            var settings = CefBrowserConfig.CreateSettings();

            // BrowserSubprocessPath: якщо subprocess не поруч з exe — виставити вручну:
            // settings.BrowserSubprocessPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CefSharp.BrowserSubprocess.exe");

            Cef.Initialize(settings, performDependencyCheck: true, browserProcessHandler: null);
            _initialized = true;
        }
    }

    public static void Shutdown()
    {
        try { Cef.Shutdown(); } catch { }
    }
}
#endif
