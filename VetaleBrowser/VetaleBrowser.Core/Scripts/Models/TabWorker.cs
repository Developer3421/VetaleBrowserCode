using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using WebViewControl;
using Avalonia.Threading;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using VetaleBrowser.VetaleBrowser.Core.Services.Windows;
using VetaleBrowser.VetaleBrowser.Core.Services;
using VetaleBrowser.VetaleBrowser.Core.Scripts.ErrorHandlers;
using Avalonia.Controls;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace VetaleBrowser.VetaleBrowser.Core.Scripts.Models
{
    /// <summary>
    /// Представляє один запис в історії навігації вкладки
    /// </summary>
    public class NavigationEntry
    {
        /// <summary>URL сторінки (може бути як http(s):// так і vetale://)</summary>
        public string Url { get; set; } = string.Empty;
        
        /// <summary>Заголовок сторінки</summary>
        public string? Title { get; set; }
        
        /// <summary>Чи є це внутрішня сторінка (vetale://)</summary>
        public bool IsInternal { get; set; }
        
        /// <summary>UserControl для внутрішніх сторінок (VetaleSearchHomePage, тощо)</summary>
        public UserControl? InternalPageContent { get; set; }
        
        /// <summary>Час створення запису</summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Керує історією навігації для вкладки (стек вперед/назад)
    /// MEMORY OPTIMIZED: Limited history size, cleanup of old entries
    /// </summary>
    public class NavigationHistory
    {
        private readonly List<NavigationEntry> _entries = new();
        private int _currentIndex = -1;
        
        // MEMORY OPTIMIZATION: Limit history size
        private const int MaxHistoryEntries = 50;

        public event EventHandler? HistoryChanged;

        /// <summary>Поточний запис в історії</summary>
        public NavigationEntry? CurrentEntry => _currentIndex >= 0 && _currentIndex < _entries.Count 
            ? _entries[_currentIndex] 
            : null;

        /// <summary>Чи можна повернутися назад</summary>
        public bool CanGoBack => _currentIndex > 0;

        /// <summary>Чи можна перейти вперед</summary>
        public bool CanGoForward => _currentIndex >= 0 && _currentIndex < _entries.Count - 1;

        /// <summary>Додає новий запис в історію (видаляє всі "вперед" записи)</summary>
        public void AddEntry(NavigationEntry entry)
        {
            // Видаляємо всі записи після поточного (при новій навігації)
            if (_currentIndex < _entries.Count - 1)
            {
                // MEMORY OPTIMIZATION: Clear InternalPageContent before removal
                for (int i = _currentIndex + 1; i < _entries.Count; i++)
                {
                    _entries[i].InternalPageContent = null;
                }
                _entries.RemoveRange(_currentIndex + 1, _entries.Count - _currentIndex - 1);
            }

            _entries.Add(entry);
            _currentIndex = _entries.Count - 1;
            
            // MEMORY OPTIMIZATION: Remove oldest entries if over limit
            while (_entries.Count > MaxHistoryEntries && _currentIndex > 0)
            {
                _entries[0].InternalPageContent = null; // Clear reference
                _entries.RemoveAt(0);
                _currentIndex--;
            }
            
            HistoryChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>Переходить на один запис назад</summary>
        public NavigationEntry? GoBack()
        {
            if (!CanGoBack) return null;
            
            _currentIndex--;
            HistoryChanged?.Invoke(this, EventArgs.Empty);
            return CurrentEntry;
        }

        /// <summary>Переходить на один запис вперед</summary>
        public NavigationEntry? GoForward()
        {
            if (!CanGoForward) return null;
            
            _currentIndex++;
            HistoryChanged?.Invoke(this, EventArgs.Empty);
            return CurrentEntry;
        }

        /// <summary>Очищує всю історію</summary>
        public void Clear()
        {
            // MEMORY OPTIMIZATION: Clear all references first
            foreach (var entry in _entries)
            {
                entry.InternalPageContent = null;
            }
            _entries.Clear();
            _currentIndex = -1;
            HistoryChanged?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>Кількість записів в історії</summary>
        public int Count => _entries.Count;
    }

    /// <summary>
    /// Represents a browser worker that owns its own WebView (CefGlue-based via WebViewControl) and navigation manager.
    /// </summary>
    public sealed class TabWorker : IDisposable, INotifyPropertyChanged
    {
        public Guid Id { get; } = Guid.NewGuid();

        public WebView WebView { get; }
        public GlobalManagers.WebViewManager Manager { get; }
        public NavigationHistory History { get; } = new NavigationHistory();
        
        /// <summary>
        /// Обробник помилок WebView для локалізації CefGlue помилок
        /// </summary>
        public WebViewErrorHandler ErrorHandler { get; }

        private string? _title;
        private string? _address;
        private bool _isActive;
        private bool _isMuted;
        
        // Track OS process IDs associated with this tab's rendering/audible activity (best-effort, OS-level, non-CEF-specific)
        private readonly HashSet<int> _relatedPids = new();
        private readonly DispatcherTimer _pidRefreshTimer = new() { Interval = TimeSpan.FromSeconds(2) };
        
        // Timer for delayed mute reapplication after navigation
        private readonly DispatcherTimer _muteReapplyTimer = new() { Interval = TimeSpan.FromMilliseconds(500) };
        private int _muteReapplyCount;

        // Fullscreen poller to catch content-initiated fullscreen when events aren't exposed
        private readonly DispatcherTimer _fullscreenPollTimer = new() { Interval = TimeSpan.FromMilliseconds(250) };
        private bool _lastFullscreenState;

        // New: subprocess launched per tab (system-level process)
        private readonly TabSubprocessService _subprocess;
        
        // Прапорець для запобігання рекурсивним викликам при навігації
        private bool _isNavigating;

        private string? _prevAddress;
        private bool _blockingDownloadNav;
        private static readonly HttpClient _httpHead = new HttpClient() { Timeout = TimeSpan.FromSeconds(10) };

        public string? Title
        {
            get => _title;
            private set { if (_title != value) { _title = value; OnPropertyChanged(); TitleChanged?.Invoke(this, value); } }
        }

        public string? Address
        {
            get => _address;
            private set { if (_address != value) { _address = value; OnPropertyChanged(); AddressChanged?.Invoke(this, value); } }
        }

        public bool IsActive
        {
            get => _isActive;
            set { if (_isActive != value) { _isActive = value; OnPropertyChanged(); } }
        }

        public bool IsMuted
        {
            get => _isMuted;
            set 
            { 
                if (_isMuted != value) 
                { 
                    _isMuted = value; 
                    OnPropertyChanged(); 
                    ApplyMuteState();
                    
                    // Якщо вмикається mute, ін'єктуємо interceptor для перехоплення нових AudioContext
                    if (_isMuted)
                    {
                        _ = InjectAudioInterceptorAsync();
                    }
                } 
            }
        }

        public event EventHandler<string?>? TitleChanged;
        public event EventHandler<string?>? AddressChanged;
        public event EventHandler<bool>? FullscreenChanged;
        public event EventHandler<NavigationEntry>? NavigationChanged;
        
        /// <summary>
        /// Подія виникнення помилки з локалізованим контентом
        /// </summary>
        public event EventHandler<BrowserErrorEventArgs>? ErrorOccurred;

        public TabWorker()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"[TabWorker {Id}] Constructor started");
                
                // Start dedicated subprocess for this tab (created via the OS) and track its PID
                System.Diagnostics.Debug.WriteLine($"[TabWorker {Id}] Creating subprocess service...");
                _subprocess = new TabSubprocessService(Id);
                if (_subprocess.ProcessId.HasValue)
                {
                    _relatedPids.Add(_subprocess.ProcessId.Value);
                    System.Diagnostics.Debug.WriteLine($"[TabWorker {Id}] Subprocess PID: {_subprocess.ProcessId.Value}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[TabWorker {Id}] Subprocess has no PID");
                }

                System.Diagnostics.Debug.WriteLine($"[TabWorker {Id}] Creating WebView...");
                WebView = new WebView
                {
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch,
                };
                
                if (WebView == null)
                {
                    throw new InvalidOperationException("WebView creation returned null");
                }
                System.Diagnostics.Debug.WriteLine($"[TabWorker {Id}] WebView created successfully");

                System.Diagnostics.Debug.WriteLine($"[TabWorker {Id}] Creating WebViewManager...");
                Manager = new GlobalManagers.WebViewManager();
                if (Manager == null)
                {
                    throw new InvalidOperationException("WebViewManager creation returned null");
                }
                System.Diagnostics.Debug.WriteLine($"[TabWorker {Id}] WebViewManager created");
                
                System.Diagnostics.Debug.WriteLine($"[TabWorker {Id}] Initializing Manager...");
                Manager.Initialize(WebView);
                System.Diagnostics.Debug.WriteLine($"[TabWorker {Id}] Manager initialized");
                
                // Initialize error handler for localized error pages
                System.Diagnostics.Debug.WriteLine($"[TabWorker {Id}] Creating ErrorHandler...");
                ErrorHandler = new WebViewErrorHandler(WebView);
                ErrorHandler.ErrorOccurred += OnErrorHandlerError;
                ErrorHandler.Attach();
                System.Diagnostics.Debug.WriteLine($"[TabWorker {Id}] ErrorHandler initialized");

                // Forward manager-initiated navigations to Address property
                Manager.Navigated += (_, url) => Address = url;

                // Observe WebView property changes to keep state up-to-date
                WebView.PropertyChanged += WebViewOnPropertyChanged;

                // Initialize subprocess title
                TryUpdateSubprocessTitle();
                
                // Підписуємося на зміни історії навігації
                History.HistoryChanged += (_, __) => OnPropertyChanged(nameof(History));
                
                System.Diagnostics.Debug.WriteLine($"[TabWorker {Id}] Setting up fullscreen events...");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TabWorker {Id}] ERROR in constructor (before hooks): {ex}");
                throw;
            }

            try
            {
                // Try to hook fullscreen events via reflection
                TryHookFullscreenEvents();

                // Also inject client-side navigation guards to keep navigation in-tab
                InjectNavigationGuards();

                // Start polling for fullscreen changes as a robust fallback
                _fullscreenPollTimer.Tick += (_, __) => PollFullscreenAsync();
                _fullscreenPollTimer.Start();

                // Periodically refresh related PIDs shortly after navigation/content changes
                _pidRefreshTimer.Tick += (_, __) => RefreshRelatedProcessesBestEffort();
                
                System.Diagnostics.Debug.WriteLine($"[TabWorker {Id}] Constructor completed successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TabWorker {Id}] ERROR in constructor (hooks): {ex}");
                // Continue - these are non-critical features
            }
        }

        private void WebViewOnPropertyChanged(object? sender, Avalonia.AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property?.Name == "Address")
            {
                var newAddr = WebView.Address;
                var prev = _prevAddress;
                _prevAddress = newAddr;
                
                // Перевіряємо чи це зовнішній протокол і блокуємо
                if (!string.IsNullOrEmpty(newAddr) && IsExternalProtocol(newAddr))
                {
                    System.Diagnostics.Debug.WriteLine($"[TabWorker] BLOCKED external protocol: {newAddr}");
                    // Повертаємось на попередню сторінку
                    if (!string.IsNullOrEmpty(prev))
                    {
                        try { WebView.Address = prev; } catch { }
                    }
                    return;
                }
                
                // Ре-ін'єктуємо JavaScript guards після кожної навігації
                InjectNavigationGuards();
                
                // Повторно застосовуємо стан mute після кожної навігації
                // Це гарантує що mute працює для нового контенту (ігри, WebGL, нові вкладки)
                if (_isMuted)
                {
                    // Даємо час на ініціалізацію нового контенту
                    ScheduleMuteReapply();
                }
            }
            else if (e.Property?.Name == "Title")
            {
                var t = WebView.Title;
                if (!string.IsNullOrWhiteSpace(t))
                {
                    Title = $"Tab: {t}";
                    TryUpdateSubprocessTitle();
                }
            }
            else if (e.Property?.Name == "CanGoBack" || e.Property?.Name == "CanGoForward")
            {
                // no-op
            }
        }
        
        /// <summary>
        /// Перевіряє чи URL є зовнішнім протоколом (intent://, tel://, mailto://, etc.)
        /// </summary>
        private static bool IsExternalProtocol(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return false;
            
            var u = url.Trim().ToLowerInvariant();
            
            // Дозволені протоколи - все всередині браузера
            if (u.StartsWith("http://") || u.StartsWith("https://") || 
                u.StartsWith("file://") || u.StartsWith("data:") || 
                u.StartsWith("javascript:") || u.StartsWith("blob:") ||
                u.StartsWith("about:") || u.StartsWith("vetale:"))
            {
                return false;
            }
            
            // Заблоковані протоколи
            string[] blockedProtocols = {
                "intent:", "android-app:", "market:", "tel:", "mailto:", 
                "sms:", "whatsapp:", "tg:", "viber:", "skype:", "zoom:",
                "ms-", "vnd.", "app:", "custom:", "myapp:"
            };
            
            foreach (var p in blockedProtocols)
            {
                if (u.StartsWith(p) || u.Contains("://" + p)) return true;
            }
            
            // Якщо є ":" в перших 20 символах і це не http/https - заблокувати
            var colonIndex = u.IndexOf(':');
            if (colonIndex > 0 && colonIndex < 20)
            {
                var protocol = u.Substring(0, colonIndex);
                // Перевіряємо чи це не звичайний URL
                if (protocol != "http" && protocol != "https" && protocol != "file" && 
                    protocol != "data" && protocol != "javascript" && protocol != "blob" &&
                    protocol != "about" && protocol != "vetale")
                {
                    return true;
                }
            }
            
            return false;
        }

        private void SchedulePidRefreshBurst()
        {
            try
            {
                // Run a few quick refresh cycles to catch newly spawned render processes after navigation
                int remaining = 3;
                _pidRefreshTimer.Tag = remaining; // store state via Tag-like pattern using extension below
                _pidRefreshTimer.Stop();
                _pidRefreshTimer.Tick -= PidRefreshTick;
                _pidRefreshTimer.Tick += PidRefreshTick;
                _pidRefreshTimer.Start();
            }
            catch { }
        }

        private void PidRefreshTick(object? sender, EventArgs e)
        {
            RefreshRelatedProcessesBestEffort();
            if (_pidRefreshTimer.Tag is int left)
            {
                left--;
                if (left <= 0)
                {
                    _pidRefreshTimer.Stop();
                    _pidRefreshTimer.Tick -= PidRefreshTick;
                    _pidRefreshTimer.Tag = null;
                }
                else
                {
                    _pidRefreshTimer.Tag = left;
                }
            }
        }

        private void RefreshRelatedProcessesBestEffort()
        {
            try
            {
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return;

                // Claim new child PIDs not yet assigned to other tabs
                var newOnes = TabProcessTracker.ClaimNewChildPids();
                if (newOnes.Count == 0) return;

                foreach (var pid in newOnes)
                {
                    _relatedPids.Add(pid);
                }
            }
            catch { }
        }

        private async void PollFullscreenAsync()
        {
            try
            {
                bool isFs = await EvaluateScriptAsBoolAsync("!!(document.fullscreenElement||document.webkitFullscreenElement||document.msFullscreenElement)");
                if (isFs != _lastFullscreenState)
                {
                    _lastFullscreenState = isFs;
                    System.Diagnostics.Debug.WriteLine($"[TabWorker] Fullscreen polled: {isFs}");
                    FullscreenChanged?.Invoke(this, isFs);
                }
            }
            catch (Exception ex)
            {
                // swallow polling errors, keep timer running
                System.Diagnostics.Debug.WriteLine($"[TabWorker] Fullscreen poll failed: {ex.Message}");
            }
        }

        private async System.Threading.Tasks.Task<bool> EvaluateScriptAsBoolAsync(string script)
        {
            try
            {
                // Use direct EvaluateScript from WebViewControl (returns Task<object>)
                var result = await WebView.EvaluateScript<object>(script);
                
                if (result is bool b) return b;
                if (result is string s)
                {
                    if (bool.TryParse(s.Trim(), out var parsed)) return parsed;
                    if (string.Equals(s.Trim(), "1", StringComparison.Ordinal)) return true;
                    if (string.Equals(s.Trim(), "0", StringComparison.Ordinal)) return false;
                    // Check if it's "true" or "false" in lowercase
                    if (string.Equals(s.Trim(), "true", StringComparison.OrdinalIgnoreCase)) return true;
                    if (string.Equals(s.Trim(), "false", StringComparison.OrdinalIgnoreCase)) return false;
                }
                if (result is int i) return i != 0;
                if (result is long l) return l != 0;
                
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TabWorker] Script evaluation failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Навігація на URL (підтримує як звичайні http(s):// так і внутрішні vetale:// URL)
        /// MEMORY OPTIMIZATION: Примусове очищення пам'яті при переході
        /// </summary>
        public async void Navigate(string url, UserControl? internalPageContent = null)
        {
            if (_isNavigating) return; // Запобігаємо рекурсії
            
            try
            {
                _isNavigating = true;
                
                if (string.IsNullOrWhiteSpace(url))
                {
                    System.Diagnostics.Debug.WriteLine($"[TabWorker {Id}] Navigate: empty URL");
                    return;
                }

                System.Diagnostics.Debug.WriteLine($"[TabWorker {Id}] Navigate: {url}");

                // MEMORY OPTIMIZATION: Примусове очищення пам'яті перед навігацією
                // Це критично для AMD карт (RX 5700 XT) де текстури WebView не звільняються
                TriggerMemoryCleanup();

                // Визначаємо чи це внутрішній URL
                bool isInternal = url.StartsWith("vetale://", StringComparison.OrdinalIgnoreCase);

                // Встановлюємо pending URL для error handler
                ErrorHandler?.SetPendingNavigation(url);

                // Створюємо запис в історії
                var entry = new NavigationEntry
                {
                    Url = url,
                    IsInternal = isInternal,
                    InternalPageContent = internalPageContent,
                    Timestamp = DateTime.UtcNow
                };

                // Додаємо в історію
                History.AddEntry(entry);

                // Оновлюємо Address
                Address = url;

                if (isInternal)
                {
                    // Для внутрішніх URL не викликаємо WebView.Navigate
                    System.Diagnostics.Debug.WriteLine($"[TabWorker {Id}] Internal navigation to: {url}");
                    
                    // Заголовок буде встановлено через подію NavigationChanged
                    Title = GetInternalPageTitle(url);
                }
                else
                {
                    // Для зовнішніх URL - опціонально перевіряємо доступність через HTTP pre-check
                    // Це дає швидку детекцію помилок DNS та недоступних серверів
                    System.Diagnostics.Debug.WriteLine($"[TabWorker {Id}] External navigation to: {url}");
                    
                    // Pre-check для швидкої детекції помилок (не блокуємо навігацію)
                    _ = PreCheckAndNavigateAsync(url);
                }

                // Сповіщаємо про зміну навігації
                NavigationChanged?.Invoke(this, entry);
                
                // MEMORY OPTIMIZATION: Очищення після навігації
                _ = Task.Delay(500).ContinueWith(_ => TriggerMemoryCleanup());
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TabWorker {Id}] Navigate error: {ex.Message}");
            }
            finally
            {
                _isNavigating = false;
            }
        }
        
        /// <summary>
        /// Асинхронна перевірка URL та навігація
        /// </summary>
        private async Task PreCheckAndNavigateAsync(string url)
        {
            try
            {
                // КРИТИЧНО: Блокуємо зовнішні протоколи ДО будь-якої навігації
                if (IsExternalProtocol(url))
                {
                    System.Diagnostics.Debug.WriteLine($"[TabWorker {Id}] BLOCKED external protocol in PreCheck: {url}");
                    return; // Нічого не робимо - просто блокуємо
                }
                
                // Перевіряємо доступність URL через HTTP HEAD request
                if (ErrorHandler != null)
                {
                    var (isSuccess, errorCode, errorMessage) = await ErrorHandler.PreCheckUrlAsync(url);
                    
                    if (!isSuccess)
                    {
                        System.Diagnostics.Debug.WriteLine($"[TabWorker {Id}] PreCheck failed: {errorCode} - {errorMessage}");
                        
                        // Повідомляємо про помилку через ErrorHandler
                        // Це покаже сторінку помилки швидше ніж WebView
                        if (errorCode >= 400)
                        {
                            ErrorHandler.ReportHttpError(errorCode, url);
                        }
                        else
                        {
                            ErrorHandler.ReportError(errorCode, url, errorMessage);
                        }
                        return; // Не навігуємо в WebView
                    }
                }
                
                // Якщо pre-check пройшов - навігуємо в WebView
                await Manager.NavigateAsync(url);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TabWorker {Id}] PreCheckAndNavigateAsync error: {ex.Message}");
                // Fallback - навігуємо в WebView напряму (тільки якщо не зовнішній протокол)
                if (!IsExternalProtocol(url))
                {
                    await Manager.NavigateAsync(url);
                }
            }
        }
        
        /// <summary>
        /// Обробник помилок від WebViewErrorHandler
        /// Пересилає подію підписникам TabWorker
        /// </summary>
        private void OnErrorHandlerError(object? sender, BrowserErrorEventArgs e)
        {
            try
            {
                Debug.WriteLine($"[TabWorker {Id}] Error received: {e.Error.Title}");
                ErrorOccurred?.Invoke(this, e);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[TabWorker {Id}] OnErrorHandlerError failed: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Повідомляє про помилку CefGlue вручну
        /// Може бути викликаний ззовні при виявленні помилки
        /// </summary>
        public void ReportError(int errorCode, string failedUrl, string? errorText = null)
        {
            ErrorHandler?.ReportError(errorCode, failedUrl, errorText);
        }
        
        /// <summary>
        /// Повідомляє про HTTP помилку сервера
        /// </summary>
        public void ReportHttpError(int httpStatusCode, string failedUrl)
        {
            ErrorHandler?.ReportHttpError(httpStatusCode, failedUrl);
        }
        
        /// <summary>
        /// MEMORY OPTIMIZATION: Примусове звільнення пам'яті
        /// Критично для AMD карт де GPU текстури не звільняються автоматично
        /// </summary>
        private static int _lastGcGeneration = 0;
        private void TriggerMemoryCleanup()
        {
            try
            {
                // Не викликаємо GC занадто часто (мінімум раз на 3 секунди)
                var currentGen = GC.CollectionCount(2);
                if (currentGen == _lastGcGeneration)
                {
                    // Ще не було GC Gen2 - можемо запустити
                    GC.Collect(1, GCCollectionMode.Optimized, false);
                }
                _lastGcGeneration = GC.CollectionCount(2);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[TabWorker] Memory cleanup error: {ex.Message}");
            }
        }

        /// <summary>
        /// Повертається на одну сторінку назад в історії
        /// </summary>
        public void GoBack()
        {
            if (!History.CanGoBack)
            {
                System.Diagnostics.Debug.WriteLine($"[TabWorker {Id}] GoBack: no history");
                return;
            }

            var entry = History.GoBack();
            if (entry != null)
            {
                System.Diagnostics.Debug.WriteLine($"[TabWorker {Id}] GoBack to: {entry.Url}");
                NavigateToHistoryEntry(entry);
            }
        }

        /// <summary>
        /// Переходить на одну сторінку вперед в історії
        /// </summary>
        public void GoForward()
        {
            if (!History.CanGoForward)
            {
                System.Diagnostics.Debug.WriteLine($"[TabWorker {Id}] GoForward: no history");
                return;
            }

            var entry = History.GoForward();
            if (entry != null)
            {
                System.Diagnostics.Debug.WriteLine($"[TabWorker {Id}] GoForward to: {entry.Url}");
                NavigateToHistoryEntry(entry);
            }
        }

        /// <summary>
        /// Навігація до існуючого запису з історії (без додавання нового запису)
        /// </summary>
        private async void NavigateToHistoryEntry(NavigationEntry entry)
        {
            if (_isNavigating) return;
            
            try
            {
                _isNavigating = true;

                Address = entry.Url;
                
                if (entry.IsInternal)
                {
                    Title = entry.Title ?? GetInternalPageTitle(entry.Url);
                }
                else
                {
                    await Manager.NavigateAsync(entry.Url);
                }

                NavigationChanged?.Invoke(this, entry);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TabWorker {Id}] NavigateToHistoryEntry error: {ex.Message}");
            }
            finally
            {
                _isNavigating = false;
            }
        }

        /// <summary>
        /// Отримує заголовок для внутрішньої сторінки за URL
        /// </summary>
        private string GetInternalPageTitle(string url)
        {
            if (url.StartsWith("vetale://search?", StringComparison.OrdinalIgnoreCase))
            {
                // Витягуємо запит з URL
                try
                {
                    var uri = new Uri(url);
                    var queryParams = uri.Query.TrimStart('?').Split('&')
                        .Select(p => p.Split('='))
                        .Where(parts => parts.Length == 2)
                        .ToDictionary(parts => parts[0], parts => Uri.UnescapeDataString(parts[1]));
                    
                    if (queryParams.TryGetValue("q", out var q) && !string.IsNullOrWhiteSpace(q))
                    {
                        return $"{q} - Vetale Search";
                    }
                }
                catch { }
                
                return "Vetale Search";
            }
            else if (url.Equals("vetale://search", StringComparison.OrdinalIgnoreCase))
            {
                return "Vetale Search";
            }
            else if (url.StartsWith("vetale://bookmarks", StringComparison.OrdinalIgnoreCase))
            {
                return "Закладки";
            }
            else if (url.StartsWith("vetale://history", StringComparison.OrdinalIgnoreCase))
            {
                return "Історія";
            }
            else if (url.StartsWith("vetale://settings", StringComparison.OrdinalIgnoreCase))
            {
                return "Налаштування";
            }
            else if (url.StartsWith("vetale://downloads", StringComparison.OrdinalIgnoreCase))
            {
                return "Завантаження";
            }
            
            return "Vetale Browser";
        }

        public void ToggleMute()
        {
            IsMuted = !IsMuted;
        }

        private void TryHookFullscreenEvents()
        {
            // WebViewControl (CefGlue-based) doesn't expose fullscreen events directly
            // We rely on JavaScript injection and polling instead
            System.Diagnostics.Debug.WriteLine("[TabWorker] Using JavaScript polling for fullscreen detection");
            InjectFullscreenListener();
        }

        private async void InjectFullscreenListener()
        {
            try
            {
                // JavaScript to listen for fullscreen changes (debug logging)
                var script = @"
                    (function() {
                        if (window.__fullscreenListenerInjected) return;
                        window.__fullscreenListenerInjected = true;
                        
                        document.addEventListener('fullscreenchange', function() {
                            var isFullscreen = !!document.fullscreenElement;
                            console.log('Fullscreen changed:', isFullscreen);
                        });
                        
                        document.addEventListener('webkitfullscreenchange', function() {
                            var isFullscreen = !!document.webkitFullscreenElement;
                            console.log('Webkit fullscreen changed:', isFullscreen);
                        });
                    })();
                ";

                // Use direct EvaluateScript from WebViewControl with explicit type
                await WebView.EvaluateScript<object>(script);
                System.Diagnostics.Debug.WriteLine("[TabWorker] Fullscreen listener injected via JavaScript");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TabWorker] Failed to inject fullscreen listener: {ex.Message}");
            }
        }

        private async void InjectNavigationGuards()
        {
            try
            {
                var js = @"
                    (function(){
                        if(window.__vetale_no_external_v2__) return; 
                        window.__vetale_no_external_v2__ = true;
                        
                        // Список заблокованих протоколів
                        var blockedProtocols = ['intent:', 'android-app:', 'market:', 'tel:', 'mailto:', 'sms:', 'whatsapp:', 'tg:', 'viber:', 'skype:', 'zoom:', 'ms-', 'vnd.'];
                        
                        function isBlockedUrl(url){
                            if(!url) return false;
                            var u = url.toString().toLowerCase().trim();
                            // Блокуємо всі non-http/https протоколи крім file, javascript, data, blob
                            if(u.startsWith('http:') || u.startsWith('https:') || u.startsWith('file:') || 
                               u.startsWith('javascript:') || u.startsWith('data:') || u.startsWith('blob:') ||
                               u.startsWith('about:') || u.startsWith('vetale:')) {
                                return false;
                            }
                            // Блокуємо все інше
                            for(var i=0; i<blockedProtocols.length; i++){
                                if(u.indexOf(blockedProtocols[i]) !== -1) return true;
                            }
                            // Якщо є ':' і це не http/https - блокуємо
                            var colonIdx = u.indexOf(':');
                            if(colonIdx > 0 && colonIdx < 20) return true;
                            return false;
                        }
                        
                        // КРИТИЧНО: Перехоплюємо location.href та location.assign
                        try {
                            var origLocationDescriptor = Object.getOwnPropertyDescriptor(window, 'location');
                            var origLocation = window.location;
                            
                            // Перехоплюємо window.location.href = ...
                            if(origLocation && origLocation.href !== undefined) {
                                var origHrefSetter = Object.getOwnPropertyDescriptor(Object.getPrototypeOf(origLocation), 'href');
                                if(origHrefSetter && origHrefSetter.set) {
                                    Object.defineProperty(origLocation, 'href', {
                                        get: function(){ return origHrefSetter.get.call(this); },
                                        set: function(v){
                                            if(isBlockedUrl(v)){
                                                console.log('[VetaleBrowser] Blocked external redirect:', v);
                                                return;
                                            }
                                            origHrefSetter.set.call(this, v);
                                        },
                                        configurable: true
                                    });
                                }
                            }
                        } catch(e){ console.log('[VetaleBrowser] location override failed:', e); }
                        
                        // Перехоплюємо location.assign та location.replace
                        try {
                            var origAssign = location.assign;
                            var origReplace = location.replace;
                            location.assign = function(url){
                                if(isBlockedUrl(url)){
                                    console.log('[VetaleBrowser] Blocked location.assign:', url);
                                    return;
                                }
                                return origAssign.call(location, url);
                            };
                            location.replace = function(url){
                                if(isBlockedUrl(url)){
                                    console.log('[VetaleBrowser] Blocked location.replace:', url);
                                    return;
                                }
                                return origReplace.call(location, url);
                            };
                        } catch(e){ console.log('[VetaleBrowser] location methods override failed:', e); }
                        
                        // Перехоплюємо window.open
                        try {
                            var originalOpen = window.open;
                            window.open = function(url, name, specs){
                                if(isBlockedUrl(url)){
                                    console.log('[VetaleBrowser] Blocked window.open:', url);
                                    return null;
                                }
                                // Відкриваємо в тій самій вкладці замість нового вікна
                                try{
                                    if(url){ location.href = url; }
                                }catch(e){}
                                return null;
                            };
                        } catch(e) {}
                        
                        // Перехоплюємо всі кліки на посилання
                        document.addEventListener('click', function(e){
                            try{
                                var el = e.target;
                                while(el && el.tagName !== 'A'){ el = el.parentElement; }
                                if(!el) return;
                                var href = el.getAttribute('href') || el.href;
                                if(!href) return;
                                
                                // Блокуємо зовнішні протоколи
                                if(isBlockedUrl(href)){
                                    e.preventDefault(); 
                                    e.stopPropagation();
                                    e.stopImmediatePropagation();
                                    console.log('[VetaleBrowser] Blocked link click:', href);
                                    return false;
                                }
                                
                                // _blank відкриваємо в тій самій вкладці
                                var target = el.getAttribute('target');
                                if(target && target.toLowerCase() === '_blank'){
                                    e.preventDefault();
                                    e.stopPropagation();
                                    try{ location.href = href; }catch(_){ }
                                }
                            }catch(_){ }
                        }, true);
                        
                        // Перехоплюємо форми
                        document.addEventListener('submit', function(e){
                            try{
                                var f = e.target; if(!f) return;
                                var action = f.getAttribute('action') || '';
                                if(isBlockedUrl(action)){
                                    e.preventDefault();
                                    console.log('[VetaleBrowser] Blocked form submit:', action);
                                    return;
                                }
                                var t = f.getAttribute('target');
                                if(t && t.toLowerCase() === '_blank'){
                                    e.preventDefault();
                                    try{ f.removeAttribute('target'); f.submit(); }catch(_){ }
                                }
                            }catch(_){ }
                        }, true);
                        
                        // Блокуємо navigator.registerProtocolHandler
                        try {
                            if(navigator.registerProtocolHandler){
                                navigator.registerProtocolHandler = function(){ 
                                    console.log('[VetaleBrowser] Blocked registerProtocolHandler');
                                    return; 
                                };
                            }
                        } catch(e){}
                        
                        console.log('[VetaleBrowser] Navigation guards v2 active');
                    })();
                ";

                await WebView.EvaluateScript<object>(js);
                Debug.WriteLine("[TabWorker] Navigation guards v2 injected");
                
                // Якщо вкладка замутована, ін'єктуємо audio interceptor для перехоплення нових AudioContext
                if (_isMuted)
                {
                    await InjectAudioInterceptorAsync();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[TabWorker] Failed to inject navigation guards: {ex.Message}");
            }
        }

        /// <summary>
        /// Ін'єктує JavaScript код, який перехоплює AudioContext до його створення.
        /// Це гарантує що ігри типу HexGL будуть замутовані з самого початку.
        /// </summary>
        private async Task InjectAudioInterceptorAsync()
        {
            try
            {
                var js = @"(function(){
                    if (window._vetaleAudioInterceptorInjected) return;
                    window._vetaleAudioInterceptorInjected = true;
                    
                    // Initialize tracking arrays
                    window._vetaleAudioContexts = window._vetaleAudioContexts || [];
                    window._vetaleGainNodes = window._vetaleGainNodes || [];
                    window._vetaleAudioMuted = true;
                    
                    // Hook AudioContext
                    if (window.AudioContext && !window._vetaleOrigAudioContext) {
                        window._vetaleOrigAudioContext = window.AudioContext;
                        window.AudioContext = function() {
                            var ctx = new window._vetaleOrigAudioContext();
                            window._vetaleAudioContexts.push(ctx);
                            
                            // Auto-suspend if muted
                            if (window._vetaleAudioMuted) {
                                try { ctx.suspend(); } catch(e){}
                            }
                            
                            // Hook createGain to track gain nodes
                            var origCreateGain = ctx.createGain.bind(ctx);
                            ctx.createGain = function() {
                                var gain = origCreateGain();
                                window._vetaleGainNodes.push(gain);
                                if (window._vetaleAudioMuted) {
                                    try { gain.gain.value = 0; } catch(e){}
                                }
                                return gain;
                            };
                            
                            return ctx;
                        };
                        window.AudioContext.prototype = window._vetaleOrigAudioContext.prototype;
                    }
                    
                    // Hook webkitAudioContext
                    if (window.webkitAudioContext && !window._vetaleOrigWebkitAudioContext) {
                        window._vetaleOrigWebkitAudioContext = window.webkitAudioContext;
                        window.webkitAudioContext = function() {
                            var ctx = new window._vetaleOrigWebkitAudioContext();
                            window._vetaleAudioContexts.push(ctx);
                            
                            if (window._vetaleAudioMuted) {
                                try { ctx.suspend(); } catch(e){}
                            }
                            
                            var origCreateGain = ctx.createGain.bind(ctx);
                            ctx.createGain = function() {
                                var gain = origCreateGain();
                                window._vetaleGainNodes.push(gain);
                                if (window._vetaleAudioMuted) {
                                    try { gain.gain.value = 0; } catch(e){}
                                }
                                return gain;
                            };
                            
                            return ctx;
                        };
                        window.webkitAudioContext.prototype = window._vetaleOrigWebkitAudioContext.prototype;
                    }
                    
                    console.log('[VetaleBrowser] Audio interceptor injected (muted mode)');
                })();";
                
                await WebView.EvaluateScript<object>(js);
                Debug.WriteLine("[TabWorker] Audio interceptor injected");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[TabWorker] Failed to inject audio interceptor: {ex.Message}");
            }
        }

        /// <summary>
        /// Спроба отримати CefBrowserHost через рефлексію з WebViewControl
        /// </summary>
        private object? TryGetCefBrowserHost()
        {
            try
            {
                var webViewType = WebView.GetType();
                object? browser = null;
                
                // Шукаємо browser через різні назви властивостей/полів
                string[] browserNames = { "Browser", "_browser", "browser", "chromiumBrowser", "_chromiumBrowser", 
                                          "InternalBrowser", "_internalBrowser", "CefBrowser", "_cefBrowser" };
                
                foreach (var name in browserNames)
                {
                    var browserProp = webViewType.GetProperty(name, 
                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                    if (browserProp != null)
                    {
                        browser = browserProp.GetValue(WebView);
                        if (browser != null)
                        {
                            Debug.WriteLine($"[TabWorker] Found browser via property '{name}'");
                            break;
                        }
                    }
                    
                    var browserField = webViewType.GetField(name, 
                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                    if (browserField != null)
                    {
                        browser = browserField.GetValue(WebView);
                        if (browser != null)
                        {
                            Debug.WriteLine($"[TabWorker] Found browser via field '{name}'");
                            break;
                        }
                    }
                }
                
                // Якщо не знайшли за назвою, шукаємо за типом
                if (browser == null)
                {
                    foreach (var field in webViewType.GetFields(System.Reflection.BindingFlags.Instance | 
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic))
                    {
                        try
                        {
                            var val = field.GetValue(WebView);
                            if (val != null)
                            {
                                var typeName = val.GetType().FullName ?? "";
                                if (typeName.Contains("CefBrowser") || typeName.Contains("Chromium") || typeName.Contains("Browser"))
                                {
                                    browser = val;
                                    Debug.WriteLine($"[TabWorker] Found browser via field type pattern '{field.Name}', type: {typeName}");
                                    break;
                                }
                            }
                        }
                        catch { }
                    }
                }

                if (browser == null)
                {
                    Debug.WriteLine("[TabWorker] Browser object not found in WebView");
                    return null;
                }

                // Отримуємо Host з browser
                var browserType = browser.GetType();
                object? host = null;
                
                // Шукаємо Host через різні назви
                string[] hostNames = { "Host", "BrowserHost", "_host", "_browserHost" };
                
                foreach (var name in hostNames)
                {
                    var hostProp = browserType.GetProperty(name, 
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (hostProp != null)
                    {
                        host = hostProp.GetValue(browser);
                        if (host != null)
                        {
                            Debug.WriteLine($"[TabWorker] Found host via property '{name}'");
                            break;
                        }
                    }
                }
                
                // Спробуємо метод GetHost
                if (host == null)
                {
                    var hostMethod = browserType.GetMethod("GetHost", 
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                    if (hostMethod != null)
                    {
                        host = hostMethod.Invoke(browser, null);
                        if (host != null)
                        {
                            Debug.WriteLine("[TabWorker] Found host via GetHost() method");
                        }
                    }
                }

                return host;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[TabWorker] TryGetCefBrowserHost failed: {ex.Message}");
                return null;
            }
        }


        private async void ApplyMuteState()
        {
            try
            {
                Debug.WriteLine($"[TabWorker] ApplyMuteState called, _isMuted={_isMuted}, _relatedPids.Count={_relatedPids.Count}");
                
                bool applied = false;
                
                // Пріоритет 1: CEF native API через рефлексію (найнадійніший метод)
                try
                {
                    var cefBrowserHost = TryGetCefBrowserHost();
                    if (cefBrowserHost != null)
                    {
                        var setAudioMutedMethod = cefBrowserHost.GetType().GetMethod("SetAudioMuted", 
                            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                        
                        if (setAudioMutedMethod != null)
                        {
                            setAudioMutedMethod.Invoke(cefBrowserHost, new object[] { _isMuted });
                            applied = true;
                            Debug.WriteLine($"[TabWorker] Audio {(_isMuted ? "muted" : "unmuted")} via CEF native API");
                        }
                        else
                        {
                            Debug.WriteLine("[TabWorker] SetAudioMuted method not found on CefBrowserHost");
                        }
                    }
                    else
                    {
                        Debug.WriteLine("[TabWorker] CefBrowserHost is null");
                    }
                }
                catch (Exception cefEx)
                {
                    Debug.WriteLine($"[TabWorker] CEF native mute failed: {cefEx.Message}");
                }

                // Пріоритет 2: Windows Audio Session API
                if (!applied && RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && _relatedPids.Count > 0)
                {
                    var pidsCopy = _relatedPids.ToList();
                    foreach (var pid in pidsCopy)
                    {
                        try
                        {
                            if (WindowsAudioSessionService.TrySetProcessMute(pid, _isMuted))
                            {
                                applied = true;
                            }
                        }
                        catch { }
                    }
                    
                    if (applied)
                    {
                        Debug.WriteLine($"[TabWorker] Audio {(_isMuted ? "muted" : "unmuted")} at system level for PIDs: {string.Join(",", pidsCopy)}");
                    }
                }

                // Пріоритет 3: JavaScript для control всіх media елементів + Web Audio API
                // Завжди виконуємо JavaScript як додатковий захист
                try
                {
                    var js = _isMuted
                        ? @"(function(){
                            try {
                                // Mute all video and audio elements
                                document.querySelectorAll('video,audio').forEach(function(m) {
                                    m.muted = true;
                                    m.volume = 0;
                                });
                                
                                // Initialize tracking array if not exists
                                if (!window._vetaleAudioContexts) {
                                    window._vetaleAudioContexts = [];
                                }
                                
                                // Suspend all tracked audio contexts
                                window._vetaleAudioContexts.forEach(function(ctx) { 
                                    try { 
                                        if (ctx && ctx.state !== 'closed') {
                                            ctx.suspend(); 
                                        }
                                    } catch(e){} 
                                });
                                
                                // Hook AudioContext to track new instances and auto-suspend them
                                if (window.AudioContext && !window._vetaleOrigAudioContext) {
                                    window._vetaleOrigAudioContext = window.AudioContext;
                                    window.AudioContext = function() {
                                        var ctx = new window._vetaleOrigAudioContext();
                                        window._vetaleAudioContexts.push(ctx);
                                        if (window._vetaleAudioMuted) {
                                            ctx.suspend();
                                        }
                                        return ctx;
                                    };
                                    window.AudioContext.prototype = window._vetaleOrigAudioContext.prototype;
                                }
                                if (window.webkitAudioContext && !window._vetaleOrigWebkitAudioContext) {
                                    window._vetaleOrigWebkitAudioContext = window.webkitAudioContext;
                                    window.webkitAudioContext = function() {
                                        var ctx = new window._vetaleOrigWebkitAudioContext();
                                        window._vetaleAudioContexts.push(ctx);
                                        if (window._vetaleAudioMuted) {
                                            ctx.suspend();
                                        }
                                        return ctx;
                                    };
                                    window.webkitAudioContext.prototype = window._vetaleOrigWebkitAudioContext.prototype;
                                }
                                
                                // Set muted flag
                                window._vetaleAudioMuted = true;
                                
                                // Also try to find and mute gain nodes (common pattern in games)
                                if (window._vetaleGainNodes) {
                                    window._vetaleGainNodes.forEach(function(g) { 
                                        try { g.gain.value = 0; } catch(e){} 
                                    });
                                }
                                
                            } catch(e) { console.log('[VetaleBrowser] Mute error:', e); }
                        })();"
                        : @"(function(){
                            try {
                                // Unmute all video and audio elements
                                document.querySelectorAll('video,audio').forEach(function(m) {
                                    m.muted = false;
                                    if (m.volume === 0) m.volume = 1.0;
                                });
                                
                                // Resume all tracked audio contexts
                                if (window._vetaleAudioContexts) {
                                    window._vetaleAudioContexts.forEach(function(ctx) { 
                                        try { 
                                            if (ctx && ctx.state !== 'closed') {
                                                ctx.resume(); 
                                            }
                                        } catch(e){} 
                                    });
                                }
                                
                                // Clear muted flag
                                window._vetaleAudioMuted = false;
                                
                                // Restore gain nodes
                                if (window._vetaleGainNodes) {
                                    window._vetaleGainNodes.forEach(function(g) { 
                                        try { g.gain.value = 1; } catch(e){} 
                                    });
                                }
                                
                            } catch(e) { console.log('[VetaleBrowser] Unmute error:', e); }
                        })();";
                    
                    await WebView.EvaluateScript<object>(js);
                    Debug.WriteLine($"[TabWorker] Audio state applied via JavaScript: {_isMuted}");
                }
                catch (Exception jsEx)
                {
                    Debug.WriteLine($"[TabWorker] JS mute failed: {jsEx.Message}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[TabWorker] Failed to apply mute state: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Планує відкладене повторне застосування mute після навігації.
        /// Викликається кілька разів з інтервалом для гарантії що mute працює для динамічного контенту.
        /// </summary>
        private void ScheduleMuteReapply()
        {
            _muteReapplyCount = 4; // Застосуємо mute 4 рази з інтервалом 500ms
            _muteReapplyTimer.Stop();
            _muteReapplyTimer.Tick -= MuteReapplyTick;
            _muteReapplyTimer.Tick += MuteReapplyTick;
            _muteReapplyTimer.Start();
        }
        
        private void MuteReapplyTick(object? sender, EventArgs e)
        {
            if (_muteReapplyCount <= 0)
            {
                _muteReapplyTimer.Stop();
                _muteReapplyTimer.Tick -= MuteReapplyTick;
                return;
            }
            
            _muteReapplyCount--;
            
            if (_isMuted)
            {
                // Оновлюємо PID-и для нових процесів що могли з'явитись
                RefreshRelatedProcessesBestEffort();
                ApplyMuteState();
            }
            else
            {
                // Mute було вимкнено - зупиняємо таймер
                _muteReapplyTimer.Stop();
                _muteReapplyTimer.Tick -= MuteReapplyTick;
            }
        }

        private void TryUpdateSubprocessTitle()
        {
            try
            {
                // Direct access to Title property
                string? siteTitle = WebView.Title;
                
                if (string.IsNullOrWhiteSpace(siteTitle))
                {
                    // Fallback to host from Address
                    var addr = WebView.Address;
                    if (!string.IsNullOrWhiteSpace(addr) && Uri.TryCreate(addr, UriKind.Absolute, out var uri))
                    {
                        siteTitle = uri.Host;
                        if (siteTitle.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
                            siteTitle = siteTitle.Substring(4);
                    }
                }

                if (string.IsNullOrWhiteSpace(siteTitle))
                {
                    siteTitle = "New Tab";
                }

                _subprocess.UpdateTitle($"Tab: {siteTitle}");
            }
            catch { }
        }

        private async Task TryInterceptDownloadAsync(string? url, string? previous)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(url)) return;
                if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return;
                if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) return;

                if (!ShouldTreatAsDownloadByExtension(uri))
                {
                    try
                    {
                        using var req = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Head, uri);
                        using var resp = await _httpHead.SendAsync(req);
                        if (!resp.IsSuccessStatusCode) return;
                        var cd = resp.Content.Headers.ContentDisposition;
                        var isAttachment = cd != null && string.Equals(cd.DispositionType, "attachment", StringComparison.OrdinalIgnoreCase);
                        if (!isAttachment) return;
                    }
                    catch { return; }
                }

                var suggested = System.IO.Path.GetFileName(uri.LocalPath);
                _ = VetaleBrowser.Core.Scripts.GlobalManagers.DownloadManager.StartDownloadAsync(url, suggested);

                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    try
                    {
                        _blockingDownloadNav = true;
                        if (WebView.CanGoBack)
                        {
                            WebView.GoBack();
                        }
                        else if (!string.IsNullOrWhiteSpace(previous))
                        {
                            WebView.Address = previous!;
                        }
                    }
                    finally
                    {
                        _blockingDownloadNav = false;
                    }
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TabWorker] TryInterceptDownloadAsync error: {ex.Message}");
            }
        }

        private static bool ShouldTreatAsDownloadByExtension(Uri uri)
        {
            try
            {
                var path = uri.LocalPath.ToLowerInvariant();
                string[] exts = new[]
                {
                    ".zip", ".7z", ".rar", ".tar", ".gz", ".bz2",
                    ".exe", ".msi", ".iso",
                    ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx",
                    ".mp3", ".mp4", ".mkv", ".avi", ".mov",
                    ".png", ".jpg", ".jpeg", ".gif", ".webp",
                    ".apk"
                };
                foreach (var ext in exts)
                {
                    if (path.EndsWith(ext)) return true;
                }
                return false;
            }
            catch { return false; }
        }

        public void Dispose()
        {
            try
            {
                WebView.PropertyChanged -= WebViewOnPropertyChanged;
                
                // Dispose error handler
                if (ErrorHandler != null)
                {
                    ErrorHandler.ErrorOccurred -= OnErrorHandlerError;
                    ErrorHandler.Dispose();
                }
                
                Manager.Dispose();
                WebView.Dispose();
                _fullscreenPollTimer.Stop();
                _pidRefreshTimer.Stop();
                _muteReapplyTimer.Stop();
                
                // Release claimed PIDs so other tabs or future workers can reuse if processes persist
                TabProcessTracker.ReleasePids(_relatedPids);

                // Stop subprocess
                _subprocess.Dispose();
            }
            catch
            {
                // best-effort
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
