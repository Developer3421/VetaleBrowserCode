using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using VetaleBrowser.VetaleBrowser.Database.Models;
using VetaleBrowser.VetaleBrowser.Database.Services;

namespace VetaleBrowser.VetaleBrowser.UI.Pages;

public partial class HistoryPage : UserControl
{
    private StackPanel? _historyStackPanel;
    private TextBlock? _totalSessionsText;
    private TextBlock? _totalTabsText;
    private TextBlock? _databaseSizeText;
    private TextBlock? _usagePercentText;
    private ITabDatabaseService? _databaseService;

    public HistoryPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        _historyStackPanel = this.FindControl<StackPanel>("PART_HistoryStackPanel");
        _totalSessionsText = this.FindControl<TextBlock>("PART_TotalSessionsText");
        _totalTabsText = this.FindControl<TextBlock>("PART_TotalTabsText");
        _databaseSizeText = this.FindControl<TextBlock>("PART_DatabaseSizeText");
        _usagePercentText = this.FindControl<TextBlock>("PART_UsagePercentText");
        
        LoadHistory();
    }

    public void SetDatabaseService(ITabDatabaseService databaseService)
    {
        _databaseService = databaseService;
        LoadHistory();
    }

    private void LoadHistory()
    {
        if (_historyStackPanel == null || _databaseService == null)
            return;

        try
        {
            // Load statistics
            var stats = _databaseService.GetStats();
            UpdateStatistics(stats);

            // Clear existing items
            _historyStackPanel.Children.Clear();

            // For demo purposes, show current session
            var currentSession = _databaseService.GetCurrentSession();
            if (currentSession != null)
            {
                AddSessionToUi(currentSession);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading history: {ex.Message}");
        }
    }

    private void UpdateStatistics(DatabaseStats stats)
    {
        if (_totalSessionsText != null)
            _totalSessionsText.Text = stats.TotalSessions.ToString();

        if (_totalTabsText != null)
            _totalTabsText.Text = stats.TotalTabs.ToString();

        if (_databaseSizeText != null)
        {
            var sizeMb = stats.DatabaseSizeBytes / (1024.0 * 1024.0);
            _databaseSizeText.Text = $"{sizeMb:F2} MB";
        }

        if (_usagePercentText != null)
            _usagePercentText.Text = $"{stats.UsagePercentage:F1}%";
    }

    private void AddSessionToUi(BrowserSession session)
    {
        if (_historyStackPanel == null || _databaseService == null)
            return;

        // Session header
        var sessionHeader = new Border
        {
            Background = Brush.Parse("#9A1CE8"),
            CornerRadius = new Avalonia.CornerRadius(4),
            Padding = new Avalonia.Thickness(15),
            Margin = new Avalonia.Thickness(0, 20, 0, 10)
        };

        var headerGrid = new Grid();
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var sessionInfo = new StackPanel();
        var titleText = new TextBlock
        {
            Text = session.IsCurrent ? "Поточна сесія" : $"Сесія від {session.StartedAt:dd.MM.yyyy HH:mm}",
            FontSize = 16,
            FontWeight = FontWeight.SemiBold,
            Foreground = Brushes.White
        };
        var countText = new TextBlock
        {
            Text = $"{session.TabCount} вкладок",
            FontSize = 12,
            Foreground = Brushes.White,
            Opacity = 0.9,
            Margin = new Avalonia.Thickness(0, 4, 0, 0)
        };
        sessionInfo.Children.Add(titleText);
        sessionInfo.Children.Add(countText);

        var deleteButton = new Button
        {
            Content = "🗑️ Видалити",
            Background = Brushes.Transparent,
            Foreground = Brushes.White,
            BorderThickness = new Avalonia.Thickness(1),
            BorderBrush = Brushes.White,
            Padding = new Avalonia.Thickness(10, 5),
            CornerRadius = new Avalonia.CornerRadius(4),
            Tag = session.Id
        };
        deleteButton.Click += DeleteSession_Click;

        Grid.SetColumn(sessionInfo, 0);
        Grid.SetColumn(deleteButton, 1);
        headerGrid.Children.Add(sessionInfo);
        headerGrid.Children.Add(deleteButton);
        sessionHeader.Child = headerGrid;

        _historyStackPanel.Children.Add(sessionHeader);

        // Load tabs for this session
        var tabs = _databaseService.GetSessionTabs(session.Id);
        foreach (var tab in tabs)
        {
            AddTabToUi(tab);
        }
    }

    private void AddTabToUi(TabModel tab)
    {
        if (_historyStackPanel == null)
            return;

        var tabBorder = new Border
        {
            Background = Brush.Parse("#F8F8F8"),
            BorderBrush = Brush.Parse("#E0E0E0"),
            BorderThickness = new Avalonia.Thickness(1),
            CornerRadius = new Avalonia.CornerRadius(4),
            Padding = new Avalonia.Thickness(15),
            Margin = new Avalonia.Thickness(0, 0, 0, 10)
        };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        // Icon
        var iconBorder = new Border
        {
            Width = 32,
            Height = 32,
            Background = Brush.Parse("#9A1CE8"),
            CornerRadius = new Avalonia.CornerRadius(4),
            Margin = new Avalonia.Thickness(0, 0, 15, 0)
        };
        var icon = new TextBlock
        {
            Text = "🌐",
            FontSize = 18,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
        };
        iconBorder.Child = icon;

        // Info
        var infoPanel = new StackPanel { VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
        var titleText = new TextBlock
        {
            Text = string.IsNullOrEmpty(tab.Title) ? "Без назви" : tab.Title,
            FontSize = 14,
            FontWeight = FontWeight.SemiBold,
            Foreground = Brush.Parse("#202020"),
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        var urlText = new TextBlock
        {
            Text = tab.Url,
            FontSize = 12,
            Foreground = Brush.Parse("#606060"),
            TextTrimming = TextTrimming.CharacterEllipsis,
            Margin = new Avalonia.Thickness(0, 4, 0, 0)
        };
        var timeText = new TextBlock
        {
            Text = $"Відкрито: {tab.LastAccessedAt:dd.MM.yyyy HH:mm}",
            FontSize = 11,
            Foreground = Brush.Parse("#9A1CE8"),
            Margin = new Avalonia.Thickness(0, 4, 0, 0)
        };
        infoPanel.Children.Add(titleText);
        infoPanel.Children.Add(urlText);
        infoPanel.Children.Add(timeText);

        // Actions
        var actionsPanel = new StackPanel 
        { 
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
        };
        
        var openButton = new Button
        {
            Content = "🌐",
            FontSize = 18,
            Background = Brushes.Transparent,
            BorderThickness = new Avalonia.Thickness(0),
            Padding = new Avalonia.Thickness(8),
            Tag = tab
        };
        openButton.Click += OpenTab_Click;
        
        var deleteButton = new Button
        {
            Content = "🗑️",
            FontSize = 18,
            Background = Brushes.Transparent,
            BorderThickness = new Avalonia.Thickness(0),
            Padding = new Avalonia.Thickness(8),
            Tag = tab
        };
        deleteButton.Click += DeleteTab_Click;

        actionsPanel.Children.Add(openButton);
        actionsPanel.Children.Add(deleteButton);

        Grid.SetColumn(iconBorder, 0);
        Grid.SetColumn(infoPanel, 1);
        Grid.SetColumn(actionsPanel, 2);
        grid.Children.Add(iconBorder);
        grid.Children.Add(infoPanel);
        grid.Children.Add(actionsPanel);

        tabBorder.Child = grid;
        _historyStackPanel.Children.Add(tabBorder);
    }

    private void OpenTab_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is TabModel tab)
        {
            // TODO: Navigate to URL
            Console.WriteLine($"Opening tab: {tab.Url}");
        }
    }

    private void DeleteTab_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is TabModel tab && _databaseService != null)
        {
            try
            {
                _databaseService.DeleteTab(tab.Id);
                LoadHistory();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deleting tab: {ex.Message}");
            }
        }
    }

    private void DeleteSession_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is int sessionId && _databaseService != null)
        {
            try
            {
                _databaseService.DeleteSession(sessionId);
                LoadHistory();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deleting session: {ex.Message}");
            }
        }
    }

    private void ClearHistory_Click(object? sender, RoutedEventArgs e)
    {
        // TODO: Show confirmation dialog
        Console.WriteLine("Clear history requested");
    }

    private void Refresh_Click(object? sender, RoutedEventArgs e)
    {
        LoadHistory();
    }
}

