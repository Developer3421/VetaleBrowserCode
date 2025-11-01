using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using VetaleBrowser.VetaleBrowser.UI.Services;

namespace VetaleBrowser.VetaleBrowser.UI.Pages;

public partial class ToolsMainPage : UserControl
{
    private readonly IFaviconService? _faviconService;
    private StackPanel? _toolsListPanel;

    public event EventHandler<ToolNavigationEventArgs>? NavigateInWebView;
    public event EventHandler<string>? NavigateInMainTab;

    public ToolsMainPage()
    {
        InitializeComponent();
        _faviconService = new FaviconService();
        _toolsListPanel = this.FindControl<StackPanel>("ToolsListPanel");
        
        LoadTools();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void LoadTools()
    {
        if (_toolsListPanel == null) return;

        var tools = new List<ToolItem>
        {
            new ToolItem
            {
                Name = "Vetale AI Chat",
                Description = "Чат з штучним інтелектом Vetale",
                IconUrl = null,
                IconEmoji = "🤖",
                Action = () => OpenVetaleAIChat()
            },
            new ToolItem
            {
                Name = "DuckDuckGo AI Chat",
                Description = "Безкоштовний AI чат від DuckDuckGo",
                IconUrl = "https://duckduckgo.com",
                NavigateUrl = "https://duckduckgo.com/aichat",
                Action = () => OpenInWebView("DuckDuckGo AI Chat", "https://duckduckgo.com/aichat")
            },
            new ToolItem
            {
                Name = "Microsoft Copilot",
                Description = "AI асистент від Microsoft",
                IconUrl = "https://copilot.microsoft.com",
                NavigateUrl = "https://copilot.microsoft.com",
                Action = () => OpenInWebView("Microsoft Copilot", "https://copilot.microsoft.com")
            },
            new ToolItem
            {
                Name = "Google Gemini",
                Description = "AI від Google",
                IconUrl = "https://gemini.google.com",
                NavigateUrl = "https://gemini.google.com",
                Action = () => OpenInWebView("Google Gemini", "https://gemini.google.com")
            },
            new ToolItem
            {
                Name = "Replika AI",
                Description = "AI компаньйон для спілкування",
                IconUrl = "https://replika.com",
                NavigateUrl = "https://replika.com",
                Action = () => OpenInWebView("Replika AI", "https://replika.com")
            },
            new ToolItem
            {
                Name = "Vetale DevTools",
                Description = "Інструменти розробника",
                IconUrl = null,
                IconEmoji = "🔧",
                Action = () => OpenVetaleDevTools()
            },
            new ToolItem
            {
                Name = "Історія",
                Description = "Історія відвідувань",
                IconUrl = null,
                IconEmoji = "📜",
                Action = () => OpenHistory()
            }
        };

        foreach (var tool in tools)
        {
            _toolsListPanel.Children.Add(CreateToolItemControl(tool));
        }
    }

    private Border CreateToolItemControl(ToolItem tool)
    {
        var border = new Border
        {
            Classes = { "tool-item" }
        };

        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto")
        };

        // Icon
        var iconPanel = new Panel
        {
            Width = 32,
            Height = 32,
            Margin = new Thickness(0, 0, 15, 0),
            VerticalAlignment = VerticalAlignment.Center
        };

        if (!string.IsNullOrEmpty(tool.IconEmoji))
        {
            // Use emoji as icon
            var emojiText = new TextBlock
            {
                Text = tool.IconEmoji,
                FontSize = 24,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            iconPanel.Children.Add(emojiText);
        }
        else if (!string.IsNullOrEmpty(tool.IconUrl))
        {
            // Load favicon
            var iconImage = new Image
            {
                Width = 32,
                Height = 32,
                Stretch = Stretch.Uniform
            };
            
            LoadFaviconAsync(iconImage, tool.IconUrl);
            iconPanel.Children.Add(iconImage);
        }

        Grid.SetColumn(iconPanel, 0);
        grid.Children.Add(iconPanel);

        // Text content (clickable area)
        var textPanel = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            Spacing = 4,
            Cursor = new Cursor(StandardCursorType.Hand)
        };

        var nameText = new TextBlock
        {
            Text = tool.Name,
            FontSize = 16,
            FontWeight = FontWeight.SemiBold,
            Foreground = new SolidColorBrush(Color.Parse("#303030"))
        };

        var descText = new TextBlock
        {
            Text = tool.Description,
            FontSize = 13,
            Foreground = new SolidColorBrush(Color.Parse("#707070"))
        };

        textPanel.Children.Add(nameText);
        textPanel.Children.Add(descText);

        // Click handler for text area (opens in WebView)
        textPanel.PointerPressed += (s, e) =>
        {
            if (e.GetCurrentPoint(textPanel).Properties.IsLeftButtonPressed)
            {
                tool.Action?.Invoke();
            }
        };

        Grid.SetColumn(textPanel, 1);
        grid.Children.Add(textPanel);

        // Navigation button (opens in main tab) - only for external tools
        if (!string.IsNullOrEmpty(tool.NavigateUrl))
        {
            var navButton = new Button
            {
                Classes = { "nav-button" },
                Content = "→ Перейти в браузері",
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(10, 0, 0, 0)
            };

            navButton.Click += (s, e) =>
            {
                NavigateInMainTab?.Invoke(this, tool.NavigateUrl);
                e.Handled = true; // Prevent border click
            };

            Grid.SetColumn(navButton, 2);
            grid.Children.Add(navButton);
        }

        border.Child = grid;


        return border;
    }

    private async void LoadFaviconAsync(Image image, string url)
    {
        if (_faviconService == null) return;

        try
        {
            var uri = new Uri(url);
            var favicon = await _faviconService.GetFaviconAsync(uri, 32);
            if (favicon != null)
            {
                image.Source = favicon;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ToolsMainPage] Failed to load favicon for {url}: {ex.Message}");
        }
    }

    private void OpenVetaleAIChat()
    {
        System.Diagnostics.Debug.WriteLine("[ToolsMainPage] Opening Vetale AI Chat...");
        // TODO: Implement Vetale AI Chat opening
    }

    private void OpenVetaleDevTools()
    {
        System.Diagnostics.Debug.WriteLine("[ToolsMainPage] Opening Vetale DevTools...");
        // TODO: Implement DevTools opening
    }

    private void OpenHistory()
    {
        System.Diagnostics.Debug.WriteLine("[ToolsMainPage] Opening History...");
        // TODO: Implement History opening
    }

    private void OpenInWebView(string toolName, string url)
    {
        System.Diagnostics.Debug.WriteLine($"[ToolsMainPage] Opening in WebView: {toolName} - {url}");
        NavigateInWebView?.Invoke(this, new ToolNavigationEventArgs(toolName, url));
    }

    private class ToolItem
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string? IconUrl { get; set; }
        public string? IconEmoji { get; set; }
        public string? NavigateUrl { get; set; }
        public Action? Action { get; set; }
    }
}

public class ToolNavigationEventArgs : EventArgs
{
    public string ToolName { get; }
    public string Url { get; }

    public ToolNavigationEventArgs(string toolName, string url)
    {
        ToolName = toolName;
        Url = url;
    }
}

