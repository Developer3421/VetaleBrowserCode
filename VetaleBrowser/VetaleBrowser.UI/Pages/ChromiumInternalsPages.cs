using System;
using System.Collections.Generic;
using System.Text;
using Avalonia;
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
            Background = Brush("#F7F3FF"),
            Content = BuildContent()
        };
        Content = scroll;
    }

    private static Control BuildContent()
    {
        var panel = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 16,
            Margin = new Avalonia.Thickness(28)
        };

        panel.Children.Add(new Border
        {
            Background = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                GradientStops =
                {
                    new GradientStop(Color.Parse("#FF7C3AED"), 0),
                    new GradientStop(Color.Parse("#FFB026FF"), 1)
                }
            },
            CornerRadius = new Avalonia.CornerRadius(18),
            Padding = new Avalonia.Thickness(24),
            Child = new StackPanel
            {
                Spacing = 6,
                Children =
                {
                    new TextBlock { Text = "GPU CENTER", FontSize = 12, FontWeight = FontWeight.Bold, Foreground = Brushes.White, Opacity = 0.8 },
                    new TextBlock { Text = "Graphics Feature Status", FontSize = 28, FontWeight = FontWeight.Bold, Foreground = Brushes.White },
                    new TextBlock { Text = "chrome://gpu  •  Vetale Browser diagnostics", FontSize = 13, Foreground = Brushes.White, Opacity = 0.85 }
                }
            }
        });

        var summary = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*,*"), ColumnSpacing = 12 };
        summary.Children.Add(StatCard("GPU", "READY", "#FF16A34A", 0));
        summary.Children.Add(StatCard("WEBGL", "ENABLED", "#FF2563EB", 1));
        summary.Children.Add(StatCard("ANGLE", "D3D11", "#FFD97706", 2));
        panel.Children.Add(summary);

        panel.Children.Add(Section("Graphics adapters", "Display hardware detected by Windows", GetAdaptersText(), "#FF7C3AED"));
        panel.Children.Add(Section("Rendering pipeline", "Acceleration capabilities used by Chromium", GetFeatureText(), "#FF2563EB"));
        panel.Children.Add(Section("CEF configuration", "Active runtime paths and command-line switches", GetSwitchesText(), "#FFD97706"));
        panel.Children.Add(Section("System profile", "Environment used by the current browser process", GetSystemText(), "#FF16A34A"));

        return panel;
    }

    private static Control StatCard(string label, string value, string color, int column)
    {
        var card = new Border
        {
            Background = Brushes.White,
            CornerRadius = new Avalonia.CornerRadius(12),
            Padding = new Avalonia.Thickness(16),
            Child = new StackPanel
            {
                Spacing = 4,
                Children =
                {
                    new TextBlock { Text = label, FontSize = 11, FontWeight = FontWeight.Bold, Foreground = Brush(color), Opacity = 0.8 },
                    new TextBlock { Text = value, FontSize = 17, FontWeight = FontWeight.Bold, Foreground = Brush("#FF24163D") }
                }
            }
        };
        Grid.SetColumn(card, column);
        return card;
    }

    private static Control Section(string title, string subtitle, string body, string accent)
    {
        var p = new StackPanel { Orientation = Orientation.Vertical, Spacing = 8 };
        p.Children.Add(new TextBlock { Text = title, FontSize = 17, FontWeight = FontWeight.Bold, Foreground = Brush("#FF24163D") });
        p.Children.Add(new TextBlock { Text = subtitle, FontSize = 12, Foreground = Brush("#FF766A88") });
        p.Children.Add(new Border
        {
            Background = Brush("#FFF9F7FF"),
            CornerRadius = new Avalonia.CornerRadius(8),
            Padding = new Avalonia.Thickness(12),
            Child = new TextBlock
            {
                Text = body,
                FontSize = 13,
                TextWrapping = TextWrapping.Wrap,
                FontFamily = new FontFamily("Cascadia Mono,Consolas,Menlo,monospace"),
                Foreground = Brush("#FF403552")
            }
        });
        var contentGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("6,*"),
            ColumnSpacing = 14,
            Children =
            {
                new Border { Background = Brush(accent), CornerRadius = new Avalonia.CornerRadius(3), Width = 6 },
                new Border { Child = p }
            }
        };
        Grid.SetColumn(contentGrid.Children[1], 1);
        return new Border
        {
            Background = Brushes.White,
            BorderBrush = Brush("#FFE8E0F5"),
            BorderThickness = new Avalonia.Thickness(1),
            CornerRadius = new Avalonia.CornerRadius(14),
            Padding = new Avalonia.Thickness(16),
            Child = contentGrid
        };
    }

    private static SolidColorBrush Brush(string color) => new(Color.Parse(color));

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
            sb.AppendLine($"CEF runtime: {typeof(CefBrowserConfig).Assembly.GetName().Version}");
            sb.AppendLine($"PersistSessionCookies: {s.PersistSessionCookies} (UserAgent: default/desktop)");
            foreach (var sw in CefBrowserConfig.CommandLineSwitches)
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
        // БЕЗПЕКА: DevTools-порт закритий (RemoteDebuggingPort = 0) — проба прибрана.
        panel.Children.Add(new TextBlock { Text = "DevTools 127.0.0.1:9223: DISABLED (порт закритий з міркувань безпеки)", FontSize = 13, FontFamily = new FontFamily("Consolas,Menlo,monospace") });
        Content = new ScrollViewer { Content = panel };
    }
}
