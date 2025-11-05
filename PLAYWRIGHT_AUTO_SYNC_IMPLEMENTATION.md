# 🔄 Playwright Auto-Sync Implementation

## ⚠️ ВАЖЛИВЕ ВИПРАВЛЕННЯ (2025-11-05)

### Проблема: 4 Chromium вікна відкривалися при запуску
**Причина:** Кожна DevTools сторінка створювала свій екземпляр `PlaywrightDevToolsService` та `WebViewWorkerService`.

### Рішення: Singleton Pattern
Реалізовано Singleton для обох сервісів - тепер створюється **лише один** екземпляр Chromium для всього додатку.

#### Зміни:
1. **PlaywrightDevToolsService** - зроблено Singleton
   - Приватний конструктор
   - Статичний метод `GetInstance()`
   - Thread-safe ініціалізація

2. **WebViewWorkerService** - зроблено Singleton
   - Використовує Singleton `PlaywrightDevToolsService`
   - Один екземпляр для всіх DevTools сторінок

3. **Всі DevTools сторінки** оновлено:
   - `PlaywrightElementsPage`
   - `PlaywrightPerformancePage`
   - `PlaywrightSourcesPage`
   - `PlaywrightApplicationPage`
   - `ElementsPage`
   - `NetworkPage`
   - `SourcesPage`
   - `ApplicationPage`
   - `PerformancePage`
   - `WebViewWorkerPage`

#### Приклад використання:
```csharp
// Було (створювало 4+ Chromium)
_playwrightService = new PlaywrightDevToolsService(dataService);
_workerService = new WebViewWorkerService(dataService);

// Стало (створює 1 Chromium)
_playwrightService = PlaywrightDevToolsService.GetInstance(dataService);
_workerService = WebViewWorkerService.GetInstance(dataService);
```

### Результат:
✅ Тільки **1 Chromium вікно** відкривається при запуску (headless)  
✅ **Chromium НЕ видимий** - працює у фоновому режимі  
✅ Всі DevTools сторінки працюють коректно  
✅ Автосинхронізація з поточною вкладкою працює  
✅ Playwright headless режим (встановлено `Headless = true`)  

### Що було виправлено:
1. ❌ **Проблема:** 4 окремі Chromium вікна відкривалися одночасно
2. ❌ **Проблема:** Chromium показувався користувачу (заважав роботі)
3. ❌ **Проблема:** Інші DevTools сторінки не працювали
4. ✅ **Вирішено:** Використовується Singleton Pattern для сервісів
5. ✅ **Вирішено:** Chromium працює в headless режимі (невидимий)
6. ✅ **Вирішено:** Всі DevTools сторінки використовують один екземпляр

### Технічні деталі:
- **PlaywrightDevToolsService** - Singleton з `GetInstance()`
- **WebViewWorkerService** - Singleton з `GetInstance()`
- **Headless Mode** - `Headless = true` в `LaunchAsync()`
- **Thread-safe** - використовується lock для безпечної ініціалізації

---

## Огляд

Реалізовано автоматичну синхронізацію Playwright Chromium контейнера з поточною активною вкладкою браузера VetaleBrowser.

## Ключові зміни

### 1. Замінено локальний WebView на Playwright Chromium

#### Було:
- `WebViewWorkerPage` містив локальний WebView контрол
- Користувач вводив URL вручну
- WebView показувався у вікні DevTools

#### Стало:
- `WebViewWorkerPage` керує Playwright Chromium контейнером
- URL автоматично береться з поточної вкладки
- Playwright Chromium відкривається у видимому режимі (не headless)
- Автоматична синхронізація при зміні активної вкладки

### 2. Оновлено `WebViewWorkerPage.axaml.cs`

**Видалено:**
```csharp
private WebView? _webView;
private WebViewManager? _webViewManager;
public WebView? DevToolsLocalWebView => _webView;
```

**Додано:**
```csharp
private PlaywrightDevToolsService? _playwrightService;
private string? _currentPlaywrightUrl;
public PlaywrightDevToolsService? PlaywrightService => _playwrightService;
public string? CurrentUrl => _currentPlaywrightUrl;
```

**Ключові функції:**
- `InitializePlaywright()` - ініціалізує Playwright Chromium
- `AutoSyncWithCurrentTab()` - автоматично синхронізує з поточною вкладкою
- `NavigatePlaywrightToUrl()` - навігація Playwright до URL

**Автоматична синхронізація:**
```csharp
private void OnMonitorTick(object? sender, EventArgs e)
{
    // Перевіряє зміну активної вкладки кожні 500мс
    if (activeTab != _currentActiveTab)
    {
        UnsubscribeFromCurrentTab();
        _currentActiveTab = activeTab;
        SubscribeToCurrentTab();
        UpdateCurrentTabInfo();
        
        // Автоматично синхронізує Playwright
        _ = AutoSyncWithCurrentTab();
    }
}

private void OnCurrentTabAddressChanged(object? sender, string? address)
{
    Dispatcher.UIThread.Post(() =>
    {
        UpdateCurrentTabInfo();
        // Автоматична синхронізація при зміні URL
        _ = AutoSyncWithCurrentTab();
    });
}
```

### 3. Оновлено `WebViewWorkerService.cs`

**Видалено:**
```csharp
private WebView? _localWebView;
public void AttachLocalWebView(WebView webView) { ... }
public void DetachLocalWebView() { ... }
```

**Змінено:**
```csharp
public string CurrentUrl => _activeTab?.Address ?? "";
```

**Додано автоматичну навігацію:**
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
            
            // Автоматична навігація Playwright до URL поточної вкладки
            if (_activeTab != null && !string.IsNullOrWhiteSpace(_activeTab.Address))
            {
                _ = NavigateToCurrentTabAsync();
            }
        }
    }
}

private void OnTabNavigated(object? sender, string? url)
{
    _sessionId = Guid.NewGuid().ToString();
    // Автоматична навігація при зміні URL
    _ = NavigateToCurrentTabAsync();
}
```

### 4. Оновлено DevTools Pages

Всі DevTools сторінки тепер автоматично використовують поточну вкладку:

- `PlaywrightElementsPage` - видалено `AttachLocalWebView()`
- `PlaywrightPerformancePage` - видалено `AttachLocalWebView()`
- `PlaywrightSourcesPage` - видалено `AttachLocalWebView()`
- `PlaywrightApplicationPage` - видалено `AttachLocalWebView()`
- `ElementsPage` - спрощено до використання `ActiveTab`
- `PerformancePage` - спрощено до використання `ActiveTab`
- `NetworkPage` - спрощено до використання `ActiveTab`
- `SourcesPage` - спрощено до використання `ActiveTab`
- `ApplicationPage` - спрощено до використання `ActiveTab`

**Було:**
```csharp
private void AttachToLocalWebView()
{
    var webView = DevToolsWebViewRegistry.CurrentWebView;
    if (webView != null)
    {
        _webViewService.AttachLocalWebView(webView);
    }
}
```

**Стало:**
```csharp
private void AttachToLocalWebView()
{
    // No longer needed - service automatically uses current tab URL
    Debug.WriteLine("[Page] Using current tab URL automatically via WebViewWorkerService");
}

private void EnsureLocalWebViewAttached()
{
    // WebViewWorkerService now automatically uses current tab URL
    try
    {
        var main = GetMainWindow();
        if (main?.TabsManager?.Active != null)
        {
            _workerService.ActiveTab = main.TabsManager.Active;
        }
    }
    catch (Exception ex)
    {
        Debug.WriteLine($"[Page] Error setting active tab: {ex.Message}");
    }
}
```

## Потік роботи

### Автоматична синхронізація:

1. Користувач відкриває вкладку і переходить на сторінку
2. `WebViewWorkerPage` моніторить зміни активної вкладки (кожні 500мс)
3. При зміні вкладки або URL:
   - Оновлює UI (показує назву і URL вкладки)
   - Автоматично навігує Playwright до того ж URL
   - Генерує новий SessionID для DevTools
4. DevTools сторінки автоматично отримують доступ до поточної вкладки через `WebViewWorkerService`

### Ручна навігація:

1. Користувач може ввести URL у поле "WebView Worker"
2. Натиснути "Go" або "Load Current Tab"
3. Playwright навігує до вказаного URL
4. DevTools сторінки використовують цей URL для аналізу

## Переваги

✅ **Автоматизація** - не треба вручну вводити URL  
✅ **Синхронізація** - Playwright завжди слідує за активною вкладкою  
✅ **Простота** - користувач просто відкриває DevTools і бачить аналіз поточної сторінки  
✅ **Видимість** - Playwright Chromium відкривається у видимому режимі для налагодження  
✅ **Актуальність** - дані завжди відповідають поточній вкладці  

## Компіляція

```bash
dotnet build E:\VetaleBrowser\VetaleBrowser.sln
```

**Результат**: ✅ Build successful (14 warnings - це норма)

## Тестування

1. Запустіть VetaleBrowser
2. Відкрийте будь-яку вкладку (наприклад, google.com)
3. Натисніть F12 (DevTools)
4. Перейдіть на вкладку "WebView Worker" - побачите інформацію про поточну вкладку
5. Playwright Chromium автоматично відкриється і покаже ту ж сторінку
6. Перейдіть на "Playwright Elements" і натисніть "Capture DOM" - отримаєте DOM поточної сторінки
7. Змініть активну вкладку - Playwright автоматично синхронізується

## Файли змінено

### Сервіси (зроблено Singleton):
1. `VetaleBrowser/VetaleBrowser.DevTools/Services/PlaywrightDevToolsService.cs` - Singleton, headless режим
2. `VetaleBrowser/VetaleBrowser.DevTools/Services/WebViewWorkerService.cs` - Singleton, використовує PlaywrightDevToolsService.GetInstance()

### DevTools сторінки (використовують Singleton):
3. `VetaleBrowser/VetaleBrowser.DevTools/Pages/WebViewWorkerPage.axaml.cs` - основна логіка Playwright
4. `VetaleBrowser/VetaleBrowser.DevTools/Pages/PlaywrightElementsPage.axaml.cs` - використовує GetInstance()
5. `VetaleBrowser/VetaleBrowser.DevTools/Pages/PlaywrightPerformancePage.axaml.cs` - використовує GetInstance()
6. `VetaleBrowser/VetaleBrowser.DevTools/Pages/PlaywrightSourcesPage.axaml.cs` - використовує GetInstance()
7. `VetaleBrowser/VetaleBrowser.DevTools/Pages/PlaywrightApplicationPage.axaml.cs` - використовує GetInstance()
8. `VetaleBrowser/VetaleBrowser.DevTools/Pages/ElementsPage.axaml.cs` - використовує GetInstance()
9. `VetaleBrowser/VetaleBrowser.DevTools/Pages/PerformancePage.axaml.cs` - використовує GetInstance()
10. `VetaleBrowser/VetaleBrowser.DevTools/Pages/NetworkPage.axaml.cs` - використовує GetInstance()
11. `VetaleBrowser/VetaleBrowser.DevTools/Pages/SourcesPage.axaml.cs` - використовує GetInstance()
12. `VetaleBrowser/VetaleBrowser.DevTools/Pages/ApplicationPage.axaml.cs` - використовує GetInstance()

### Документація:
13. `PLAYWRIGHT_AUTO_SYNC_IMPLEMENTATION.md` - оновлена документація з описом Singleton

## Наступні кроки

- [x] Виправити 4 Chromium вікна → використано Singleton Pattern
- [x] Chromium в headless режимі → `Headless = true`
- [x] Виправити функціонал інших DevTools сторінок → всі використовують GetInstance()
- [ ] Додати опцію toggle для headless/visible режиму (за потреби)
- [ ] Додати можливість вимкнути автосинхронізацію
- [ ] Додати історію навігації у Playwright браузері
- [ ] Оптимізувати частоту моніторингу (зараз 500мс)
- [ ] Можливість вбудувати скріншот Chromium в DevTools UI (замість вікна)

