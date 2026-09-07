using System;
using System.Collections.Generic;
using CefSharp.Avalonia;

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
            NoSandbox = true,
            CachePath = DiskCacheDir,
            RootCachePath = UserDataDir,
            PersistSessionCookies = true,
            PersistUserPreferences = true,
            LogSeverity = CefLogSeverity.Disable,
            LogFile = System.IO.Path.Combine(UserDataDir, "cef.log"),
            JavascriptFlags = "--max-old-space-size=128 --optimize-for-size",
            WindowlessRenderingEnabled = false,
            RemoteDebuggingPort = 9223
        };

        settings.CommandLineSwitches.AddRange(CommandLineSwitches);
        return settings;
    }

    public static void EnsureInitialized() => EnsureDirectories();
}
