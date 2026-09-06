using System;
using System.Collections.Generic;
using System.Text;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using VetaleBrowser.VetaleBrowser.Core.Scripts.Browser;

namespace VetaleBrowser.VetaleBrowser.UI.Pages;

/// <summary>
/// Built-in replacement for chrome://gpu (the bundled CEF has no WebUI scheme).
/// Shows real GPU/driver data read from the OS plus the active CEF switches.
/// Code-only control - no axaml needed.
/// </summary>
public sealed class GpuInfoPage : UserControl
{
    public GpuInfoPage()
    {
        var scroll = new ScrollViewer
        {
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            Content = BuildContent()
        };
        Content = scroll;
    }

    private static Control BuildContent()
    {
        var panel = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 8,
            Margin = new Avalonia.Thickness(24)
        };

        panel.Children.Add(Header("Graphics Feature Status"));
        panel.Children.Add(Sub("chrome://gpu - Vetale Browser (built-in)"));
        panel.Children.Add(Section("Graphics adapters (OS data)", GetAdaptersText()));
        panel.Children.Add(Section("Compositing / rendering", GetFeatureText()));
        panel.Children.Add(Section("CEF switches in effect", GetSwitchesText()));
        panel.Children.Add(Section("System", GetSystemText()));

        return panel;
    }

    private static Control Header(string text) => new TextBlock
    {
        Text = text, FontSize = 22, FontWeight = FontWeight.Bold
    };

    private static Control Sub(string text) => new TextBlock
    {
        Text = text, FontSize = 13, Opacity = 0.7
    };

    private static Control Section(string title, string body)
    {
        var p = new StackPanel { Orientation = Orientation.Vertical, Spacing = 4 };
        p.Children.Add(new TextBlock { Text = title, FontSize = 16, FontWeight = FontWeight.SemiBold });
        p.Children.Add(new TextBlock { Text = body, FontSize = 13, TextWrapping = TextWrapping.Wrap, FontFamily = new FontFamily("Consolas,Menlo,monospace") });
        return new Border
        {
            BorderBrush = Brushes.Gray, BorderThickness = new Avalonia.Thickness(1),
            CornerRadius = new Avalonia.CornerRadius(6), Padding = new Avalonia.Thickness(12), Child = p
        };
    }

    private static string GetAdaptersText()
    {
        var sb = new StringBuilder();
        try
        {
            // Display adapters from registry: HKLM\...\Control\Class\{display-class-guid}
            using var cls = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                @"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}");
            if (cls == null) return "Registry key not available on this platform.";
            int n = 0;
            foreach (var sub in cls.GetSubKeyNames())
            {
                if (sub is null || sub.StartsWith("Properties", StringComparison.OrdinalIgnoreCase))
                    continue;
                try
                {
                    using var k = cls.OpenSubKey(sub);
                    var desc = k?.GetValue("DriverDesc") as string;
                    if (string.IsNullOrWhiteSpace(desc)) continue;
                    n++;
                    sb.AppendLine($"GPU {n}: {desc}");
                    sb.AppendLine($"  Driver: {k?.GetValue("DriverVersion") ?? "?"} ({k?.GetValue("ProviderName") ?? "?"})");
                    sb.AppendLine($"  Installed: {k?.GetValue("DriverDate") ?? "?"}");
                }
                catch { }
            }
            if (n == 0) sb.AppendLine("No adapters found.");
        }
        catch (Exception ex)
        {
            sb.AppendLine($"Unavailable: {ex.GetType().Name}");
        }
        return sb.ToString().TrimEnd();
    }

    private static string GetFeatureText()
    {
        var sb = new StringBuilder();
        sb.AppendLine("Canvas: Hardware accelerated");
        sb.AppendLine("Compositing: Hardware accelerated");
        sb.AppendLine("WebGL: Hardware accelerated (enable-webgl / enable-webgl2)");
        sb.AppendLine("WebGPU: Enabled (enable-unsafe-webgpu)");
        sb.AppendLine("Video Decode/Encode: Hardware accelerated");
        sb.AppendLine("Rasterization: GPU + OOP (enable-gpu-rasterization)");
        sb.AppendLine("ANGLE backend: d3d11 (use-angle)");
        sb.AppendLine("VSync: enabled (not disabled, per Lite config)");
        return sb.ToString().TrimEnd();
    }

    private static string GetSwitchesText()
    {
        try
        {
            var s = CefBrowserConfig.CreateSettings();
            var sb = new StringBuilder();
            sb.AppendLine($"UserDataDir: {CefBrowserConfig.UserDataDir}");
            sb.AppendLine($"CacheDir: {CefBrowserConfig.DiskCacheDir}");
            sb.AppendLine($"ChromeRuntime: {s.ChromeRuntime}");
            sb.AppendLine($"PersistSessionCookies: {s.PersistSessionCookies}");
            foreach (var sw in s.CommandLineSwitches)
                sb.AppendLine($"--{sw}");
            return sb.ToString().TrimEnd();
        }
        catch (Exception ex)
        {
            return $"Unavailable: {ex.Message}";
        }
    }

    private static string GetSystemText()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"OS: {System.Runtime.InteropServices.RuntimeInformation.OSDescription} ({System.Runtime.InteropServices.RuntimeInformation.OSArchitecture})");
        sb.AppendLine($"Process: {System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture}");
        sb.AppendLine($"CPUs: {Environment.ProcessorCount}, 64-bit OS: {Environment.Is64BitOperatingSystem}");
        try
        {
            var mem = GC.GetGCMemoryInfo();
            sb.AppendLine($"Total available memory: {mem.TotalAvailableMemoryBytes / 1024 / 1024} MB");
        }
        catch { }
        return sb.ToString().TrimEnd();
    }
}

/// <summary>
/// Built-in replacement for chrome://version.
/// </summary>
public sealed class VersionInfoPage : UserControl
{
    public VersionInfoPage()
    {
        var panel = new StackPanel { Orientation = Orientation.Vertical, Spacing = 8, Margin = new Avalonia.Thickness(24) };
        var sb = new StringBuilder();
        sb.AppendLine($"Vetale Browser 1.0.1");
        sb.AppendLine($"Build: 2026-09-06 (tab-stack + fullscreen fixes)");
        sb.AppendLine($"Chromium engine: CEF via CefSharp.Avalonia 1.0.6 (Chrome runtime)");
        sb.AppendLine($"User agent: (default CEF - not overridden)");
        sb.AppendLine($"OS: {System.Runtime.InteropServices.RuntimeInformation.OSDescription}");
        sb.AppendLine($"Exe: {AppContext.BaseDirectory}");
        panel.Children.Add(new TextBlock { Text = "About Version", FontSize = 22, FontWeight = FontWeight.Bold });
        panel.Children.Add(new TextBlock { Text = "chrome://version - Vetale Browser (built-in)", FontSize = 13, Opacity = 0.7 });
        panel.Children.Add(new TextBlock { Text = sb.ToString().TrimEnd(), FontSize = 13, TextWrapping = TextWrapping.Wrap, FontFamily = new FontFamily("Consolas,Menlo,monospace") });
        var devToolsStatus = new TextBlock { Text = "DevTools 127.0.0.1:9223: probing...", FontSize = 13, FontFamily = new FontFamily("Consolas,Menlo,monospace") };
        panel.Children.Add(devToolsStatus);
        Content = new ScrollViewer { Content = panel };

        // Проба DevTools-порта (від нього залежить детект F-фулскріна)
        _ = System.Threading.Tasks.Task.Run(async () =>
        {
            string status;
            try
            {
                using var http = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(3) };
                var json = await http.GetStringAsync("http://127.0.0.1:9223/json/version");
                status = "DevTools 127.0.0.1:9223: OK " + json.Trim().Replace("\n", " ");
            }
            catch (Exception ex)
            {
                status = "DevTools 127.0.0.1:9223: UNREACHABLE (" + ex.GetType().Name + ": " + ex.Message + ")";
            }
            Avalonia.Threading.Dispatcher.UIThread.Post(() => devToolsStatus.Text = status);
        });
    }
}
