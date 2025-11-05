# DevTools Автоматична Синхронізація з Вкладками

## Огляд

Всі сторінки моніторингу DevTools тепер автоматично використовують URL поточної активної вкладки браузера та виконують свій функціонал незалежно через headless Playwright.

## Реалізовані Зміни

### 1. WebViewWorkerService - Автоматична Синхронізація

Додано метод `SyncWithMainWindow()` у `WebViewWorkerService.cs`:

```csharp
public void SyncWithMainWindow()
{
    // Автоматично знаходить MainWindow
    // Отримує поточну активну вкладку через TabsManager
    // Встановлює її як ActiveTab для сервісу
}
```

**Переваги:**
- Не потрібно вручну передавати посилання на вкладку
- Автоматично відстежує активну вкладку
- Використовує reflection для гнучкості

### 2. Оновлені Сторінки Моніторингу

Кожна сторінка DevTools тепер працює за наступною схемою:

#### ApplicationPage (Сховище)
```csharp
private async void CaptureStorageFromWebView(object? sender, RoutedEventArgs e)
{
    // 1. Автоматична синхронізація з поточною вкладкою
    _workerService?.SyncWithMainWindow();
    
    // 2. Отримання URL з поточної вкладки
    var url = _workerService?.CurrentUrl;
    
    // 3. Виконання функціоналу через headless Playwright
    var items = await _workerService.CaptureStorageAsync();
}
```

#### ElementsPage (DOM Структура)
```csharp
private async void CaptureDomFromWebView(object? sender, RoutedEventArgs e)
{
    _workerService?.SyncWithMainWindow();
    var url = _workerService?.CurrentUrl;
    _domElements = await _workerService.CaptureDomStructureAsync();
}
```

#### NetworkPage (Мережеві Ресурси)
```csharp
private async void CaptureNetworkFromWebView(object? sender, RoutedEventArgs e)
{
    _workerService?.SyncWithMainWindow();
    var url = _workerService?.CurrentUrl;
    _networkResources = await _workerService.CapturePageResourcesAsync();
}
```

#### PerformancePage (Продуктивність)
```csharp
private async void CapturePerformanceFromWebView(object? sender, RoutedEventArgs e)
{
    _workerService?.SyncWithMainWindow();
    var url = _workerService?.CurrentUrl;
    var snapshot = await _workerService.CapturePerformanceSnapshotAsync();
}
```

#### SourcesPage (Джерела)
```csharp
private async void CaptureSourcesFromWebView(object? sender, RoutedEventArgs e)
{
    _workerService?.SyncWithMainWindow();
    var url = _workerService?.CurrentUrl;
    var list = await _workerService.CapturePageResourcesAsync();
}
```

### 3. Playwright Сторінки

Всі Playwright-версії сторінок також оновлені:

- **PlaywrightApplicationPage** - автоматична синхронізація для Storage
- **PlaywrightElementsPage** - автоматична синхронізація для DOM
- **PlaywrightPerformancePage** - автоматична синхронізація для Performance
- **PlaywrightSourcesPage** - автоматична синхронізація для Sources

## Як це Працює

### Схема Роботи

```
1. Користувач відкриває вкладку браузера (наприклад: https://example.com)
   ↓
2. Користувач відкриває DevTools → Elements Page
   ↓
3. Натискає "Capture DOM"
   ↓
4. SyncWithMainWindow() автоматично:
   - Знаходить MainWindow
   - Отримує TabsManager.Active
   - Встановлює ActiveTab
   ↓
5. CurrentUrl повертає "https://example.com"
   ↓
6. Headless Playwright:
   - Відкриває сторінку у фоновому режимі
   - Виконує аналіз (DOM, Performance, Storage, etc.)
   - Зберігає результати у базу даних
   ↓
7. Результати відображаються на сторінці DevTools
```

### Незалежність Виконання

Кожна сторінка DevTools виконує свій функціонал повністю незалежно:

- **Не потребує** WebView на сторінці DevTools
- **Не залежить** від видимого контенту вкладки
- **Використовує** headless Playwright для аналізу
- **Працює** паралельно з основним браузером

## Видалені Методи

З усіх сторінок видалено застарілі методи:

```csharp
// ❌ ВИДАЛЕНО
private void EnsureLocalWebViewAttached()
{
    var main = GetMainWindow();
    if (main?.TabsManager?.Active != null)
    {
        _workerService.ActiveTab = main.TabsManager.Active;
    }
}
```

Замість цього тепер використовується один виклик:

```csharp
// ✅ НОВИЙ ПІДХІД
_workerService?.SyncWithMainWindow();
```

## Переваги Нового Підходу

### 1. Простота Використання
- Один метод замість багатьох рядків коду
- Автоматична синхронізація
- Немає необхідності вручну керувати посиланнями

### 2. Надійність
- Завжди працює з актуальною вкладкою
- Не залежить від WebView на сторінці DevTools
- Ізольоване виконання через headless Playwright

### 3. Незалежність
- Кожна сторінка працює самостійно
- Паралельне виконання можливе
- Не впливає на роботу основного браузера

### 4. Масштабованість
- Легко додати нові сторінки моніторингу
- Єдиний шаблон для всіх сторінок
- Централізована логіка в WebViewWorkerService

## Приклад Використання

### Користувацький Сценарій

1. **Користувач** відкриває сайт у вкладці браузера
2. **Користувач** відкриває DevTools (Tools → DevTools)
3. **Користувач** переходить на будь-яку вкладку моніторингу:
   - Elements - аналіз DOM структури
   - Performance - аналіз продуктивності
   - Application - перегляд сховища
   - Sources - перегляд ресурсів
   - Network - аналіз мережі
4. **Користувач** натискає кнопку захоплення (Capture/Analyze)
5. **Система** автоматично:
   - Синхронізується з поточною вкладкою
   - Отримує URL
   - Запускає headless Playwright
   - Виконує аналіз
   - Відображає результати

### Технічний Сценарій

```csharp
// Приклад: Захоплення Storage з поточної вкладки
private async void OnCaptureStorageClick(object? sender, RoutedEventArgs e)
{
    // Автоматична синхронізація - знаходить активну вкладку
    _workerService?.SyncWithMainWindow();
    
    // Отримує URL (наприклад: "https://github.com")
    var url = _workerService?.CurrentUrl;
    
    if (string.IsNullOrEmpty(url))
    {
        Debug.WriteLine("No active tab");
        return;
    }
    
    // Запускає headless Playwright для аналізу
    // Playwright відкриває https://github.com у фоновому режимі
    // Збирає localStorage, sessionStorage, cookies
    var items = await _workerService.CaptureStorageAsync();
    
    // Відображає результати
    DisplayStorageItems(items);
}
```

## Сумісність

- ✅ Працює з усіма типами вкладок
- ✅ Підтримує множинні вкладки
- ✅ Автоматично перемикається при зміні активної вкладки
- ✅ Не впливає на існуючий функціонал браузера

## Налагодження

Для відстеження роботи автоматичної синхронізації використовуйте Debug Output:

```
[WebViewWorkerService] Synced with active tab: Example Page Title
[ApplicationPage] Capturing storage from URL: https://example.com (headless Playwright)
[PlaywrightDevToolsService] Navigating to: https://example.com
[PlaywrightDevToolsService] Captured 15 storage items
```

## Майбутні Покращення

1. **Кешування результатів** - зберігати останні результати для швидкого доступу
2. **Auto-refresh** - автоматично оновлювати при зміні вкладки
3. **Batch processing** - аналіз декількох вкладок одночасно
4. **Export функціонал** - експорт результатів у файли

## Висновок

Новий підхід з автоматичною синхронізацією робить DevTools більш зручним, надійним та незалежним. Кожна сторінка моніторингу тепер працює автономно, використовуючи headless Playwright для глибокого аналізу веб-сторінок без впливу на основний браузер.

