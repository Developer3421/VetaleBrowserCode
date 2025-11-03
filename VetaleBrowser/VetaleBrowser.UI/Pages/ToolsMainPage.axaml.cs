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
                NameKey = "Tools.VetaleAI.Name",
                DescriptionKey = "Tools.VetaleAI.Description",
                IconUrl = null,
                IconEmoji = "🤖",
                Action = () => OpenVetaleAIChat()
            },
            new ToolItem
            {
                NameKey = "Tools.DuckDuckGoAI.Name",
                DescriptionKey = "Tools.DuckDuckGoAI.Description",
                IconUrl = "https://duckduckgo.com",
                NavigateUrl = "https://duckduckgo.com/aichat",
                Action = () => OpenInWebView(GetLocalizedString("Tools.DuckDuckGoAI.Name"), "https://duckduckgo.com/aichat")
            },
            new ToolItem
            {
                NameKey = "Tools.Copilot.Name",
                DescriptionKey = "Tools.Copilot.Description",
                IconUrl = "https://copilot.microsoft.com",
                NavigateUrl = "https://copilot.microsoft.com",
                Action = () => OpenInWebView(GetLocalizedString("Tools.Copilot.Name"), "https://copilot.microsoft.com")
            },
            new ToolItem
            {
                NameKey = "Tools.Gemini.Name",
                DescriptionKey = "Tools.Gemini.Description",
                IconUrl = "https://gemini.google.com",
                NavigateUrl = "https://gemini.google.com",
                Action = () => OpenInWebView(GetLocalizedString("Tools.Gemini.Name"), "https://gemini.google.com")
            },
            new ToolItem
            {
                NameKey = "Tools.Replika.Name",
                DescriptionKey = "Tools.Replika.Description",
                IconUrl = "https://replika.com",
                NavigateUrl = "https://replika.com",
                Action = () => OpenInWebView(GetLocalizedString("Tools.Replika.Name"), "https://replika.com")
            },
            new ToolItem
            {
                NameKey = "Tools.DevTools.Name",
                DescriptionKey = "Tools.DevTools.Description",
                IconUrl = null,
                IconEmoji = "🔧",
                Action = () => OpenVetaleDevTools()
            },
            new ToolItem
            {
                NameKey = "Tools.History.Name",
                DescriptionKey = "Tools.History.Description",
                IconUrl = null,
                IconEmoji = "📜",
                Action = () => OpenHistory()
            },
            new ToolItem
            {
                NameKey = "Tools.Console.Name",
                DescriptionKey = "Tools.Console.Description",
                IconUrl = null,
                IconEmoji = "🖥️",
                Action = () => OpenConsole()
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
            Text = GetLocalizedString(tool.NameKey),
            FontSize = 16,
            FontWeight = FontWeight.SemiBold,
            Foreground = new SolidColorBrush(Color.Parse("#303030"))
        };

        var descText = new TextBlock
        {
            Text = GetLocalizedString(tool.DescriptionKey),
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
                Content = GetLocalizedString("Tools.NavigateButton"),
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
        System.Diagnostics.Debug.WriteLine("[ToolsMainPage] ===== Opening History START =====");
        
        try
        {
            System.Diagnostics.Debug.WriteLine("[ToolsMainPage] Step 1: Creating HistoryWindow instance...");
            var historyWindow = new Windows.HistoryWindow();
            System.Diagnostics.Debug.WriteLine("[ToolsMainPage] Step 1: SUCCESS - HistoryWindow created");
            
            System.Diagnostics.Debug.WriteLine("[ToolsMainPage] Step 2: Getting HistoryInstance from DatabaseManager...");
            var historyService = VetaleBrowser.Core.Scripts.GlobalManagers.DatabaseManager.HistoryInstance;
            System.Diagnostics.Debug.WriteLine($"[ToolsMainPage] Step 2: SUCCESS - HistoryService obtained: {historyService != null}");
            
            if (historyService == null)
            {
                System.Diagnostics.Debug.WriteLine("[ToolsMainPage] ERROR: HistoryService is null! Cannot open History window.");
                return;
            }
            
            System.Diagnostics.Debug.WriteLine("[ToolsMainPage] Step 3: Setting HistoryService on window...");
            historyWindow.SetHistoryService(historyService);
            System.Diagnostics.Debug.WriteLine("[ToolsMainPage] Step 3: SUCCESS - HistoryService set");
            
            System.Diagnostics.Debug.WriteLine("[ToolsMainPage] Step 4: Showing window...");
            historyWindow.Show();
            System.Diagnostics.Debug.WriteLine("[ToolsMainPage] Step 4: SUCCESS - Window shown");
            
            System.Diagnostics.Debug.WriteLine("[ToolsMainPage] ===== Opening History COMPLETE =====");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ToolsMainPage] ===== ERROR in OpenHistory =====");
            System.Diagnostics.Debug.WriteLine($"[ToolsMainPage] Exception Type: {ex.GetType().Name}");
            System.Diagnostics.Debug.WriteLine($"[ToolsMainPage] Message: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"[ToolsMainPage] Stack trace: {ex.StackTrace}");
            
            if (ex.InnerException != null)
            {
                System.Diagnostics.Debug.WriteLine($"[ToolsMainPage] Inner Exception: {ex.InnerException.Message}");
                System.Diagnostics.Debug.WriteLine($"[ToolsMainPage] Inner Stack trace: {ex.InnerException.StackTrace}");
            }
            
            // Показуємо користувачу діалог з помилкою
            try
            {
                var errorWindow = new Window
                {
                    Title = "Помилка відкриття історії",
                    Width = 500,
                    Height = 300,
                    WindowStartupLocation = WindowStartupLocation.CenterScreen,
                    Content = new StackPanel
                    {
                        Margin = new Thickness(20),
                        Spacing = 10,
                        Children =
                        {
                            new TextBlock
                            {
                                Text = "❌ Помилка відкриття історії",
                                FontSize = 18,
                                FontWeight = FontWeight.Bold,
                                Foreground = new SolidColorBrush(Color.Parse("#E74C3C"))
                            },
                            new TextBlock
                            {
                                Text = $"Тип помилки: {ex.GetType().Name}",
                                FontSize = 12,
                                Foreground = new SolidColorBrush(Color.Parse("#606060"))
                            },
                            new ScrollViewer
                            {
                                Height = 120,
                                Content = new TextBlock
                                {
                                    Text = ex.Message + (ex.InnerException != null ? "\n\nВнутрішня помилка: " + ex.InnerException.Message : ""),
                                    TextWrapping = TextWrapping.Wrap,
                                    FontSize = 11
                                }
                            },
                            new Button
                            {
                                Content = "OK",
                                HorizontalAlignment = HorizontalAlignment.Center,
                                Margin = new Thickness(0, 10, 0, 0),
                                Padding = new Thickness(30, 8),
                                Background = new SolidColorBrush(Color.Parse("#9A1CE8")),
                                Foreground = Brushes.White,
                                BorderThickness = new Thickness(0),
                                CornerRadius = new CornerRadius(4)
                            }
                        }
                    }
                };
                
                var okButton = (Button)((StackPanel)errorWindow.Content).Children[3];
                okButton.Click += (s, e) => errorWindow.Close();
                
                errorWindow.Show();
            }
            catch (Exception dialogEx)
            {
                System.Diagnostics.Debug.WriteLine($"[ToolsMainPage] Failed to show error dialog: {dialogEx.Message}");
            }
        }
    }

    private void OpenConsole()
    {
        System.Diagnostics.Trace.WriteLine("[ToolsMainPage] Opening Console...");
        
        try
        {
            var consoleWindow = new Windows.ConsoleWindow();
            var consoleService = Core.Scripts.GlobalManagers.DatabaseManager.ConsoleInstance;
            consoleWindow.SetConsoleService(consoleService);
            consoleWindow.Show();
            
            System.Diagnostics.Trace.WriteLine("[ToolsMainPage] Console window opened successfully");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"[ToolsMainPage] Error opening console: {ex.Message}");
        }
    }


    private void OpenInWebView(string toolName, string url)
    {
        System.Diagnostics.Trace.WriteLine($"[ToolsMainPage] Opening in WebView: {toolName} - {url}");
        NavigateInWebView?.Invoke(this, new ToolNavigationEventArgs(toolName, url));
    }

    private string GetLocalizedString(string key)
    {
        try
        {
            if (Application.Current?.Resources.TryGetResource(key, null, out var resource) == true && resource is string str)
            {
                System.Diagnostics.Debug.WriteLine($"[ToolsMainPage] Localized string for '{key}': '{str}'");
                return str;
            }
            System.Diagnostics.Debug.WriteLine($"[ToolsMainPage] WARNING: Localized string not found for key '{key}', using key as fallback");
            return key;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ToolsMainPage] ERROR getting localized string for '{key}': {ex.Message}");
            return key;
        }
    }

    private class ToolItem
    {
        public string NameKey { get; set; } = string.Empty;
        public string DescriptionKey { get; set; } = string.Empty;
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
