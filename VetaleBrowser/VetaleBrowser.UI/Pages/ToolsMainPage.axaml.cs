using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using VetaleBrowser.VetaleBrowser.UI.Services;

namespace VetaleBrowser.VetaleBrowser.UI.Pages;

public partial class ToolsMainPage : UserControl
{
    private readonly IFaviconService? _faviconService;
    private StackPanel? _toolsListPanel;

    // Static window references to prevent memory leaks
    private static Windows.HistoryWindow? _historyWindowInstance;

    private static Windows.VetaleAIWindow? _vetaleAiWindowInstance;
    private static Windows.DownloadsWindow? _downloadsWindowInstance;
    private static Windows.UserAgreementWindow? _userAgreementWindowInstance;
    private static Windows.AboutWindow? _aboutWindowInstance;
    private static Windows.VetaleSearchSettingsWindow? _vetaleSearchSettingsWindowInstance;

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
            // Put complaint at the very top
            new ToolItem
            {
                NameKey = "Tools.AIComplaint.Name",
                DescriptionKey = "Tools.AIComplaint.Description",
                IconUrl = null,
                IconEmoji = "⚠️",
                Action = () => OpenAIComplaintWindow()
            },
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
            // Move Gemini right after Copilot
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
                NameKey = "Tools.Downloads.Name",
                DescriptionKey = "Tools.Downloads.Description",
                IconUrl = null,
                IconEmoji = "⬇️",
                Action = () => OpenDownloads()
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
                NameKey = "Tools.UserAgreement.Name",
                DescriptionKey = "Tools.UserAgreement.Description",
                IconUrl = null,
                IconEmoji = "🛡️",
                Action = () => OpenUserAgreement()
            },
            new ToolItem
            {
                NameKey = "Tools.VetaleSearchSettings.Name",
                DescriptionKey = "Tools.VetaleSearchSettings.Description",
                IconUrl = null,
                IconEmoji = "🔍",
                Action = () => OpenVetaleSearchSettings()
            },
            new ToolItem
            {
                NameKey = "Tools.About.Name",
                DescriptionKey = "Tools.About.Description",
                IconUrl = null,
                IconEmoji = "ℹ️",
                Action = () => OpenAbout()
            }

            // (removed) Gemini was previously at the end
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
            Classes = { "tool-item" },
            Cursor = new Cursor(StandardCursorType.Hand)
        };

        Button? navButton = null;

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
                FontFamily = new FontFamily("Segoe UI Emoji, Noto Color Emoji, Apple Color Emoji"),
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

        Grid.SetColumn(textPanel, 1);
        grid.Children.Add(textPanel);

        // Navigation button (opens in main tab) - only for external tools
        if (!string.IsNullOrEmpty(tool.NavigateUrl))
        {
            navButton = new Button
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

        // Whole tile is clickable (not just the title text)
        border.PointerPressed += (s, e) =>
        {
            if (!e.GetCurrentPoint(border).Properties.IsLeftButtonPressed)
                return;
            // Ignore presses on the navigation button (it has its own action)
            if (navButton != null && Equals(e.Source, navButton))
                return;
            tool.Action?.Invoke();
        };

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
        try
        {
            // Check if a window is already open
            if (_vetaleAiWindowInstance != null)
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine("[ToolsMainPage] Reusing existing VetaleAIWindow");
                    _vetaleAiWindowInstance.Activate();
                    _vetaleAiWindowInstance.WindowState = WindowState.Normal;
                    System.Diagnostics.Debug.WriteLine("[ToolsMainPage] VetaleAI window activated");
                    return;
                }
                catch
                {
                    // Window is closed, clear the reference
                    System.Diagnostics.Debug.WriteLine("[ToolsMainPage] Previous VetaleAI window was closed, creating new one");
                    _vetaleAiWindowInstance = null;
                }
            }

            System.Diagnostics.Debug.WriteLine("[ToolsMainPage] Creating new VetaleAIWindow instance...");
            _vetaleAiWindowInstance = new Windows.VetaleAIWindow();
            
            // Subscribe to window close event to clear the reference
            _vetaleAiWindowInstance.Closed += (s, e) =>
            {
                System.Diagnostics.Debug.WriteLine("[ToolsMainPage] VetaleAIWindow closed, clearing reference");
                _vetaleAiWindowInstance = null;
            };
            
            System.Diagnostics.Debug.WriteLine("[ToolsMainPage] Showing VetaleAI window...");
            _vetaleAiWindowInstance.Show();
            System.Diagnostics.Debug.WriteLine("[ToolsMainPage] VetaleAI window opened successfully");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ToolsMainPage] ERROR opening VetaleAI: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"[ToolsMainPage] Stack trace: {ex.StackTrace}");
        }
    }

    private void OpenHistory()
    {
        System.Diagnostics.Debug.WriteLine("[ToolsMainPage] ===== Opening History START =====");
        
        try
        {
            // Check if a window is already open
            if (_historyWindowInstance != null)
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine("[ToolsMainPage] Reusing existing HistoryWindow");
                    _historyWindowInstance.Activate();
                    _historyWindowInstance.WindowState = WindowState.Normal;
                    System.Diagnostics.Debug.WriteLine("[ToolsMainPage] ===== Opening History COMPLETE (reused) =====");
                    return;
                }
                catch
                {
                    // Window is closed, clear the reference
                    System.Diagnostics.Debug.WriteLine("[ToolsMainPage] Previous window was closed, creating new one");
                    _historyWindowInstance = null;
                }
            }

            System.Diagnostics.Debug.WriteLine("[ToolsMainPage] Step 1: Creating HistoryWindow instance...");
            _historyWindowInstance = new Windows.HistoryWindow();
            System.Diagnostics.Debug.WriteLine("[ToolsMainPage] Step 1: SUCCESS - HistoryWindow created");
            
            // Subscribe to window close event to clear the reference
            _historyWindowInstance.Closed += (s, e) =>
            {
                System.Diagnostics.Debug.WriteLine("[ToolsMainPage] HistoryWindow closed, clearing reference");
                _historyWindowInstance = null;
            };
            
            System.Diagnostics.Debug.WriteLine("[ToolsMainPage] Step 2: Getting HistoryInstance from DatabaseManager...");
            var historyService = VetaleBrowser.Core.Scripts.GlobalManagers.DatabaseManager.HistoryInstance;
            System.Diagnostics.Debug.WriteLine($"[ToolsMainPage] Step 2: SUCCESS - HistoryService obtained: {historyService != null}");
            
            if (historyService == null)
            {
                System.Diagnostics.Debug.WriteLine("[ToolsMainPage] ERROR: HistoryService is null! Cannot open History window.");
                _historyWindowInstance = null;
                return;
            }
            
            System.Diagnostics.Debug.WriteLine("[ToolsMainPage] Step 3: Setting HistoryService on window...");
            _historyWindowInstance.SetHistoryService(historyService);
            System.Diagnostics.Debug.WriteLine("[ToolsMainPage] Step 3: SUCCESS - HistoryService set");
            
            System.Diagnostics.Debug.WriteLine("[ToolsMainPage] Step 4: Showing window...");
            _historyWindowInstance.Show();
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
            
            // Show error dialog to the user
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

    private void OpenDownloads()
    {
        System.Diagnostics.Debug.WriteLine("[ToolsMainPage] Opening Downloads...");
        
        try
        {
            // Check if a window is already open
            if (_downloadsWindowInstance != null)
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine("[ToolsMainPage] Reusing existing DownloadsWindow");
                    _downloadsWindowInstance.Activate();
                    _downloadsWindowInstance.WindowState = WindowState.Normal;
                    System.Diagnostics.Debug.WriteLine("[ToolsMainPage] Downloads window activated");
                    return;
                }
                catch
                {
                    // Window is closed, clear the reference
                    System.Diagnostics.Debug.WriteLine("[ToolsMainPage] Previous Downloads window was closed, creating new one");
                    _downloadsWindowInstance = null;
                }
            }

            System.Diagnostics.Debug.WriteLine("[ToolsMainPage] Creating new DownloadsWindow instance...");
            _downloadsWindowInstance = new Windows.DownloadsWindow();
            
            // Subscribe to window close event to clear the reference
            _downloadsWindowInstance.Closed += (s, e) =>
            {
                System.Diagnostics.Debug.WriteLine("[ToolsMainPage] DownloadsWindow closed, clearing reference");
                _downloadsWindowInstance = null;
            };
            
            System.Diagnostics.Debug.WriteLine("[ToolsMainPage] Showing Downloads window...");
            _downloadsWindowInstance.Show();
            System.Diagnostics.Debug.WriteLine("[ToolsMainPage] Downloads window opened successfully");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ToolsMainPage] ERROR opening Downloads: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"[ToolsMainPage] Stack trace: {ex.StackTrace}");
            _downloadsWindowInstance = null;
        }
    }

    private void OpenUserAgreement()
    {
        System.Diagnostics.Debug.WriteLine("[ToolsMainPage] Opening User Agreement...");
        
        try
        {
            // Check if a window is already open
            if (_userAgreementWindowInstance != null)
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine("[ToolsMainPage] Reusing existing UserAgreementWindow");
                    _userAgreementWindowInstance.Activate();
                    _userAgreementWindowInstance.WindowState = WindowState.Normal;
                    System.Diagnostics.Debug.WriteLine("[ToolsMainPage] UserAgreement window activated");
                    return;
                }
                catch
                {
                    // Window is closed, clear the reference
                    System.Diagnostics.Debug.WriteLine("[ToolsMainPage] Previous UserAgreement window was closed, creating new one");
                    _userAgreementWindowInstance = null;
                }
            }

            System.Diagnostics.Debug.WriteLine("[ToolsMainPage] Creating new UserAgreementWindow instance (read-only)...");
            _userAgreementWindowInstance = new Windows.UserAgreementWindow(readOnly: true);
            
            // Subscribe to window close event to clear the reference
            _userAgreementWindowInstance.Closed += (s, e) =>
            {
                System.Diagnostics.Debug.WriteLine("[ToolsMainPage] UserAgreementWindow closed, clearing reference");
                _userAgreementWindowInstance = null;
            };
            
            System.Diagnostics.Debug.WriteLine("[ToolsMainPage] Showing UserAgreement window...");
            _userAgreementWindowInstance.Show();
            System.Diagnostics.Debug.WriteLine("[ToolsMainPage] UserAgreement window opened successfully");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ToolsMainPage] ERROR opening UserAgreement: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"[ToolsMainPage] Stack trace: {ex.StackTrace}");
            _userAgreementWindowInstance = null;
        }
    }

    private void OpenAbout()
    {
        System.Diagnostics.Debug.WriteLine("[ToolsMainPage] Opening About...");
        
        try
        {
            // Check if a window is already open
            if (_aboutWindowInstance != null)
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine("[ToolsMainPage] Reusing existing AboutWindow");
                    _aboutWindowInstance.Activate();
                    _aboutWindowInstance.WindowState = WindowState.Normal;
                    System.Diagnostics.Debug.WriteLine("[ToolsMainPage] About window activated");
                    return;
                }
                catch
                {
                    // Window is closed, clear the reference
                    System.Diagnostics.Debug.WriteLine("[ToolsMainPage] Previous About window was closed, creating new one");
                    _aboutWindowInstance = null;
                }
            }

            System.Diagnostics.Debug.WriteLine("[ToolsMainPage] Creating new AboutWindow instance...");
            _aboutWindowInstance = new Windows.AboutWindow();
            
            // Subscribe to window close event to clear the reference
            _aboutWindowInstance.Closed += (s, e) =>
            {
                System.Diagnostics.Debug.WriteLine("[ToolsMainPage] AboutWindow closed, clearing reference");
                _aboutWindowInstance = null;
            };
            
            System.Diagnostics.Debug.WriteLine("[ToolsMainPage] Showing About window...");
            _aboutWindowInstance.Show();
            System.Diagnostics.Debug.WriteLine("[ToolsMainPage] About window opened successfully");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ToolsMainPage] ERROR opening About: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"[ToolsMainPage] Stack trace: {ex.StackTrace}");
            _aboutWindowInstance = null;
        }
    }

    private void OpenVetaleSearchSettings()
    {
        System.Diagnostics.Debug.WriteLine("[ToolsMainPage] Opening VetaleSearch Settings...");
        
        try
        {
            // Check if a window is already open
            if (_vetaleSearchSettingsWindowInstance != null)
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine("[ToolsMainPage] Reusing existing VetaleSearchSettingsWindow");
                    _vetaleSearchSettingsWindowInstance.Activate();
                    _vetaleSearchSettingsWindowInstance.WindowState = WindowState.Normal;
                    System.Diagnostics.Debug.WriteLine("[ToolsMainPage] VetaleSearchSettings window activated");
                    return;
                }
                catch
                {
                    // Window is closed, clear the reference
                    System.Diagnostics.Debug.WriteLine("[ToolsMainPage] Previous VetaleSearchSettings window was closed, creating new one");
                    _vetaleSearchSettingsWindowInstance = null;
                }
            }

            System.Diagnostics.Debug.WriteLine("[ToolsMainPage] Creating new VetaleSearchSettingsWindow instance...");
            _vetaleSearchSettingsWindowInstance = new Windows.VetaleSearchSettingsWindow();
            
            // Subscribe to window close event to clear the reference
            _vetaleSearchSettingsWindowInstance.Closed += (s, e) =>
            {
                System.Diagnostics.Debug.WriteLine("[ToolsMainPage] VetaleSearchSettingsWindow closed, clearing reference");
                _vetaleSearchSettingsWindowInstance = null;
            };
            
            System.Diagnostics.Debug.WriteLine("[ToolsMainPage] Showing VetaleSearchSettings window...");
            _vetaleSearchSettingsWindowInstance.Show();
            System.Diagnostics.Debug.WriteLine("[ToolsMainPage] VetaleSearchSettings window opened successfully");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ToolsMainPage] ERROR opening VetaleSearchSettings: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"[ToolsMainPage] Stack trace: {ex.StackTrace}");
            _vetaleSearchSettingsWindowInstance = null;
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

    private async void OpenAIComplaintWindow()
    {
        try
        {
            var owner = TopLevel.GetTopLevel(this) as Window;

            var w = new Windows.VetaleAIComplaintWindow
            {
                UserMessageContext = null,
                AssistantMessageContext = null
            };

            if (owner?.Icon != null)
                w.Icon = owner.Icon;

            if (owner != null)
                await w.ShowDialog(owner);
            else
                w.Show();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ToolsMainPage] ERROR opening AI complaint window: {ex}");
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
