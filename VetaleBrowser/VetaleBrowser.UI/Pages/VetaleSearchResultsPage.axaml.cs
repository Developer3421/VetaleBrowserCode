using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace VetaleBrowser.VetaleBrowser.UI.Pages;

public partial class VetaleSearchResultsPage : UserControl
{
    private string? _currentQuery;
    public event EventHandler<string>? NavigateRequested;
    
    private TextBlock? _searchStats;
    private StackPanel? _resultsPanel;
    private TextBlock? _queryHeading;
    private TextBox? _searchInput;
    private ComboBox? _searchEngineSelector;
    private Button? _searchButton;

    public VetaleSearchResultsPage()
    {
        InitializeComponent();
        InitializeControls();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void InitializeControls()
    {
        _searchStats = this.FindControl<TextBlock>("SearchStats");
        _resultsPanel = this.FindControl<StackPanel>("ResultsPanel");
        _queryHeading = this.FindControl<TextBlock>("QueryHeading");
        _searchInput = this.FindControl<TextBox>("SearchInput");
        _searchEngineSelector = this.FindControl<ComboBox>("SearchEngineSelector");
        _searchButton = this.FindControl<Button>("SearchButton");

        if (_searchInput != null)
        {
            _searchInput.Focus();
        }

        if (_searchButton != null)
        {
            _searchButton.IsEnabled = !string.IsNullOrWhiteSpace(_searchInput?.Text);
        }
    }

    /// <summary>
    /// Встановити пошуковий запит та завантажити результати
    /// </summary>
    public void SetSearchQuery(string query)
    {
        System.Diagnostics.Debug.WriteLine($"[VetaleSearchResultsPage] SetSearchQuery called with: '{query}'");
        _currentQuery = query;

        if (_searchInput != null)
        {
            _searchInput.Text = query;
        }

        if (_queryHeading != null)
        {
            _queryHeading.Text = string.IsNullOrWhiteSpace(query)
                ? "Локальний пошук Vetale"
                : $"Результати для: \"{query}\"";
        }

        LoadSearchResults(query);
    }

    /// <summary>
    /// Оновити статистику пошуку
    /// </summary>
    public void UpdateSearchStats(int totalResults, double searchTime)
    {
        if (_searchStats != null)
        {
            _searchStats.Text = $"Приблизно {totalResults:N0} результатів ({searchTime:F2} секунди)";
        }
    }

    /// <summary>
    /// Завантажити результати пошуку
    /// </summary>
    private void LoadSearchResults(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return;

        System.Diagnostics.Debug.WriteLine($"[VetaleSearchResultsPage] Loading results for: {query}");

        // TODO: інтеграція з реальним пошуком

        if (_searchStats != null)
        {
            _searchStats.Text = $"Показуємо попередні результати для запиту \"{query}\"";
        }
    }

    // --- Обробники подій пошуку (як на домашній сторінці) ---

    private void SearchInput_TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_searchButton != null)
        {
            _searchButton.IsEnabled = !string.IsNullOrWhiteSpace(_searchInput?.Text);
        }
    }

    private void SearchInput_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            PerformSearch();
        }
    }

    private void Search_Click(object? sender, RoutedEventArgs e)
    {
        PerformSearch();
    }

    private void LuckySearch_Click(object? sender, RoutedEventArgs e)
    {
        PerformSearch(isLucky: true);
    }

    private void SearchEngineSelector_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        // Поки що просто лог для діагностики
        System.Diagnostics.Debug.WriteLine($"[VetaleSearchResultsPage] Search engine changed to index: {_searchEngineSelector?.SelectedIndex}");
    }

    private void PerformSearch(bool isLucky = false)
    {
        if (_searchInput == null || string.IsNullOrWhiteSpace(_searchInput.Text))
        {
            return;
        }

        string query = _searchInput.Text.Trim();
        _currentQuery = query;

        // Для локального пошуку просто оновлюємо результати на цій сторінці
        LoadSearchResults(query);
    }

    /// <summary>
    /// Обробка кліку по заголовку результату
    /// </summary>
    private void ResultTitle_Click(object? sender, PointerPressedEventArgs e)
    {
        if (sender is TextBlock titleBlock)
        {
            // Отримати URL з батьківського Border
            var border = FindParentBorder(titleBlock);
            if (border?.DataContext is SearchResult result)
            {
                System.Diagnostics.Debug.WriteLine($"[VetaleSearchResultsPage] Opening result: {result.Url}");
                NavigateRequested?.Invoke(this, result.Url);
            }
            else
            {
                // Для статичних результатів з XAML - витягуємо URL з breadcrumb
                var stackPanel = titleBlock.Parent as StackPanel;
                if (stackPanel != null)
                {
                    var breadcrumbPanel = FindBreadcrumbPanel(stackPanel);
                    if (breadcrumbPanel != null)
                    {
                        var urlText = ExtractUrlFromBreadcrumb(breadcrumbPanel);
                        if (!string.IsNullOrEmpty(urlText))
                        {
                            System.Diagnostics.Debug.WriteLine($"[VetaleSearchResultsPage] Opening static result: {urlText}");
                            NavigateRequested?.Invoke(this, urlText);
                        }
                    }
                }
            }
        }
    }

    private Border? FindParentBorder(Control control)
    {
        var parent = control.Parent;
        while (parent != null)
        {
            if (parent is Border border)
                return border;
            parent = parent.Parent;
        }
        return null;
    }

    private StackPanel? FindBreadcrumbPanel(StackPanel parentPanel)
    {
        foreach (var child in parentPanel.Children)
        {
            if (child is StackPanel sp && sp.Orientation == Avalonia.Layout.Orientation.Horizontal)
            {
                // Перевіряємо, чи це панель breadcrumb (містить TextBlock з класом breadcrumb-text)
                foreach (var grandChild in sp.Children)
                {
                    if (grandChild is TextBlock tb && tb.Classes.Contains("breadcrumb-text"))
                    {
                        return sp;
                    }
                }
            }
        }
        return null;
    }

    private string? ExtractUrlFromBreadcrumb(StackPanel breadcrumbPanel)
    {
        var urlParts = new List<string>();
        foreach (var child in breadcrumbPanel.Children)
        {
            if (child is TextBlock tb && tb.Classes.Contains("breadcrumb-text"))
            {
                urlParts.Add(tb.Text ?? "");
            }
        }

        if (urlParts.Count > 0)
        {
            // Перша частина - це домен
            var domain = urlParts[0];
            if (!domain.StartsWith("http"))
            {
                domain = "https://" + domain;
            }
            return domain;
        }

        return null;
    }

    /// <summary>
    /// Додати результат пошуку програмно
    /// </summary>
    public void AddSearchResult(SearchResult result)
    {
        if (_resultsPanel == null)
            return;

        var resultBorder = new Border
        {
            Classes = { "result-item" }
        };

        var stackPanel = new StackPanel();

        // Breadcrumb / URL
        var breadcrumb = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            Margin = new Avalonia.Thickness(0, 0, 0, 4)
        };

        var urlParts = result.DisplayUrl.Split(new[] { " › ", "›" }, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < urlParts.Length; i++)
        {
            breadcrumb.Children.Add(new TextBlock
            {
                Classes = { "breadcrumb-text" },
                Text = urlParts[i].Trim()
            });

            if (i < urlParts.Length - 1)
            {
                breadcrumb.Children.Add(new TextBlock
                {
                    Classes = { "breadcrumb-sep" },
                    Text = "›"
                });
            }
        }
        stackPanel.Children.Add(breadcrumb);

        // Title
        var titleBlock = new TextBlock
        {
            Classes = { "result-title" },
            Text = result.Title
        };
        titleBlock.PointerPressed += ResultTitle_Click;
        stackPanel.Children.Add(titleBlock);

        // Date (optional)
        if (result.Date.HasValue)
        {
            stackPanel.Children.Add(new TextBlock
            {
                Classes = { "result-date" },
                Text = result.Date.Value.ToString("d MMM yyyy")
            });
        }

        // Rating (optional)
        if (result.Rating.HasValue && result.ReviewCount.HasValue)
        {
            var ratingPanel = new StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Horizontal,
                Margin = new Avalonia.Thickness(0, 4)
            };

            // Stars
            int fullStars = (int)Math.Floor(result.Rating.Value);
            for (int i = 0; i < fullStars; i++)
            {
                ratingPanel.Children.Add(new TextBlock
                {
                    Classes = { "star" },
                    Text = "★"
                });
            }

            // Rating text
            ratingPanel.Children.Add(new TextBlock
            {
                Classes = { "rating-count" },
                Text = $"{result.Rating.Value:F1} · {result.ReviewCount.Value} відгуків"
            });

            stackPanel.Children.Add(ratingPanel);
        }

        // Description
        var descBlock = new TextBlock
        {
            Classes = { "result-description" },
            Text = result.Description
        };
        stackPanel.Children.Add(descBlock);

        resultBorder.Child = stackPanel;
        resultBorder.DataContext = result;

        _resultsPanel.Children.Add(resultBorder);
    }

    /// <summary>
    /// Очистити всі результати
    /// </summary>
    public void ClearResults()
    {
        _resultsPanel?.Children.Clear();
        if (_searchStats != null)
        {
            _searchStats.Text = "Немає результатів";
        }
    }
}

/// <summary>
/// Мо��ель результату пошуку
/// </summary>
public class SearchResult
{
    public string Url { get; set; } = string.Empty;
    public string DisplayUrl { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime? Date { get; set; }
    public double? Rating { get; set; }
    public int? ReviewCount { get; set; }
}
