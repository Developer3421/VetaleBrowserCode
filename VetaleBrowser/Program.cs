using Avalonia;
using System;

namespace VetaleBrowser;

class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        // Ensure GPU rendering and related Chromium features are enabled for WebView engines (WebView2/CEF) where applicable.
        ConfigureWebEngines();

        BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);
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
            // Common GPU/performance friendly switches for Chromium-based engines.
            var additionalArgs = string.Join(" ", new[]
            {
                "--enable-gpu",
                "--ignore-gpu-blocklist",
                "--disable-software-rasterizer",
                "--enable-zero-copy",
                "--enable-native-gpu-memory-buffers",
                "--enable-accelerated-video-decode",
                "--enable-accelerated-video-encode",
                "--enable-webgl",
                "--enable-webgpu",
                "--use-angle=d3d11",
                "--enable-features=CanvasOopRasterization,UseSkiaRenderer,PlatformHEVCDecoderSupport,SharedArrayBuffer,AllowContentInitiatedDataUrlNavigations",
                "--disable-features=CalculateNativeWinOcclusion"
            });

            // WebView2 respects this environment variable when creating the browser process.
            Environment.SetEnvironmentVariable(
                "WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS",
                additionalArgs,
                EnvironmentVariableTarget.Process);

            // Optional: Open a remote debugging port for DevTools (useful during development).
            // Uncomment if needed.
            // Environment.SetEnvironmentVariable("WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS",
            //     additionalArgs + " --remote-debugging-port=9222", EnvironmentVariableTarget.Process);
        }
        catch
        {
            // Best-effort; ignore if anything goes wrong here.
        }
    }
}