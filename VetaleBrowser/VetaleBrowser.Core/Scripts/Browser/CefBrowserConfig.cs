using System;
using System.Collections.Generic;
using CefSharp;
using CefSharp.Wpf;

namespace VetaleBrowser.VetaleBrowser.Core.Scripts.Browser;

public static class CefBrowserConfig
{
    public static readonly string UserDataDir;
    public static readonly string DiskCacheDir;
    public static readonly IReadOnlyList<string> CommandLineSwitches = new[]
    {
        "enable-javascript",
        "enable-media-stream",
        "disable-background-timer-throttling",
        "disable-renderer-backgrounding",
        "disable-backgrounding-occluded-windows"
    };

    static CefBrowserConfig()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        UserDataDir = System.IO.Path.Combine(appData, "VetaleBrowser", "cef_data");
        DiskCacheDir = System.IO.Path.Combine(UserDataDir, "cache");
        EnsureDirectories();
    }

    public static void EnsureDirectories()
    {
        System.IO.Directory.CreateDirectory(UserDataDir);
        System.IO.Directory.CreateDirectory(DiskCacheDir);
    }

    public static CefSettings CreateSettings()
    {
        var settings = new CefSettings
        {
            CachePath = DiskCacheDir,
            RootCachePath = UserDataDir,
            PersistSessionCookies = true,
            LogSeverity = LogSeverity.Disable,
            LogFile = System.IO.Path.Combine(UserDataDir, "cef.log"),
            RemoteDebuggingPort = 9223, // потрібен CefDevToolsClient (favicon/title/fullscreen через DevTools)
            WindowlessRenderingEnabled = false,
            // БЕЗ кастомного UserAgent: дефолтний десктопний Chrome UA від CEF.
            // Кастомний/мобільний UA змушував сайти віддавати спрощені версії.
            UserAgent = null,
        };

        settings.CefCommandLineArgs.Add("disable-background-timer-throttling");
        settings.CefCommandLineArgs.Add("disable-renderer-backgrounding");
        settings.CefCommandLineArgs.Add("disable-backgrounding-occluded-windows");
        return settings;
    }

    public static void EnsureInitialized() => CefWpfBootstrapper.EnsureInitialized();
}
