using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;
using VetaleBrowser.VetaleBrowser.Database.Models;
using VetaleBrowser.VetaleBrowser.Database.Services;
using Avalonia.Platform.Storage;

namespace VetaleBrowser.VetaleBrowser.UI.Windows;

public partial class ConsoleWindow : Window
{
    private ConsoleDatabaseService? _consoleService;
    private ItemsControl? _logItemsControl;
    private ScrollViewer? _logScrollViewer;
    private Panel? _emptyStatePanel;
    private TextBox? _searchTextBox;
    private ComboBox? _levelFilterComboBox;
    private CheckBox? _autoRefreshCheckBox;
    
    private readonly ObservableCollection<ConsoleLogViewModel> _logItems = new();
    private Timer? _autoRefreshTimer;
    private bool _isAutoScrollEnabled = true;
    
    private bool _isMaximized;

    // Прапорець для одноразового банера ініціалізації при відкритті консолі
    private bool _initBannerAdded;

    public ConsoleWindow()
    {
        InitializeComponent();
        InitializeControls();
        
        this.Opened += OnWindowOpened;
        this.Closing += OnWindowClosing;
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void InitializeControls()
    {
        _logItemsControl = this.FindControl<ItemsControl>("LogItemsControl");
        _logScrollViewer = this.FindControl<ScrollViewer>("LogScrollViewer");
        _emptyStatePanel = this.FindControl<Panel>("EmptyStatePanel");
        _searchTextBox = this.FindControl<TextBox>("SearchTextBox");
        _levelFilterComboBox = this.FindControl<ComboBox>("LevelFilterComboBox");
        _autoRefreshCheckBox = this.FindControl<CheckBox>("AutoRefreshCheckBox");

        if (_logItemsControl != null)
        {
            _logItemsControl.ItemsSource = _logItems;
        }

        if (_logScrollViewer != null)
        {
            _logScrollViewer.ScrollChanged += (_, e) =>
            {
                // Disable auto-scroll if user scrolls up
                if (e.OffsetDelta.Y < 0)
                {
                    _isAutoScrollEnabled = false;
                }
            };
        }
    }

    public void SetConsoleService(ConsoleDatabaseService? service)
    {
        _consoleService = service;
        LoadLogs();
    }

    private void OnWindowOpened(object? sender, EventArgs e)
    {
        // Додаємо одноразовий банер ініціалізації та виконуємо перше завантаження
        _initBannerAdded = false; // гарантуємо додавання саме при відкритті
        LoadLogs(addInitBannerOnce: true);

        // Start auto-refresh timer
        if (_autoRefreshCheckBox?.IsChecked == true)
        {
            StartAutoRefresh();
        }
    }

    private void OnWindowClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        StopAutoRefresh();
    }

    private void StartAutoRefresh()
    {
        StopAutoRefresh();
        _autoRefreshTimer = new Timer(_ =>
        {
            Dispatcher.UIThread.Post(() => LoadLogs(true));
        }, null, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2));
    }

    private void StopAutoRefresh()
    {
        _autoRefreshTimer?.Dispose();
        _autoRefreshTimer = null;
    }

    private void LoadLogs(bool isAutoRefresh = false, bool addInitBannerOnce = false)
    {
        if (_consoleService == null) return;

        try
        {
            var searchQuery = _searchTextBox?.Text;
            var selectedLevel = GetSelectedLevel();
            
            List<ConsoleLogItem> logs;
            
            if (!string.IsNullOrWhiteSpace(searchQuery))
            {
                logs = _consoleService.SearchLogs(searchQuery);
            }
            else
            {
                logs = _consoleService.GetLogs(level: selectedLevel);
            }

            var viewModels = logs.Select(log => new ConsoleLogViewModel
            {
                Level = log.Level,
                Message = log.Message,
                Source = log.Source,
                Timestamp = log.Timestamp
            }).ToList();

            _logItems.Clear();

            // Одноразовий банер ініціалізації (локалізований)
            if (addInitBannerOnce && !_initBannerAdded)
            {
                var initMsg = TryGetString("Console.InitBanner", "Console initialized");
                _logItems.Add(new ConsoleLogViewModel
                {
                    Level = "Info",
                    Source = "Console",
                    Message = initMsg,
                    Timestamp = DateTime.Now
                });
                _initBannerAdded = true;
            }

            foreach (var vm in viewModels)
            {
                _logItems.Add(vm);
            }

            UpdateEmptyState();

            // Auto-scroll to bottom if enabled or if it's auto-refresh
            if ((isAutoRefresh || addInitBannerOnce) && _isAutoScrollEnabled && _logScrollViewer != null)
            {
                _logScrollViewer.ScrollToEnd();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"[ConsoleWindow] Error loading logs: {ex.Message}");
        }
    }

    private string? GetSelectedLevel()
    {
        if (_levelFilterComboBox == null) return null;
        
        var selectedItem = _levelFilterComboBox.SelectedItem as ComboBoxItem;
        if (selectedItem == null) return null;
        
        var content = selectedItem.Content?.ToString();
        var all = TryGetString("Console.Level.All", "All levels");
        return content == all ? null : content;
    }

    private void UpdateEmptyState()
    {
        if (_emptyStatePanel != null)
        {
            _emptyStatePanel.IsVisible = _logItems.Count == 0;
        }
    }

    private void OnSearchKeyUp(object? sender, KeyEventArgs e)
    {
        LoadLogs();
        _isAutoScrollEnabled = true;
    }

    private void OnLevelFilterChanged(object? sender, SelectionChangedEventArgs e)
    {
        LoadLogs();
        _isAutoScrollEnabled = true;
    }

    private void OnAutoRefreshChanged(object? sender, RoutedEventArgs e)
    {
        if (_autoRefreshCheckBox?.IsChecked == true)
        {
            StartAutoRefresh();
        }
        else
        {
            StopAutoRefresh();
        }
    }

    private void OnRefreshClick(object? sender, RoutedEventArgs e)
    {
        LoadLogs();
        _isAutoScrollEnabled = true;
        if (_logScrollViewer != null)
        {
            _logScrollViewer.ScrollToEnd();
        }
    }

    private async void OnClearClick(object? sender, RoutedEventArgs e)
    {
        if (_consoleService == null) return;

        // Show confirmation dialog
        var title = TryGetString("Common.ClearAllLogs", "Clear all logs");
        var message = TryGetString("Console.ClearConfirm", "This will delete all console entries. Continue?");
        var result = await ShowConfirmationDialog(title, message);
        
        if (result)
        {
            _consoleService.ClearLogs();
            LoadLogs();
        }
    }

    private async void OnExportClick(object? sender, RoutedEventArgs e)
    {
        if (_consoleService == null) return;

        try
        {
            var exportTitle = TryGetString("Console.ExportLogs", "Export logs");
            var suggestedNamePrefix = TryGetString("Console.ExportLogs.FilePrefix", "console_logs_");
            var result = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = exportTitle,
                DefaultExtension = "txt",
                SuggestedFileName = $"{suggestedNamePrefix}{DateTime.Now:yyyyMMdd_HHmmss}.txt"
            });

            if (result != null)
            {
                var filePath = result.Path.LocalPath;
                ExportLogsToFile(filePath);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"[ConsoleWindow] Error exporting logs: {ex.Message}");
        }
    }

    private void ExportLogsToFile(string filePath)
    {
        if (_consoleService == null) return;

        try
        {
            var logs = _consoleService.GetLogs();
            var sb = new StringBuilder();
            
            sb.AppendLine("=== Vetale Browser Console Logs ===");
            sb.AppendLine($"Exported: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"Total logs: {logs.Count}");
            sb.AppendLine();
            
            foreach (var log in logs)
            {
                sb.AppendLine($"[{log.Timestamp:yyyy-MM-dd HH:mm:ss}] [{log.Level}] {log.Source ?? "Unknown"}");
                sb.AppendLine($"  {log.Message}");
                if (!string.IsNullOrEmpty(log.StackTrace))
                {
                    sb.AppendLine($"  Stack: {log.StackTrace}");
                }
                sb.AppendLine();
            }
            
            File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
            
            System.Diagnostics.Trace.WriteLine($"[ConsoleWindow] Logs exported to: {filePath}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"[ConsoleWindow] Error writing export file: {ex.Message}");
        }
    }

    private async System.Threading.Tasks.Task<bool> ShowConfirmationDialog(string title, string message)
    {
        var cancelText = TryGetString("Common.Cancel", "Cancel");
        var okText = TryGetString("Common.Yes", "Yes");

        var dialog = new Window
        {
            Title = title,
            Width = 400,
            Height = 180,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            Content = new StackPanel
            {
                Margin = new Thickness(20),
                Spacing = 20,
                Children =
                {
                    new TextBlock
                    {
                        Text = message,
                        FontSize = 14,
                        TextWrapping = TextWrapping.Wrap
                    },
                    new StackPanel
                    {
                        Orientation = Avalonia.Layout.Orientation.Horizontal,
                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                        Spacing = 10,
                        Children =
                        {
                            new Button
                            {
                                Content = cancelText,
                                Width = 100,
                                Height = 32,
                                Tag = false
                            },
                            new Button
                            {
                                Content = okText,
                                Width = 100,
                                Height = 32,
                                Tag = true
                            }
                        }
                    }
                }
            }
        };

        bool result = false;
        foreach (var child in ((StackPanel)dialog.Content).Children)
        {
            if (child is StackPanel sp)
            {
                foreach (var btn in sp.Children.OfType<Button>())
                {
                    btn.Click += (_, _) =>
                    {
                        result = (bool)(btn.Tag ?? false);
                        dialog.Close();
                    };
                }
            }
        }

        await dialog.ShowDialog(this);
        return result;
    }

    private static string TryGetString(string key, string fallback)
    {
        try
        {
            var app = Application.Current;
            if (app != null && app.TryFindResource(key, out var value) && value is string s)
                return s;
        }
        catch { }
        return fallback;
    }

    // Window controls
    private void OpenMainWindow(object? sender, RoutedEventArgs e)
    {
        try
        {
            var mainWindow = Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
                ? desktop.MainWindow
                : null;
            mainWindow?.Activate();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"[ConsoleWindow] Error activating main window: {ex.Message}");
        }
    }

    private void MinimizeWindow(object? sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void MaximizeWindow(object? sender, RoutedEventArgs e)
    {
        if (_isMaximized)
        {
            WindowState = WindowState.Normal;
            _isMaximized = false;
        }
        else
        {
            WindowState = WindowState.Maximized;
            _isMaximized = true;
        }
    }

    private void CloseWindow(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private void TopBar_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginMoveDrag(e);
        }
    }

    private void TopBar_DoubleTapped(object? sender, RoutedEventArgs e)
    {
        MaximizeWindow(sender, e);
    }

    // Helper: build a line string from a VM
    private static string FormatLogLine(ConsoleLogViewModel vm)
        => $"[{vm.Timestamp:yyyy-MM-dd HH:mm:ss.fff}] [{vm.Level}] {(string.IsNullOrEmpty(vm.Source) ? "[System]" : "[" + vm.Source + "]")} {vm.Message}";

    private async void OnCopyMessageClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem mi) return;
        if (mi.TryGetDataContext(out ConsoleLogViewModel? vm) && vm != null)
        {
            await SetClipboardTextAsync(vm.Message);
        }
        else
        {
            var placementTarget = (mi.Parent as ContextMenu)?.PlacementTarget as Control;
            var ctxVm = placementTarget?.DataContext as ConsoleLogViewModel;
            if (ctxVm != null)
            {
                await SetClipboardTextAsync(ctxVm.Message);
            }
        }
    }

    private async void OnCopyRowClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem mi) return;
        if (mi.TryGetDataContext(out ConsoleLogViewModel? vm) && vm != null)
        {
            await SetClipboardTextAsync(FormatLogLine(vm));
        }
        else
        {
            var placementTarget = (mi.Parent as ContextMenu)?.PlacementTarget as Control;
            var ctxVm = placementTarget?.DataContext as ConsoleLogViewModel;
            if (ctxVm != null)
            {
                await SetClipboardTextAsync(FormatLogLine(ctxVm));
            }
        }
    }

    private async void OnCopyAllClick(object? sender, RoutedEventArgs e)
    {
        if (_logItems.Count == 0) return;
        var sb = new StringBuilder(_logItems.Count * 64);
        foreach (var vm in _logItems)
        {
            sb.AppendLine(FormatLogLine(vm));
        }
        await SetClipboardTextAsync(sb.ToString());
    }

    private async System.Threading.Tasks.Task SetClipboardTextAsync(string text)
    {
        try
        {
            if (string.IsNullOrEmpty(text)) return;
            var clipboard = this.Clipboard; // TopLevel clipboard
            if (clipboard != null)
            {
                await clipboard.SetTextAsync(text);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"[ConsoleWindow] Clipboard error: {ex.Message}");
        }
    }
}

public static class MenuItemDataContextExtensions
{
    public static bool TryGetDataContext<T>(this MenuItem mi, out T? value)
    {
        value = default;
        try
        {
            var placementTarget = (mi.Parent as ContextMenu)?.PlacementTarget as Control;
            if (placementTarget?.DataContext is T t)
            {
                value = t;
                return true;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"[ConsoleWindow] TryGetDataContext error: {ex.Message}");
        }
        return false;
    }
}

/// <summary>
/// ViewModel for console log items
/// </summary>
public class ConsoleLogViewModel
{
    public string Level { get; set; } = "Info";
    public string Message { get; set; } = string.Empty;
    public string? Source { get; set; }
    public DateTime Timestamp { get; set; }

    public string TimestampFormatted => Timestamp.ToLocalTime().ToString("HH:mm:ss.fff");
    
    public string SourceFormatted => string.IsNullOrEmpty(Source) ? "[System]" : $"[{Source}]";

    public IBrush LevelColor => Level switch
    {
        "Info" => new SolidColorBrush(Color.Parse("#4CAF50")),
        "Warning" => new SolidColorBrush(Color.Parse("#FF9800")),
        "Error" => new SolidColorBrush(Color.Parse("#F44336")),
        "Debug" => new SolidColorBrush(Color.Parse("#2196F3")),
        _ => new SolidColorBrush(Color.Parse("#808080"))
    };

    public IBrush MessageColor => Level switch
    {
        "Error" => new SolidColorBrush(Color.Parse("#FF6B6B")),
        "Warning" => new SolidColorBrush(Color.Parse("#FFB86C")),
        _ => new SolidColorBrush(Color.Parse("#D4D4D4"))
    };
}
