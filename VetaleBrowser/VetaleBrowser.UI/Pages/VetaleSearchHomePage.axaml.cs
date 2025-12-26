using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Controls.Primitives;
using VetaleBrowser.VetaleBrowser.Search.Models;
using VetaleBrowser.VetaleBrowser.Search.Services;
using VetaleBrowser.VetaleBrowser.VoiceRecognition.Services;
using Avalonia;
using Avalonia.Threading;
using VetaleBrowser.VetaleBrowser.UI.Theme;
using VetaleBrowser.VetaleBrowser.UI.Services;

namespace VetaleBrowser.VetaleBrowser.UI.Pages;

public partial class VetaleSearchHomePage : UserControl
{
    public event EventHandler<string>? NavigateRequested;

    private TextBox? _searchInput;
    private ComboBox? _searchEngineSelector;
    private Button? _searchButton;
    private Button? _voiceButton;
    private Button? _imageSearchButton;
    private Popup? _suggestionsPopup;
    private ItemsControl? _suggestionsList;
    private readonly System.Collections.ObjectModel.ObservableCollection<SearchSuggestion> _suggestions = new();
    private ISuggestionsService? _suggestionsService;
    private IVoiceRecognitionService? _voiceRecognitionService;
    private System.Threading.CancellationTokenSource? _suggestionsCts;

    public VetaleSearchHomePage()
    {
        var msg = "[VOICE][HOME] VetaleSearchHomePage constructor called";
        System.Diagnostics.Debug.WriteLine(msg);
        Console.WriteLine(msg);
        
        InitializeComponent();
        InitializeControls();

        ApplyTheme();
        VetaleSearchThemeManager.ThemeChanged += OnThemeChanged;
        Unloaded += OnUnloaded;

        var msg2 = "[VOICE][HOME] VetaleSearchHomePage constructor completed";
        System.Diagnostics.Debug.WriteLine(msg2);
        Console.WriteLine(msg2);
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public void SetSuggestionsService(ISuggestionsService service)
    {
        _suggestionsService = service;
    }

    public void SetVoiceRecognitionService(IVoiceRecognitionService service)
    {
        var msg = "═══════════════════════════════════════════════════════\n" +
                  "[VOICE][HOME] ✓✓✓ SetVoiceRecognitionService CALLED ✓✓✓\n" +
                  "═══════════════════════════════════════════════════════";
        System.Diagnostics.Debug.WriteLine(msg);
        Console.WriteLine(msg);
        Console.WriteLine($"[VOICE][HOME] Service parameter null? {service == null}");
        
        _voiceRecognitionService = service;
        
        if (_voiceRecognitionService != null)
        {
            System.Diagnostics.Debug.WriteLine("[VOICE][HOME] Subscribing to voice events...");
            Console.WriteLine("[VOICE][HOME] Subscribing to voice events...");
            
            _voiceRecognitionService.TextRecognized += OnVoiceTextRecognized;
            _voiceRecognitionService.StateChanged += OnVoiceStateChanged;
            _voiceRecognitionService.ErrorOccurred += OnVoiceError;
            
            System.Diagnostics.Debug.WriteLine("[VOICE][HOME] ✓ Successfully subscribed to all voice events");
            Console.WriteLine("[VOICE][HOME] ✓ Successfully subscribed to all voice events");
        }
        else
        {
            var errMsg = "[VOICE][HOME] ✗ WARNING: Service is NULL, cannot subscribe to events!";
            System.Diagnostics.Debug.WriteLine(errMsg);
            Console.WriteLine(errMsg);
        }
    }

    private void InitializeControls()
    {
        _searchInput = this.FindControl<TextBox>("SearchInput");
        _searchEngineSelector = this.FindControl<ComboBox>("SearchEngineSelector");
        _searchButton = this.FindControl<Button>("SearchButton");
        _voiceButton = this.FindControl<Button>("VoiceButton");
        _imageSearchButton = this.FindControl<Button>("ImageSearchButton");
        _suggestionsPopup = this.FindControl<Popup>("SuggestionsPopup");
        _suggestionsList = this.FindControl<ItemsControl>("SuggestionsList");
        if (_suggestionsList != null)
        {
            _suggestionsList.ItemsSource = _suggestions;
            _suggestionsList.AddHandler(InputElement.PointerPressedEvent, OnSuggestionPointerPressed, handledEventsToo: false);
        }

        // Focus search input when page loads
        if (_searchInput != null)
        {
            _searchInput.Focus();
        }

        if (_searchButton != null)
        {
            // Defensive: rebind handler to prevent duplicates + survive visual tree/style reload edge cases
            _searchButton.Click -= Search_Click;
            _searchButton.Click += Search_Click;
            _searchButton.IsEnabled = !string.IsNullOrWhiteSpace(_searchInput?.Text);
        }
    }

    private void SearchInput_TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (_searchButton != null)
        {
            _searchButton.IsEnabled = !string.IsNullOrWhiteSpace(_searchInput?.Text);
        }
        _ = LoadSuggestionsAsync();
    }

    private async System.Threading.Tasks.Task LoadSuggestionsAsync()
    {
        if (_suggestionsService == null || _searchInput == null)
        {
            if (_suggestionsPopup != null) _suggestionsPopup.IsOpen = false;
            return;
        }
        _suggestionsCts?.Cancel();
        _suggestionsCts = new System.Threading.CancellationTokenSource();
        var token = _suggestionsCts.Token;
        try
        {
            await System.Threading.Tasks.Task.Delay(300, token); // debounce
            var query = _searchInput.Text ?? string.Empty;
            if (token.IsCancellationRequested) return;
            if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
            {
                _suggestions.Clear();
                if (_suggestionsPopup != null) _suggestionsPopup.IsOpen = false;
                return;
            }
            var results = await _suggestionsService.GetSuggestionsAsync(query, 8);
            if (token.IsCancellationRequested) return;
            _suggestions.Clear();
            foreach (var s in results) _suggestions.Add(s);
            if (_suggestionsPopup != null)
                _suggestionsPopup.IsOpen = _suggestions.Count > 0;
        }
        catch (System.OperationCanceledException) { }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[VetaleSearchHomePage] Suggestions error: {ex.Message}");
            _suggestions.Clear();
            if (_suggestionsPopup != null) _suggestionsPopup.IsOpen = false;
        }
    }

    private void OnSuggestionPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        try
        {
            // Знаходимо елемент на який клікнули
            if (e.Source is Control clickedControl)
            {
                // Шукаємо SearchSuggestion в DataContext поточного або батьківських елементів
                var current = clickedControl;
                SearchSuggestion? suggestion = null;
                
                while (current != null)
                {
                    if (current.DataContext is SearchSuggestion sug)
                    {
                        suggestion = sug;
                        break;
                    }
                    current = current.Parent as Control;
                }
                
                if (suggestion != null && _searchInput != null)
                {
                    System.Diagnostics.Debug.WriteLine($"[VetaleSearchHomePage] Suggestion clicked: {suggestion.Text}");
                    
                    // Вставляємо текст підказки в поле пошуку
                    _searchInput.Text = suggestion.Text;
                    
                    // Ставимо курсор в кінець тексту
                    _searchInput.CaretIndex = suggestion.Text.Length;
                    
                    // Закриваємо popup
                    if (_suggestionsPopup != null) _suggestionsPopup.IsOpen = false;
                    
                    // Фокусуємо поле пошуку
                    _searchInput.Focus();
                    
                    // НЕ виконуємо пошук автоматично - даємо користувачу можливість редагувати
                    // Якщо потрібно автоматично шукати, розкоментуйте:
                    // PerformSearch();
                    
                    e.Handled = true;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[VetaleSearchHomePage] OnSuggestionPointerPressed error: {ex.Message}");
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
        // "I'm feeling lucky" functionality
        PerformSearch(isLucky: true);
    }

    private void SearchEngineSelector_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        // Handle search engine change
        // You can store the selected engine for later use
    }

    private async void SearchSettings_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            var parentWindow = TopLevel.GetTopLevel(this) as Window;
            if (parentWindow != null)
            {
                var settingsWindow = new Windows.VetaleSearchSettingsWindow();
                await settingsWindow.ShowDialog(parentWindow);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[VetaleSearchHomePage] SearchSettings_Click ERROR: {ex.Message}");
        }
    }

    private async void Bookmarks_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            var parentWindow = TopLevel.GetTopLevel(this) as Window;
            if (parentWindow != null)
            {
                var bookmarksWindow = new Windows.BookmarksWindow();
                await bookmarksWindow.ShowDialog(parentWindow);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[VetaleSearchHomePage] Bookmarks_Click ERROR: {ex.Message}");
        }
    }

    private async void History_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            var parentWindow = TopLevel.GetTopLevel(this) as Window;
            if (parentWindow != null)
            {
                var historyWindow = new Windows.HistoryWindow();
                await historyWindow.ShowDialog(parentWindow);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[VetaleSearchHomePage] History_Click ERROR: {ex.Message}");
        }
    }

    private void GeoSearch_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            var raw = _searchInput?.Text ?? string.Empty;
            var query = raw.Trim();
            var url = string.IsNullOrWhiteSpace(query)
                ? "https://www.openstreetmap.org/"
                : $"https://www.openstreetmap.org/search?query={Uri.EscapeDataString(query)}";

            System.Diagnostics.Debug.WriteLine($"[VetaleSearchHomePage] GeoSearch_Click: query='{query}', url='{url}'");
            NavigateRequested?.Invoke(this, url);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[VetaleSearchHomePage] GeoSearch_Click ERROR: {ex.Message}");
        }
    }

    private void ImageSearch_Click(object? sender, RoutedEventArgs e)
    {
        if (_searchInput == null)
            return;

        var query = _searchInput.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(query))
        {
            _searchInput.Focus();
            return;
        }

        // Открываем страницу результатов в режиме изображений
        var resultsUrl = $"vetale://search/results?mode=images&q={Uri.EscapeDataString(query)}";
        NavigateRequested?.Invoke(this, resultsUrl);
    }

    private void PerformSearch(bool isLucky = false)
    {
        System.Diagnostics.Debug.WriteLine($"[VetaleSearchHomePage] PerformSearch called (isLucky={isLucky})");
        
        if (_searchInput == null)
        {
            System.Diagnostics.Debug.WriteLine($"[VetaleSearchHomePage] ✗ Search input is NULL");
            return;
        }
        
        System.Diagnostics.Debug.WriteLine($"[VetaleSearchHomePage] Search input text: '{_searchInput.Text}'");
        
        if (string.IsNullOrWhiteSpace(_searchInput.Text))
        {
            System.Diagnostics.Debug.WriteLine($"[VetaleSearchHomePage] ✗ Search input is empty or whitespace");
            return;
        }

        string query = _searchInput.Text.Trim();
        int selectedEngine = _searchEngineSelector?.SelectedIndex ?? 0;
        
        System.Diagnostics.Debug.WriteLine($"[VetaleSearchHomePage] Query: '{query}', Engine: {selectedEngine}");

        if (selectedEngine == 0) // Vetale Search (локальний)
        {
            var resultsUrl = $"vetale://search/results?q={Uri.EscapeDataString(query)}";

            // Primary path: event (wired by MainWindow/InternalUrlHandler in most cases)
            if (NavigateRequested != null)
            {
                NavigateRequested.Invoke(this, resultsUrl);
                return;
            }

            // Fallback: drive navigation through the global callback (set by MainWindow).
            var cb = InternalUrlHandler.NavigationRequestCallback;
            System.Diagnostics.Debug.WriteLine($"[VetaleSearchHomePage] NavigateRequested is NULL; NavigationRequestCallback={(cb != null ? "OK" : "NULL")}, url={resultsUrl}");

            if (cb != null)
            {
                Dispatcher.UIThread.Post(() =>
                {
                    try { cb(resultsUrl); }
                    catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[VetaleSearchHomePage] NavigationRequestCallback invoke failed: {ex.Message}"); }
                });
            }
            return;
        }

        string[] searchUrls = new[]
        {
            "",
            $"https://www.google.com/search?q={Uri.EscapeDataString(query)}",
            $"https://www.bing.com/search?q={Uri.EscapeDataString(query)}",
            $"https://duckduckgo.com/?q={Uri.EscapeDataString(query)}",
            $"https://yandex.com/search/?text={Uri.EscapeDataString(query)}",
            $"https://www.openstreetmap.org/search?query={Uri.EscapeDataString(query)}"
        };
        
        if (selectedEngine < searchUrls.Length && !string.IsNullOrEmpty(searchUrls[selectedEngine]))
        {
            System.Diagnostics.Debug.WriteLine($"[VetaleSearchHomePage] Web search URL: {searchUrls[selectedEngine]}");
            NavigateRequested?.Invoke(this, searchUrls[selectedEngine]);
            System.Diagnostics.Debug.WriteLine($"[VetaleSearchHomePage] ✓ NavigateRequested invoked for web search");
        }
    }

    public void SetQuery(string query)
    {
        if (_searchInput != null)
        {
            _searchInput.Text = query;
            // оновити стан кнопки пошуку
            if (_searchButton != null)
            {
                _searchButton.IsEnabled = !string.IsNullOrWhiteSpace(_searchInput.Text);
            }
        }
    }

    // Обробники подій голосового розпізнавання
    private async void VoiceButton_Click(object? sender, RoutedEventArgs e)
    {
        var msg = "🎤🎤🎤 VOICE BUTTON CLICKED 🎤🎤🎤";
        System.Diagnostics.Debug.WriteLine(msg);
        Console.WriteLine(msg);
        System.Diagnostics.Debug.WriteLine("🎤🎤🎤🎤🎤🎤🎤🎤🎤🎤🎤🎤🎤🎤🎤🎤🎤🎤🎤🎤🎤🎤🎤🎤🎤🎤🎤🎤🎤");
        System.Diagnostics.Debug.WriteLine("🎤 [VOICE][HOME] VOICE BUTTON CLICKED!!! 🎤");
        System.Diagnostics.Debug.WriteLine($"🎤 [VOICE][HOME] Service null? {_voiceRecognitionService == null}");
        System.Diagnostics.Debug.WriteLine($"🎤 [VOICE][HOME] State: {_voiceRecognitionService?.CurrentState}");
        System.Diagnostics.Debug.WriteLine("🎤🎤🎤🎤🎤🎤🎤🎤🎤🎤🎤🎤🎤🎤🎤🎤🎤🎤🎤🎤🎤🎤🎤🎤🎤🎤🎤🎤🎤");
        
        Console.WriteLine($"[VOICE][HOME] Service null? {_voiceRecognitionService == null}");
        
        if (_voiceRecognitionService == null)
        {
            var errMsg = "[VOICE][HOME] ✗✗✗ Voice recognition service NOT INITIALIZED ✗✗✗";
            System.Diagnostics.Debug.WriteLine(errMsg);
            Console.WriteLine(errMsg);
            return;
        }

        try
        {
            if (_voiceRecognitionService.CurrentState == VoiceRecognitionState.Listening)
            {
                System.Diagnostics.Debug.WriteLine("[VOICE][HOME] Currently listening, will stop");
                Console.WriteLine("[VOICE][HOME] Currently listening, will stop");
                _voiceRecognitionService.StopListening();
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("[VOICE][HOME] Checking IsAvailable...");
                Console.WriteLine("[VOICE][HOME] Checking IsAvailable...");
                
                var available = _voiceRecognitionService.IsAvailable();
                
                System.Diagnostics.Debug.WriteLine($"[VOICE][HOME] IsAvailable = {available}");
                Console.WriteLine($"[VOICE][HOME] IsAvailable = {available}");
                
                if (!available)
                {
                    System.Diagnostics.Debug.WriteLine("[VOICE][HOME] ✗ Voice recognition not available");
                    Console.WriteLine("[VOICE][HOME] ✗ Voice recognition not available");
                    OnVoiceError(this, "Розпізнавання голосу недоступне на цьому пристрої");
                    return;
                }

                System.Diagnostics.Debug.WriteLine("[VOICE][HOME] ✓ Starting voice recognition...");
                Console.WriteLine("[VOICE][HOME] ✓ Starting voice recognition...");
                
                await _voiceRecognitionService.StartListeningAsync();
                
                System.Diagnostics.Debug.WriteLine("[VOICE][HOME] ✓ StartListeningAsync completed");
                Console.WriteLine("[VOICE][HOME] ✓ StartListeningAsync completed");
            }
        }
        catch (Exception ex)
        {
            var errMsg = $"[VOICE][HOME] ✗✗✗ VoiceButton_Click EXCEPTION: {ex.Message}";
            System.Diagnostics.Debug.WriteLine(errMsg);
            System.Diagnostics.Debug.WriteLine($"[VOICE][HOME] Stack: {ex.StackTrace}");
            Console.WriteLine(errMsg);
            Console.WriteLine($"Stack: {ex.StackTrace}");
            OnVoiceError(this, $"Помилка: {ex.Message}");
        }
    }

    private async void OnVoiceTextRecognized(object? sender, string text)
    {
        System.Diagnostics.Debug.WriteLine($"[VOICE][HOME] ✓ Voice text recognized: '{text}'");
        
        if (string.IsNullOrWhiteSpace(text))
        {
            System.Diagnostics.Debug.WriteLine("[VOICE][HOME] ⚠️ Recognized text is empty!");
            return;
        }
        
        // Оновлюємо UI в UI-потоці
        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(async () =>
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("[VOICE][HOME] Setting search input text...");
                
                if (_searchInput != null)
                {
                    _searchInput.Text = text;
                    System.Diagnostics.Debug.WriteLine($"[VOICE][HOME] ✓ Search input text set to: '{_searchInput.Text}'");
                    
                    if (_searchButton != null)
                    {
                        _searchButton.IsEnabled = !string.IsNullOrWhiteSpace(text);
                        System.Diagnostics.Debug.WriteLine($"[VOICE][HOME] Search button enabled: {_searchButton.IsEnabled}");
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("[VOICE][HOME] ⚠️ Search input is null!");
                }
                
                // Автоматично зупиняємо після розпізнавання
                System.Diagnostics.Debug.WriteLine("[VOICE][HOME] Stopping voice recognition...");
                _voiceRecognitionService?.StopListening();
                
                // Невелика асинхронна затримка перед пошуком для оновлення UI
                await System.Threading.Tasks.Task.Delay(150);
                
                // Автоматично виконуємо пошук
                System.Diagnostics.Debug.WriteLine("[VOICE][HOME] Performing search...");
                PerformSearch();
                System.Diagnostics.Debug.WriteLine("[VOICE][HOME] ✓ Search performed!");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[VOICE][HOME] ✗ Error in OnVoiceTextRecognized: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[VOICE][HOME] Stack trace: {ex.StackTrace}");
            }
        });
    }

    private void OnVoiceStateChanged(object? sender, VoiceRecognitionState state)
    {
        System.Diagnostics.Debug.WriteLine($"[VetaleSearchHomePage] Voice state changed: {state}");
        
        // Оновлюємо UI в UI-потоці
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            if (_voiceButton != null)
            {
                // Змінюємо вигляд кнопки в залежності від стану
                var iconText = state switch
                {
                    VoiceRecognitionState.Listening => "⏹️", // Зупинити
                    VoiceRecognitionState.Processing => "⏳", // Обробка
                    _ => "🎤" // Мікрофон
                };
                
                // Створюємо новий TextBlock
                _voiceButton.Content = new TextBlock 
                { 
                    Text = iconText,
                    FontSize = 20
                };
                
                _voiceButton.IsEnabled = state != VoiceRecognitionState.Processing;
            }
        });
    }

    private void OnVoiceError(object? sender, string error)
    {
        System.Diagnostics.Debug.WriteLine($"[VetaleSearchHomePage] Voice error: {error}");
        
        // TODO: Показати користувачу повідомлення про помилку
        // Наприклад, через MessageBox або Toast notification
    }

    private void OnThemeChanged(object? sender, EventArgs e)
    {
        // Only re-apply theme. Do not touch navigation delegates here.
        ApplyTheme();
    }

    private void OnUnloaded(object? sender, RoutedEventArgs e)
    {
        VetaleSearchThemeManager.ThemeChanged -= OnThemeChanged;
        Unloaded -= OnUnloaded;
    }

    private async void ApplyTheme()
    {
        try
        {
            if (Application.Current != null)
                await VetaleSearchThemeManager.ApplyToResourceHostAsync(Application.Current);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[VetaleSearchHomePage] ApplyTheme error: {ex.Message}");
        }
    }
}
