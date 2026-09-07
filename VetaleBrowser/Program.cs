using Avalonia;
using System;
using System.Diagnostics;
using VetaleBrowser.VetaleBrowser.Core.Scripts.Browser;

namespace VetaleBrowser;

class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // MEMORY OPTIMIZATION: Set aggressive GC mode for better memory management
        System.Runtime.GCSettings.LargeObjectHeapCompactionMode = System.Runtime.GCLargeObjectHeapCompactionMode.CompactOnce;
        
        // Skip tab-helper mode since we no longer spawn subprocesses (memory optimization)
        if (args is { Length: > 0 } && Array.Exists(args, a => a == "--tab-helper"))
        {
            // Immediately exit - subprocess mode is disabled for memory savings
            return;
        }

        // Configure CEF/WebView before any UI is created
        ConfigureWebEngines();

        BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args ?? Array.Empty<string>());
    }


    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();

    /// <summary>
    /// Set process-wide environment variables to enable GPU acceleration and useful features
    /// for Chromium-based engines (WebView2/CEF). Unknown flags are ignored by the engine.
    /// Must run before any WebView instance is created.
    /// </summary>
    private static void ConfigureWebEngines()
    {
        try
        {
            ConfigureCefSwitches();
        }
        catch
        {
            // Best-effort; ignore if anything goes wrong here.
        }
    }

    private static void ConfigureCefSwitches()
    {
        try
        {
            // CefSharp.Avalonia starts its native browser process on first attachment.
            CefBrowserConfig.EnsureDirectories();
            CefBrowserConfig.EnsureInitialized();
            VetaleBrowser.Database.Services.LiteSettingsMigrator.ResetPersistedChromiumLanguage();
            Debug.WriteLine($"[Program] CEF dirs ready: {CefBrowserConfig.UserDataDir}");
        }
        catch
        {
            // no-op
        }
    }
}