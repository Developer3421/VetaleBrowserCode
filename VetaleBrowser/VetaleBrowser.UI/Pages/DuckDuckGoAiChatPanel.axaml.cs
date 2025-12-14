using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using WebViewControl;

namespace VetaleBrowser.VetaleBrowser.UI.Pages;

/// <summary>
/// Панель з Perplexity AI через WebView
/// AI пошук з джерелами - працює без реєстрації (обмежено)
/// </summary>
public partial class DuckDuckGoAiChatPanel : UserControl, IDisposable
{
    private Grid? _webViewContainer;
    private Border? _loadingOverlay;
    private TextBlock? _statusText;
    private WebView? _webView;
    private bool _isDisposed;
    private bool _isLoaded;
    private bool _isWebViewReady;
    private string? _pendingMessage; // Повідомлення для відправки після завантаження
    
    // Perplexity AI URL - можна передати запит через параметр q
    private const string PerplexityAiUrl = "https://www.perplexity.ai";

    /// <summary>
    /// Подія для навігації до URL у браузері
    /// </summary>
    public event EventHandler<string>? NavigateRequested;

    public DuckDuckGoAiChatPanel()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        _webViewContainer = this.FindControl<Grid>("WebViewContainer");
        _loadingOverlay = this.FindControl<Border>("LoadingOverlay");
        _statusText = this.FindControl<TextBlock>("StatusText");

        if (!_isLoaded)
        {
            _isLoaded = true;
            InitializeWebView();
        }

        Debug.WriteLine("[DuckDuckGoAiChat] Panel loaded");
    }

    private void OnUnloaded(object? sender, RoutedEventArgs e)
    {
        // Не знищуємо WebView при вивантаженні, щоб зберегти сесію
        Debug.WriteLine("[DuckDuckGoAiChat] Panel unloaded (WebView preserved)");
    }

    private void InitializeWebView()
    {
        if (_webViewContainer == null) return;

        try
        {
            ShowLoading(true);
            UpdateStatus("Ініціалізація...");

            // Очищуємо контейнер
            _webViewContainer.Children.Clear();

            // Створюємо WebView з правильними властивостями розтягування
            _webView = new WebView
            {
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch
            };
            
            // Підписуємось на події
            _webView.PropertyChanged += OnWebViewPropertyChanged;
            
            // Додаємо до контейнера
            _webViewContainer.Children.Add(_webView);

            // Навігація до Perplexity AI
            _webView.Address = PerplexityAiUrl;

            Debug.WriteLine($"[PerplexityAiChat] WebView created, navigating to: {PerplexityAiUrl}");
            Core.Scripts.Services.ConsoleLogger.LogInfo($"Navigating to Perplexity AI", "PerplexityAiChat");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PerplexityAiChat] Error initializing WebView: {ex.Message}");
            Core.Scripts.Services.ConsoleLogger.LogError("Error initializing WebView", "PerplexityAiChat", ex);
            ShowError($"Помилка завантаження: {ex.Message}");
        }
    }

    private void OnWebViewPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (_webView == null || e.Property.Name != "Address") return;

        var address = _webView.Address;
        Debug.WriteLine($"[DuckDuckGoAiChat] Address changed: {address}");

        // Інжектимо скрипти для покращення UX
        Dispatcher.UIThread.Post(async () =>
        {
            await Task.Delay(2000); // Чекаємо завантаження сторінки (DuckDuckGo потребує більше часу)
            
            // Спочатку пробуємо прийняти згоду
            await AcceptTermsIfNeededAsync();
            
            await InjectCustomScriptsAsync();
            ShowLoading(false);
            UpdateStatus("Готово");
            
            // Помічаємо що WebView готовий
            _isWebViewReady = true;
            
            // Якщо є pending повідомлення - відправляємо з затримкою
            if (!string.IsNullOrWhiteSpace(_pendingMessage))
            {
                Debug.WriteLine($"[DuckDuckGoAiChat] Sending pending message: {_pendingMessage}");
                await Task.Delay(1000); // Більша затримка для стабільності після прийняття згоди
                await SendMessageInternalAsync(_pendingMessage);
                _pendingMessage = null;
            }
        });
    }
    
    /// <summary>
    /// Автоматично приймає Terms/Privacy якщо показується вікно згоди
    /// </summary>
    private async Task AcceptTermsIfNeededAsync()
    {
        if (_webView == null) return;
        
        try
        {
            // Скрипт для автоматичного прийняття згоди DuckDuckGo
            var js = @"
                (function() {
                    try {
                        console.log('[Vetale] Checking for consent dialog...');
                        
                        // Шукаємо кнопки прийняття згоди (різні варіанти)
                        var acceptButtons = [
                            // DuckDuckGo AI Chat специфічні
                            document.querySelector('button[data-testid=""chat-terms-accept""]'),
                            document.querySelector('button[data-testid=""accept-terms""]'),
                            document.querySelector('button[aria-label*=""Accept""]'),
                            document.querySelector('button[aria-label*=""agree"" i]'),
                            document.querySelector('button[aria-label*=""Accept"" i]'),
                            // Загальні селектори для кнопок згоди
                            document.querySelector('button.terms-accept'),
                            document.querySelector('button.accept-btn'),
                            document.querySelector('button.consent-accept'),
                            document.querySelector('[class*=""accept"" i] button'),
                            document.querySelector('[class*=""consent"" i] button'),
                            document.querySelector('[class*=""terms"" i] button'),
                            // Пошук за текстом
                            Array.from(document.querySelectorAll('button')).find(b => 
                                b.textContent && (
                                    b.textContent.toLowerCase().includes('accept') ||
                                    b.textContent.toLowerCase().includes('agree') ||
                                    b.textContent.toLowerCase().includes('i agree') ||
                                    b.textContent.toLowerCase().includes('get started') ||
                                    b.textContent.toLowerCase().includes('start chat') ||
                                    b.textContent.toLowerCase().includes('continue')
                                )
                            )
                        ];
                        
                        for (var i = 0; i < acceptButtons.length; i++) {
                            var btn = acceptButtons[i];
                            if (btn && btn.offsetParent !== null) { // Перевіряємо що кнопка видима
                                console.log('[Vetale] Found accept button:', btn.textContent || btn.outerHTML.substring(0, 100));
                                btn.click();
                                console.log('[Vetale] Clicked accept button!');
                                return true;
                            }
                        }
                        
                        // Також шукаємо чекбокси згоди
                        var checkboxes = document.querySelectorAll('input[type=""checkbox""]');
                        checkboxes.forEach(function(cb) {
                            if (!cb.checked) {
                                cb.click();
                                console.log('[Vetale] Clicked checkbox');
                            }
                        });
                        
                        console.log('[Vetale] No consent dialog found or already accepted');
                        return false;
                    } catch(e) {
                        console.error('[Vetale] Error accepting terms:', e);
                        return false;
                    }
                })();
            ";
            
            var result = await _webView.EvaluateScript<bool>(js);
            Debug.WriteLine($"[DuckDuckGoAiChat] AcceptTerms result: {result}");
            
            if (result)
            {
                // Якщо прийняли згоду - чекаємо поки UI оновиться
                await Task.Delay(1500);
                // Пробуємо ще раз на випадок якщо є ще одне вікно
                await _webView.EvaluateScript<bool>(js);
                await Task.Delay(500);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[DuckDuckGoAiChat] Error accepting terms: {ex.Message}");
        }
    }

    /// <summary>
    /// Інжектує кастомні скрипти для покращення UX
    /// </summary>
    private async Task InjectCustomScriptsAsync()
    {
        if (_webView == null) return;

        try
        {
            // Скрипт для:
            // 1. Блокування відкриття нових вікон
            // 2. Приховування зайвих елементів UI (опціонально)
            // 3. Обробки зовнішніх посилань
            var js = @"
                (function(){
                    if(window.__vetale_ddg_injected__) return;
                    window.__vetale_ddg_injected__ = true;
                    
                    // Блокуємо window.open
                    var originalOpen = window.open;
                    window.open = function(url) {
                        try {
                            if(url) location.href = url;
                        } catch(e) {}
                        return null;
                    };
                    
                    // Перехоплюємо кліки на посилання з target=_blank
                    document.addEventListener('click', function(e) {
                        try {
                            var el = e.target;
                            while(el && el.tagName !== 'A') { el = el.parentElement; }
                            if(!el) return;
                            
                            var href = el.getAttribute('href');
                            if(!href) return;
                            
                            var target = el.getAttribute('target');
                            if(target && target.toLowerCase() === '_blank') {
                                e.preventDefault();
                                e.stopPropagation();
                                // Відкриваємо в тому ж вікні
                                location.href = href;
                            }
                        } catch(_) {}
                    }, true);
                    
                    // Блокуємо середній клік миші
                    document.addEventListener('auxclick', function(e) {
                        try {
                            if(e.button === 1) {
                                var el = e.target;
                                while(el && el.tagName !== 'A') { el = el.parentElement; }
                                if(!el) return;
                                
                                var href = el.getAttribute('href');
                                if(href) {
                                    e.preventDefault();
                                    e.stopPropagation();
                                    location.href = href;
                                }
                            }
                        } catch(_) {}
                    }, true);
                    
                    console.log('[Vetale] DuckDuckGo AI Chat scripts injected');
                })();
            ";

            await _webView.EvaluateScript<object>(js);
            Debug.WriteLine("[DuckDuckGoAiChat] Custom scripts injected successfully");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[DuckDuckGoAiChat] Failed to inject scripts: {ex.Message}");
        }
    }

    /// <summary>
    /// Програмно відправити повідомлення в чат
    /// </summary>
    public async Task SendMessageAsync(string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return;
        
        Debug.WriteLine($"[PerplexityAiChat] SendMessageAsync called: {message}");
        
        // Для Perplexity найкраще використовувати URL з параметром q
        // Це автоматично виконає пошук
        if (_webView != null)
        {
            var encodedQuery = Uri.EscapeDataString(message);
            var searchUrl = $"{PerplexityAiUrl}/search?q={encodedQuery}";
            
            Debug.WriteLine($"[PerplexityAiChat] Navigating to search URL: {searchUrl}");
            _webView.Address = searchUrl;
            return;
        }
        
        // Якщо WebView ще не готовий - зберігаємо повідомлення в чергу
        if (!_isWebViewReady)
        {
            Debug.WriteLine($"[PerplexityAiChat] WebView not ready, queuing message");
            _pendingMessage = message;
            return;
        }
        
        await SendMessageInternalAsync(message);
    }
    
    /// <summary>
    /// Внутрішній метод для відправки повідомлення через навігацію URL
    /// </summary>
    private Task SendMessageInternalAsync(string message)
    {
        if (_webView == null || string.IsNullOrWhiteSpace(message)) 
            return Task.CompletedTask;

        try
        {
            // Для Perplexity краще використовувати URL напряму
            var encodedQuery = Uri.EscapeDataString(message);
            var searchUrl = $"{PerplexityAiUrl}/search?q={encodedQuery}";
            
            Debug.WriteLine($"[PerplexityAiChat] Navigating to: {searchUrl}");
            _webView.Address = searchUrl;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[PerplexityAiChat] Error sending message: {ex.Message}");
        }
        
        return Task.CompletedTask;
    }

    private void OnRefreshClick(object? sender, RoutedEventArgs e)
    {
        if (_webView != null)
        {
            try
            {
                ShowLoading(true);
                UpdateStatus("Оновлення...");
                _webView.Reload();
                Debug.WriteLine("[DuckDuckGoAiChat] Refreshing page");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DuckDuckGoAiChat] Error refreshing: {ex.Message}");
                ShowLoading(false);
            }
        }
    }

    private void OnNewChatClick(object? sender, RoutedEventArgs e)
    {
        if (_webView != null)
        {
            try
            {
                ShowLoading(true);
                UpdateStatus("Новий чат...");
                _isWebViewReady = false;
                // Перезавантажуємо сторінку для нового чату
                _webView.Address = PerplexityAiUrl;
                Debug.WriteLine("[PerplexityAiChat] Starting new chat");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PerplexityAiChat] Error starting new chat: {ex.Message}");
                ShowLoading(false);
            }
        }
    }

    private void ShowLoading(bool show)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (_loadingOverlay != null)
            {
                _loadingOverlay.IsVisible = show;
            }
        });
    }

    private void UpdateStatus(string status)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (_statusText != null)
            {
                _statusText.Text = status;
            }
        });
    }

    private void ShowError(string message)
    {
        if (_webViewContainer == null) return;

        Dispatcher.UIThread.Post(() =>
        {
            ShowLoading(false);
            _webViewContainer.Children.Clear();
            _webViewContainer.Children.Add(new StackPanel
            {
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                Spacing = 12,
                Children =
                {
                    new TextBlock
                    {
                        Text = "❌",
                        FontSize = 48,
                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
                    },
                    new TextBlock
                    {
                        Text = message,
                        Foreground = Avalonia.Media.Brushes.Red,
                        FontSize = 14,
                        TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
                    },
                    new Button
                    {
                        Content = "Спробувати знову",
                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                        Command = new RelayCommand(() => InitializeWebView())
                    }
                }
            });
        });
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        try
        {
            if (_webView != null)
            {
                _webView.PropertyChanged -= OnWebViewPropertyChanged;
                _webView.Dispose();
                _webView = null;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[DuckDuckGoAiChat] Error disposing: {ex.Message}");
        }

        Debug.WriteLine("[DuckDuckGoAiChat] Disposed");
    }

    /// <summary>
    /// Простий RelayCommand для кнопок
    /// </summary>
    private class RelayCommand : System.Windows.Input.ICommand
    {
        private readonly Action _execute;
        
        public RelayCommand(Action execute) => _execute = execute;
        
        public event EventHandler? CanExecuteChanged;
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => _execute();
    }
}

