using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using VetaleBrowser.VetaleBrowser.Core.Scripts.ErrorHandlers;

namespace VetaleBrowser.VetaleBrowser.UI.Pages
{
    /// <summary>
    /// Browser error page with localized messages
    /// Modern design in the style of Chrome/Firefox
    /// </summary>
    public partial class BrowserErrorPage : UserControl
    {
        private BrowserError? _error;
        
        /// <summary>
        /// Current error
        /// </summary>
        public BrowserError? Error => _error;
        
        // UI elements
        private Border? _errorIconCircle;
        private TextBlock? _errorIconText;
        private TextBlock? _errorTitleText;
        private TextBlock? _errorCategoryText;
        private TextBlock? _errorDescriptionText;
        private TextBlock? _failedUrlText;
        private TextBlock? _errorCodeText;
        private TextBlock? _timestampText;
        private TextBlock? _tipsHeaderText;
        private StackPanel? _tipsPanel;
        private Border? _searchSection;
        private TextBlock? _searchHeaderText;
        private TextBox? _searchTextBox;
        private TextBlock? _searchButtonText;
        private TextBlock? _retryButtonText;
        private TextBlock? _goBackButtonText;
        private Button? _retryButton;
        private Button? _playGameButton;
        private TextBlock? _playGameButtonText;
        private TextBlock? _gameDeveloperText;

        /// <summary>
        /// Event for requesting a retry of page loading
        /// </summary>
        public event EventHandler? RetryRequested;
        
        /// <summary>
        /// Event for requesting navigation back
        /// </summary>
        public event EventHandler? GoBackRequested;
        
        /// <summary>
        /// Event for requesting navigation home (kept for compatibility)
        /// </summary>
        public event EventHandler? GoHomeRequested;
        
        /// <summary>
        /// Event for requesting a search
        /// </summary>
        public event EventHandler<string>? SearchRequested;
        
        /// <summary>
        /// Event for requesting the HexGL game to be launched
        /// </summary>
        public event EventHandler? PlayGameRequested;

        public BrowserErrorPage()
        {
            InitializeComponent();
            FindControls();
            ApplyLocalization();
        }
        
        public BrowserErrorPage(BrowserError error) : this()
        {
            SetError(error);
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
        
        private void FindControls()
        {
            _errorIconCircle = this.FindControl<Border>("ErrorIconCircle");
            _errorIconText = this.FindControl<TextBlock>("ErrorIconText");
            _errorTitleText = this.FindControl<TextBlock>("ErrorTitleText");
            _errorCategoryText = this.FindControl<TextBlock>("ErrorCategoryText");
            _errorDescriptionText = this.FindControl<TextBlock>("ErrorDescriptionText");
            _failedUrlText = this.FindControl<TextBlock>("FailedUrlText");
            _errorCodeText = this.FindControl<TextBlock>("ErrorCodeText");
            _timestampText = this.FindControl<TextBlock>("TimestampText");
            _tipsHeaderText = this.FindControl<TextBlock>("TipsHeaderText");
            _tipsPanel = this.FindControl<StackPanel>("TipsPanel");
            _searchSection = this.FindControl<Border>("SearchSection");
            _searchHeaderText = this.FindControl<TextBlock>("SearchHeaderText");
            _searchTextBox = this.FindControl<TextBox>("SearchTextBox");
            _searchButtonText = this.FindControl<TextBlock>("SearchButtonText");
            _retryButtonText = this.FindControl<TextBlock>("RetryButtonText");
            _goBackButtonText = this.FindControl<TextBlock>("GoBackButtonText");
            _retryButton = this.FindControl<Button>("RetryButton");
            _playGameButton = this.FindControl<Button>("PlayGameButton");
            _playGameButtonText = this.FindControl<TextBlock>("PlayGameButtonText");
            _gameDeveloperText = this.FindControl<TextBlock>("GameDeveloperText");
        }
        
        /// <summary>
        /// Apply localization to static elements
        /// </summary>
        private void ApplyLocalization()
        {
            if (_tipsHeaderText != null)
                _tipsHeaderText.Text = GetLocalizedString("ErrorPage.TipsHeader", "💡 Що можна спробувати");
            if (_searchHeaderText != null)
                _searchHeaderText.Text = GetLocalizedString("ErrorPage.SearchHeader", "🔍 Пошук в інтернеті");
            if (_searchTextBox != null)
                _searchTextBox.Watermark = GetLocalizedString("ErrorPage.SearchWatermark", "Введіть пошуковий запит...");
            if (_searchButtonText != null)
                _searchButtonText.Text = GetLocalizedString("ErrorPage.Search", "Шукати");
            if (_retryButtonText != null)
                _retryButtonText.Text = GetLocalizedString("ErrorPage.Retry", "Спробувати знову");
            if (_goBackButtonText != null)
                _goBackButtonText.Text = GetLocalizedString("ErrorPage.GoBack", "Назад");
            if (_playGameButtonText != null)
                _playGameButtonText.Text = GetLocalizedString("ErrorPage.PlayGame", "Грати в гру");
            if (_gameDeveloperText != null)
                _gameDeveloperText.Text = GetLocalizedString("ErrorPage.GameDeveloper", "Гра HexGL від Thibaut Despoulain");
        }
        
        /// <summary>
        /// Get a localized string
        /// </summary>
        private string GetLocalizedString(string key, string defaultValue)
        {
            try
            {
                if (Application.Current?.TryFindResource(key, out var resource) == true && resource is string str)
                    return str;
            }
            catch { }
            return defaultValue;
        }

        /// <summary>
        /// Sets the error data and updates the UI
        /// </summary>
        public void SetError(BrowserError error)
        {
            _error = error ?? throw new ArgumentNullException(nameof(error));
            
            // Update the icon circle color according to category
            if (_errorIconCircle != null)
            {
                _errorIconCircle.Background = new SolidColorBrush(GetIconCircleColor(error.Category));
            }
            
            // Update icon and title
            if (_errorIconText != null)
                _errorIconText.Text = error.Icon;
            
            if (_errorTitleText != null)
                _errorTitleText.Text = error.Title;
            
            if (_errorCategoryText != null)
                _errorCategoryText.Text = GetCategoryDisplayName(error.Category);
            
            // Update description
            if (_errorDescriptionText != null)
                _errorDescriptionText.Text = error.Description;
            
            // Update URL
            if (_failedUrlText != null)
                _failedUrlText.Text = error.FailedUrl;
            
            // Update error code
            if (_errorCodeText != null)
                _errorCodeText.Text = $"{error.ErrorName} ({error.ErrorCode})";
            
            // Update timestamp
            if (_timestampText != null)
            {
                var timeLabel = GetLocalizedString("ErrorPage.Time", "Час");
                _timestampText.Text = $"{timeLabel}: {error.OccurredAt.ToLocalTime():HH:mm:ss dd.MM.yyyy}";
            }
            
            // Update tips
            UpdateTips(error.Tips);
            
            // Show/hide search section
            if (_searchSection != null)
                _searchSection.IsVisible = error.ShowSearch;
            
            // Show/hide retry button
            if (_retryButton != null)
                _retryButton.IsVisible = error.CanRetry;
            
            // Pre-fill search field with domain from URL
            if (_searchTextBox != null && error.ShowSearch)
            {
                try
                {
                    var uri = new Uri(error.FailedUrl);
                    _searchTextBox.Text = uri.Host;
                }
                catch
                {
                    _searchTextBox.Text = error.FailedUrl;
                }
            }
        }
        
        /// <summary>
        /// Gets the icon circle color by category
        /// </summary>
        private Color GetIconCircleColor(BrowserErrorCategory category)
        {
            return category switch
            {
                BrowserErrorCategory.NetworkError => Color.Parse("#FEF3C7"),    // Orange/yellow
                BrowserErrorCategory.ServerError => Color.Parse("#FEE2E2"),     // Red
                BrowserErrorCategory.SecurityError => Color.Parse("#FECACA"),   // Dark red
                BrowserErrorCategory.GeneralError => Color.Parse("#E2E8F0"),    // Gray
                _ => Color.Parse("#E2E8F0")
            };
        }
        
        /// <summary>
        /// Gets the category display name
        /// </summary>
        private string GetCategoryDisplayName(BrowserErrorCategory category)
        {
            return category switch
            {
                BrowserErrorCategory.NetworkError => GetLocalizedString("ErrorPage.Category.Network", "🌐 Мережева проблема"),
                BrowserErrorCategory.ServerError => GetLocalizedString("ErrorPage.Category.Server", "🖥️ Проблема сервера"),
                BrowserErrorCategory.SecurityError => GetLocalizedString("ErrorPage.Category.Security", "🔒 Проблема безпеки"),
                BrowserErrorCategory.GeneralError => GetLocalizedString("ErrorPage.Category.General", "⚠️ Загальна помилка"),
                _ => GetLocalizedString("ErrorPage.Category.Error", "Помилка")
            };
        }
        
        /// <summary>
        /// Updates the tips list
        /// </summary>
        private void UpdateTips(string[] tips)
        {
            if (_tipsPanel == null) return;
            
            _tipsPanel.Children.Clear();
            
            int index = 1;
            foreach (var tip in tips)
            {
                var tipBorder = new Border
                {
                    Background = new SolidColorBrush(Color.Parse("#F8FAFC")),
                    BorderBrush = new SolidColorBrush(Color.Parse("#E2E8F0")),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(10),
                    Padding = new Thickness(18, 14),
                    Margin = new Thickness(0, 0, 0, 10),
                    Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand)
                };
                
                // Hover effect
                tipBorder.PointerEntered += (s, e) =>
                {
                    tipBorder.Background = new SolidColorBrush(Color.Parse("#F1F5F9"));
                    tipBorder.BorderBrush = new SolidColorBrush(Color.Parse("#CBD5E1"));
                };
                tipBorder.PointerExited += (s, e) =>
                {
                    tipBorder.Background = new SolidColorBrush(Color.Parse("#F8FAFC"));
                    tipBorder.BorderBrush = new SolidColorBrush(Color.Parse("#E2E8F0"));
                };
                
                var tipPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 14
                };
                
                // Tip number in a circle
                var numberBorder = new Border
                {
                    Width = 28,
                    Height = 28,
                    CornerRadius = new CornerRadius(14),
                    Background = new SolidColorBrush(Color.Parse("#3B82F6")),
                    VerticalAlignment = VerticalAlignment.Top
                };
                
                var numberText = new TextBlock
                {
                    Text = index.ToString(),
                    FontWeight = FontWeight.SemiBold,
                    FontSize = 13,
                    Foreground = Brushes.White,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                numberBorder.Child = numberText;
                
                // Tip text
                var tipText = new TextBlock
                {
                    Text = tip,
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = new SolidColorBrush(Color.Parse("#475569")),
                    VerticalAlignment = VerticalAlignment.Center,
                    FontSize = 14,
                    LineHeight = 22
                };
                
                tipPanel.Children.Add(numberBorder);
                tipPanel.Children.Add(tipText);
                tipBorder.Child = tipPanel;
                
                _tipsPanel.Children.Add(tipBorder);
                index++;
            }
        }
        
        /// <summary>
        /// Handler for the "Retry" button
        /// </summary>
        private void OnRetryClicked(object? sender, RoutedEventArgs e)
        {
            RetryRequested?.Invoke(this, EventArgs.Empty);
        }
        
        /// <summary>
        /// Handler for the "Go Back" button
        /// </summary>
        private void OnGoBackClicked(object? sender, RoutedEventArgs e)
        {
            GoBackRequested?.Invoke(this, EventArgs.Empty);
        }
        
        /// <summary>
        /// Handler for the "Search" button
        /// </summary>
        private void OnSearchClicked(object? sender, RoutedEventArgs e)
        {
            var query = _searchTextBox?.Text?.Trim();
            if (!string.IsNullOrEmpty(query))
            {
                SearchRequested?.Invoke(this, query);
            }
        }
        
        /// <summary>
        /// Handler for the "Play Game" button
        /// </summary>
        private void OnPlayGameClicked(object? sender, RoutedEventArgs e)
        {
            PlayGameRequested?.Invoke(this, EventArgs.Empty);
        }
        
        /// <summary>
        /// Static method for quickly creating an error page
        /// </summary>
        public static BrowserErrorPage CreateForCefError(int errorCode, string failedUrl, string? errorText = null)
        {
            var error = BrowserErrorService.GetLocalizedError(errorCode, failedUrl, errorText);
            return new BrowserErrorPage(error);
        }
        
        /// <summary>
        /// Static method for creating an HTTP error page
        /// </summary>
        public static BrowserErrorPage CreateForHttpError(int httpStatusCode, string failedUrl)
        {
            var error = BrowserErrorService.GetHttpError(httpStatusCode, failedUrl);
            return new BrowserErrorPage(error);
        }
    }
}

