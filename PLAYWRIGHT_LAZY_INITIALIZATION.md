# 🚀 Playwright Lazy Initialization (Ініціалізація на вимогу)

## Проблема

Раніше Playwright Chromium запускався автоматично при відкритті DevTools, що призводило до:
- ❌ Зайвих процесів Chromium при старті
- ❌ Витрати ресурсів без потреби
- ❌ Автоматичної синхронізації, яка не завжди потрібна

## Рішення

Реалізовано **Lazy Initialization** (ліниву ініціалізацію) - Playwright запускається **тільки** коли користувач натискає кнопку для завантаження URL або захоплення даних.

## Що змінено

### 1. `WebViewWorkerPage.axaml.cs`

**Було:**
```csharp
public WebViewWorkerPage()
{
    InitializeComponent();
    InitializeControls();
    InitializePlaywright(); // ❌ Автоматично запускав Playwright
    StartMonitoring();
}
```

**Стало:**
```csharp
public WebViewWorkerPage()
{
    InitializeComponent();
    InitializeControls();
    // ✅ Не запускаємо Playwright автоматично
    StartMonitoring();
}
```

**Ініціалізація при навігації:**
```csharp
private async Task NavigatePlaywrightToUrl(string url)
{
    try
    {
        // ✅ Ініціалізуємо Playwright при першому використанні
        if (_playwrightService == null)
        {
            UpdateStatus("⏳", "Initializing Playwright Chromium...");
            _playwrightService = PlaywrightDevToolsService.GetInstance(...);
            await _playwrightService.InitializeAsync();
        }

        // Навігація до URL
        await _playwrightService.NavigateAsync(url);
    }
    catch (Exception ex)
    {
        UpdateStatus("❌", $"Error: {ex.Message}");
    }
}
```

**Видалено автосинхронізацію:**
```csharp
private void OnMonitorTick(object? sender, EventArgs e)
{
    if (activeTab != _currentActiveTab)
    {
        UnsubscribeFromCurrentTab();
        _currentActiveTab = activeTab;
        SubscribeToCurrentTab();
        UpdateCurrentTabInfo();
        // ❌ Видалено: _ = AutoSyncWithCurrentTab();
    }
}

private void OnCurrentTabAddressChanged(object? sender, string? address)
{
    Dispatcher.UIThread.Post(() =>
    {
        UpdateCurrentTabInfo();
        // ❌ Видалено: _ = AutoSyncWithCurrentTab();
    });
}
```

### 2. `WebViewWorkerService.cs`

**Було:**
```csharp
private WebViewWorkerService(DevToolsDataService dataService)
{
    _dataService = dataService;
    _sessionId = Guid.NewGuid().ToString();
    _playwrightService = PlaywrightDevToolsService.GetInstance(_dataService);
    _ = InitializePlaywrightAsync(); // ❌ Автоматична ініціалізація
}
```

**Стало:**
```csharp
private WebViewWorkerService(DevToolsDataService dataService)
{
    _dataService = dataService;
    _sessionId = Guid.NewGuid().ToString();
    _playwrightService = PlaywrightDevToolsService.GetInstance(_dataService);
    // ✅ Не запускаємо Playwright автоматично
}

public async Task EnsureInitializedAsync()
{
    if (_playwrightService == null)
        return;

    try
    {
        await _playwrightService.InitializeAsync();
        Debug.WriteLine("[WebViewWorkerService] Playwright initialized on demand");
    }
    catch (Exception ex)
    {
        Debug.WriteLine($"[WebViewWorkerService] Failed to initialize: {ex.Message}");
        throw;
    }
}
```

**Видалено автонавігацію:**
```csharp
public TabWorker? ActiveTab
{
    get => _activeTab;
    set
    {
        if (_activeTab != value)
        {
            UnsubscribeFromTab(_activeTab);
            _activeTab = value;
            SubscribeToTab(_activeTab);
            _sessionId = Guid.NewGuid().ToString();
            // ❌ Видалено: _ = NavigateToCurrentTabAsync();
        }
    }
}

private void OnTabNavigated(object? sender, string? url)
{
    _sessionId = Guid.NewGuid().ToString();
    Debug.WriteLine($"[WebViewWorkerService] Tab navigated to: {url}");
    // ❌ Видалено: _ = NavigateToCurrentTabAsync();
}
```

**Додано ініціалізацію в методах захоплення:**
```csharp
public async Task<List<DomElement>> CaptureDomStructureAsync()
{
    // ... перевірки URL ...
    
    try
    {
        // ✅ Ініціалізуємо перед використанням
        await EnsureInitializedAsync();
        
        Debug.WriteLine($"[WebViewWorkerService] Capturing DOM for: {currentUrl}");
        await _playwrightService.NavigateAsync(currentUrl);
        return await _playwrightService.CaptureDomStructureAsync();
    }
    catch (Exception ex)
    {
        Debug.WriteLine($"[WebViewWorkerService] DOM capture failed: {ex.Message}");
        return new List<DomElement>();
    }
}

// Аналогічно для:
// - CapturePerformanceSnapshotAsync()
// - CapturePageResourcesAsync()
// - CaptureStorageAsync()
```

## Як працює зараз

### Сценарій 1: Користувач відкриває DevTools

1. ✅ DevTools вікно відкривається
2. ✅ UI елементи ініціалізуються
3. ✅ Моніторинг поточної вкладки працює
4. ❌ **Playwright НЕ запускається**
5. ❌ **Chromium процес НЕ створюється**

### Сценарій 2: Користувач натискає кнопку "Load Current Tab" або "Go"

1. Натискання кнопки → `NavigatePlaywrightToUrl(url)`
2. **Перша перевірка**: чи ініціалізований Playwright?
3. **Якщо НІ**: 
   - Показати статус "⏳ Initializing Playwright Chromium..."
   - Створити singleton instance
   - Запустити Chromium браузер
   - Показати статус "✅ Playwright Chromium готовий"
4. **Якщо ТАК**: перейти до наступного кроку
5. Навігація до URL
6. Показати статус "✅ Loaded successfully"

### Сценарій 3: Користувач натискає "Capture DOM" на вкладці Elements

1. Натискання кнопки → `CaptureDomStructureAsync()`
2. Перевірка URL поточної вкладки
3. **Перша перевірка**: чи ініціалізований Playwright?
4. **Якщо НІ**: 
   - `await EnsureInitializedAsync()`
   - Запустити Chromium
5. **Якщо ТАК**: перейти до наступного кроку
6. Навігація до URL поточної вкладки
7. Захоплення DOM структури
8. Збереження в базу даних
9. Відображення в UI

Аналогічно для Performance, Sources, Application вкладок.

## Переваги

✅ **Економія ресурсів** - Chromium НЕ запускається якщо не потрібен  
✅ **Швидший старт** - DevTools відкривається миттєво  
✅ **Контрольований запуск** - користувач сам вирішує коли запустити аналіз  
✅ **Без автосинхронізації** - Playwright НЕ слідує за вкладками автоматично  
✅ **Singleton pattern** - тільки один Chromium процес на всю програму  

## Як використовувати

### Варіант 1: Вкладка "WebView Worker"

1. Натисніть F12 (DevTools)
2. Перейдіть на вкладку "WebView Worker"
3. Побачите інформацію про поточну вкладку, але Playwright ще НЕ запущений
4. Натисніть кнопку "📥 Load Current Tab" або введіть URL і натисніть "🚀 Go"
5. **Тепер** Playwright ініціалізується і відкриє Chromium браузер
6. Можете використовувати інші вкладки DevTools

### Варіант 2: Прямо з Playwright вкладок

1. Натисніть F12 (DevTools)
2. Перейдіть на вкладку "Playwright Elements" (або Performance/Sources/Application)
3. Натисніть кнопку "Capture DOM" (або відповідну кнопку)
4. **Тепер** Playwright ініціалізується автоматично
5. Відбудеться захоплення даних з поточної вкладки

## Компіляція

```bash
dotnet build E:\VetaleBrowser\VetaleBrowser.sln
```

**Результат**: ✅ Build successful (14 warnings)

## Файли змінено

1. `VetaleBrowser/VetaleBrowser.DevTools/Pages/WebViewWorkerPage.axaml.cs`
   - Видалено автоініціалізацію Playwright
   - Додано lazy initialization в `NavigatePlaywrightToUrl()`
   - Видалено `AutoSyncWithCurrentTab()` метод
   - Видалено автосинхронізацію при зміні вкладки/URL

2. `VetaleBrowser/VetaleBrowser.DevTools/Services/WebViewWorkerService.cs`
   - Видалено `InitializePlaywrightAsync()` з конструктора
   - Додано публічний метод `EnsureInitializedAsync()`
   - Видалено `NavigateToCurrentTabAsync()` метод
   - Видалено автонавігацію при зміні `ActiveTab`
   - Додано виклики `EnsureInitializedAsync()` в усіх методах захоплення

## Тестування

### Тест 1: Відкриття DevTools

1. ✅ Запустіть VetaleBrowser
2. ✅ Натисніть F12
3. ✅ DevTools відкриється миттєво
4. ✅ **Перевірте Task Manager**: НЕ має бути процесу Chromium

### Тест 2: Завантаження URL вручну

1. ✅ В DevTools перейдіть на "WebView Worker"
2. ✅ Натисніть "Load Current Tab"
3. ✅ Статус покаже "⏳ Initializing Playwright Chromium..."
4. ✅ **Перевірте Task Manager**: з'являється процес Chromium
5. ✅ Статус змінюється на "🔄 Navigating..."
6. ✅ Статус змінюється на "✅ Loaded successfully"
7. ✅ Chromium браузер відкривається і показує сторінку

### Тест 3: Захоплення DOM

1. ✅ Відкрийте будь-яку сторінку (наприклад google.com)
2. ✅ Натисніть F12
3. ✅ Перейдіть на "Playwright Elements"
4. ✅ Натисніть "Capture DOM"
5. ✅ **Перше використання**: Playwright ініціалізується автоматично
6. ✅ DOM структура захоплюється і відображається в дереві
7. ✅ **Друге використання**: Playwright вже ініціалізований, працює швидше

### Тест 4: Зміна вкладок

1. ✅ Відкрийте дві вкладки (google.com і github.com)
2. ✅ Натисніть F12
3. ✅ Перейдіть на "WebView Worker"
4. ✅ **НЕ натискайте** кнопку Load
5. ✅ Переключайтеся між вкладками
6. ✅ **Перевірте Task Manager**: процес Chromium НЕ запускається
7. ✅ UI показує інформацію про поточну вкладку, але Playwright НЕ працює

## Підсумок

Тепер Playwright Chromium працює **на вимогу** (on-demand):
- ❌ НЕ запускається автоматично при відкритті DevTools
- ❌ НЕ слідує за вкладками автоматично
- ✅ Запускається ТІЛЬКИ при натисканні кнопки користувачем
- ✅ Singleton pattern - один Chromium на всю програму
- ✅ Економить ресурси і прискорює роботу

**Користувач отримує повний контроль над тим, коли запускати Playwright!** 🎯

