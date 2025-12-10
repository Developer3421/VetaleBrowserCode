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
    /// Сторінка помилки браузера з локалізованими повідомленнями
    /// Стилізована відповідно до ToolsMainPage для єдиного фірмового стилю
    /// </summary>
    public partial class BrowserErrorPage : UserControl
    {
        private BrowserError? _error;
        
        // Елементи UI
        private TextBlock? _errorIconText;
        private TextBlock? _errorTitleText;
        private TextBlock? _errorCategoryText;
        private TextBlock? _errorDescriptionText;
        private TextBlock? _failedUrlText;
        private TextBlock? _errorCodeText;
        private TextBlock? _timestampText;
        private StackPanel? _tipsPanel;
        private Border? _searchSection;
        private TextBox? _searchTextBox;
        private Button? _retryButton;

        /// <summary>
        /// Подія запиту повторної спроби завантаження
        /// </summary>
        public event EventHandler? RetryRequested;
        
        /// <summary>
        /// Подія запиту повернення назад
        /// </summary>
        public event EventHandler? GoBackRequested;
        
        /// <summary>
        /// Подія запиту переходу на головну
        /// </summary>
        public event EventHandler? GoHomeRequested;
        
        /// <summary>
        /// Подія запиту пошуку
        /// </summary>
        public event EventHandler<string>? SearchRequested;

        public BrowserErrorPage()
        {
            InitializeComponent();
            FindControls();
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
            _errorIconText = this.FindControl<TextBlock>("ErrorIconText");
            _errorTitleText = this.FindControl<TextBlock>("ErrorTitleText");
            _errorCategoryText = this.FindControl<TextBlock>("ErrorCategoryText");
            _errorDescriptionText = this.FindControl<TextBlock>("ErrorDescriptionText");
            _failedUrlText = this.FindControl<TextBlock>("FailedUrlText");
            _errorCodeText = this.FindControl<TextBlock>("ErrorCodeText");
            _timestampText = this.FindControl<TextBlock>("TimestampText");
            _tipsPanel = this.FindControl<StackPanel>("TipsPanel");
            _searchSection = this.FindControl<Border>("SearchSection");
            _searchTextBox = this.FindControl<TextBox>("SearchTextBox");
            _retryButton = this.FindControl<Button>("RetryButton");
        }

        /// <summary>
        /// Встановлює дані помилки та оновлює UI
        /// </summary>
        public void SetError(BrowserError error)
        {
            _error = error ?? throw new ArgumentNullException(nameof(error));
            
            // Оновлюємо фон відповідно до категорії
            try
            {
                Background = new SolidColorBrush(Color.Parse(error.BackgroundColor));
            }
            catch
            {
                Background = new SolidColorBrush(Color.Parse("#FF9800"));
            }
            
            // Оновлюємо іконку та заголовок
            if (_errorIconText != null)
                _errorIconText.Text = error.Icon;
            
            if (_errorTitleText != null)
                _errorTitleText.Text = error.Title;
            
            if (_errorCategoryText != null)
                _errorCategoryText.Text = GetCategoryDisplayName(error.Category);
            
            // Оновлюємо опис
            if (_errorDescriptionText != null)
                _errorDescriptionText.Text = error.Description;
            
            // Оновлюємо URL
            if (_failedUrlText != null)
                _failedUrlText.Text = error.FailedUrl;
            
            // Оновлюємо код помилки
            if (_errorCodeText != null)
                _errorCodeText.Text = $"Код: {error.ErrorName} ({error.ErrorCode})";
            
            // Оновлюємо час
            if (_timestampText != null)
                _timestampText.Text = $"Час: {error.OccurredAt.ToLocalTime():HH:mm:ss}";
            
            // Оновлюємо підказки
            UpdateTips(error.Tips);
            
            // Показуємо/приховуємо секцію пошуку
            if (_searchSection != null)
                _searchSection.IsVisible = error.ShowSearch;
            
            // Показуємо/приховуємо кнопку повторної спроби
            if (_retryButton != null)
                _retryButton.IsVisible = error.CanRetry;
            
            // Заповнюємо поле пошуку доменом з URL
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
        /// Отримує назву категорії для відображення
        /// </summary>
        private string GetCategoryDisplayName(BrowserErrorCategory category)
        {
            return category switch
            {
                BrowserErrorCategory.NetworkError => "🌐 Мережева проблема",
                BrowserErrorCategory.ServerError => "🖥️ Проблема сервера",
                BrowserErrorCategory.SecurityError => "🔒 Проблема безпеки",
                BrowserErrorCategory.GeneralError => "⚠️ Загальна помилка",
                _ => "Помилка"
            };
        }
        
        /// <summary>
        /// Оновлює список підказок
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
                    Background = new SolidColorBrush(Color.Parse("#FAFAFA")),
                    BorderBrush = new SolidColorBrush(Color.Parse("#E8E8E8")),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(15, 10),
                    Margin = new Thickness(0, 0, 0, 8)
                };
                
                var tipPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 12
                };
                
                // Номер підказки
                var numberText = new TextBlock
                {
                    Text = $"{index}.",
                    FontWeight = FontWeight.SemiBold,
                    Foreground = new SolidColorBrush(Color.Parse("#4CAF50")),
                    VerticalAlignment = VerticalAlignment.Top
                };
                
                // Текст підказки
                var tipText = new TextBlock
                {
                    Text = tip,
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = new SolidColorBrush(Color.Parse("#424242")),
                    VerticalAlignment = VerticalAlignment.Center
                };
                
                tipPanel.Children.Add(numberText);
                tipPanel.Children.Add(tipText);
                tipBorder.Child = tipPanel;
                
                _tipsPanel.Children.Add(tipBorder);
                index++;
            }
        }
        
        /// <summary>
        /// Обробник кнопки "Спробувати знову"
        /// </summary>
        private void OnRetryClicked(object? sender, RoutedEventArgs e)
        {
            RetryRequested?.Invoke(this, EventArgs.Empty);
        }
        
        /// <summary>
        /// Обробник кнопки "Назад"
        /// </summary>
        private void OnGoBackClicked(object? sender, RoutedEventArgs e)
        {
            GoBackRequested?.Invoke(this, EventArgs.Empty);
        }
        
        /// <summary>
        /// Обробник кнопки "На головну"
        /// </summary>
        private void OnGoHomeClicked(object? sender, RoutedEventArgs e)
        {
            GoHomeRequested?.Invoke(this, EventArgs.Empty);
        }
        
        /// <summary>
        /// Обробник кнопки "Шукати"
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
        /// Статичний метод для швидкого створення сторінки помилки
        /// </summary>
        public static BrowserErrorPage CreateForCefError(int errorCode, string failedUrl, string? errorText = null)
        {
            var error = BrowserErrorService.GetLocalizedError(errorCode, failedUrl, errorText);
            return new BrowserErrorPage(error);
        }
        
        /// <summary>
        /// Статичний метод для створення сторінки HTTP помилки
        /// </summary>
        public static BrowserErrorPage CreateForHttpError(int httpStatusCode, string failedUrl)
        {
            var error = BrowserErrorService.GetHttpError(httpStatusCode, failedUrl);
            return new BrowserErrorPage(error);
        }
    }
}

