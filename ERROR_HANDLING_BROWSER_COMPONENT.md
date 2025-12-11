# ErrorHandlingBrowserComponent

## Опис

`ErrorHandlingBrowserComponent` — це Avalonia UserControl, який інкапсулює логіку перехоплення помилок CefGlue WebView та автоматичного відображення існуючої сторінки помилки `BrowserErrorPage`.

## Як працює виявлення помилок

Система використовує кілька механізмів для виявлення помилок:

1. **Прямі події CefGlue** (через reflection):
   - `LoadError` — помилки завантаження сторінки (DNS, timeout, HTTP тощо)
   - `UnhandledException` — необроблені .NET винятки
   - `JavascriptUncaughtException` — JS помилки
   - `ConsoleMessage` — помилки консолі

2. **Fallback через PropertyChanged**:
   - Виявлення `chrome-error://` URL в Address
   - Виявлення `ERR_` кодів в Title (напр. `ERR_NAME_NOT_RESOLVED`)
   - Виявлення типових текстів помилок в Title (напр. "This site can't be reached")

3. **PreCheck через HTTP HEAD** (в TabWorker):
   - Перевірка доступності URL перед навігацією

## Тестування

Для тестування введіть в адресний рядок:
- `https://asdfasdfasdfasdf.com` — для помилки DNS
- `https://expired.badssl.com/` — для помилки сертифіката
- `http://localhost:99999` — для помилки з'єднання

Після натискання Enter, має з'явитися сторінка помилки з локалізованим описом.

## Особливості

- **Автоматичне перехоплення помилок**: Використовує `WebViewErrorHandler` для захоплення всіх типів помилок:
  - `LoadError` — помилки завантаження сторінки (DNS, timeout, HTTP тощо)
  - `UnhandledException` — необроблені .NET винятки
  - `JavascriptUncaughtException` — JS помилки
  - `ConsoleMessage` — помилки консолі

- **Інтеграція з BrowserErrorPage**: Автоматично показує локалізовану сторінку помилки з:
  - Заголовком та описом помилки
  - Підказками для вирішення
  - Кнопками "Спробувати знову", "Назад", "На головну"
  - Пошуковим полем

- **Плавне перемикання**: Автоматично перемикає видимість між WebView та сторінкою помилки

## Використання

### Спосіб 1: Програмне встановлення WebView

```csharp
using VetaleBrowser.VetaleBrowser.UI.Controls;
using WebViewControl;

// Створення компоненту
var errorBrowser = new ErrorHandlingBrowserComponent();

// Встановлення існуючого WebView
var webView = new WebView();
errorBrowser.SetWebView(webView);

// Навігація
errorBrowser.Navigate("https://example.com");

// Додавання на сторінку
myGrid.Children.Add(errorBrowser);
```

### Спосіб 2: Передача WebView в конструктор

```csharp
var webView = new WebView();
var errorBrowser = new ErrorHandlingBrowserComponent(webView);
errorBrowser.Navigate("https://example.com");
```

### Спосіб 3: В XAML

```xml
<Window xmlns="https://github.com/avaloniaui"
        xmlns:controls="using:VetaleBrowser.VetaleBrowser.UI.Controls">
    
    <controls:ErrorHandlingBrowserComponent x:Name="ErrorBrowser"/>
    
</Window>
```

```csharp
// В code-behind
var webView = new WebView();
ErrorBrowser.SetWebView(webView);
ErrorBrowser.Navigate("https://example.com");
```

## Події

### ErrorOccurred
Виникає при помилці. Дозволяє обробити помилку зовні.

```csharp
errorBrowser.ErrorOccurred += (sender, e) =>
{
    Debug.WriteLine($"Помилка: {e.Error.Title}");
    
    // Якщо хочете запобігти показу стандартної сторінки помилки:
    // e.Handled = true;
};
```

### RetryRequested
Виникає коли користувач натискає "Спробувати знову".

```csharp
errorBrowser.RetryRequested += (sender, e) =>
{
    // Власна логіка повторної спроби
    myWorker.Navigate(lastUrl);
};
```

### GoBackRequested
Виникає коли користувач натискає "Назад".

```csharp
errorBrowser.GoBackRequested += (sender, e) =>
{
    if (history.CanGoBack)
        history.GoBack();
};
```

### GoHomeRequested
Виникає коли користувач натискає "На головну".

```csharp
errorBrowser.GoHomeRequested += (sender, e) =>
{
    Navigate("vetale://search");
};
```

### SearchRequested
Виникає коли користувач шукає щось зі сторінки помилки.

```csharp
errorBrowser.SearchRequested += (sender, query) =>
{
    Navigate($"vetale://search?q={Uri.EscapeDataString(query)}");
};
```

## Властивості

| Властивість | Тип | Опис |
|-------------|-----|------|
| `WebView` | `WebView?` | Доступ до WebView |
| `IsShowingError` | `bool` | Чи відображається сторінка помилки |
| `CurrentError` | `BrowserError?` | Поточна помилка |
| `ErrorHandler` | `WebViewErrorHandler?` | Доступ до обробника помилок |

## Методи

| Метод | Опис |
|-------|------|
| `SetWebView(WebView)` | Встановлює WebView та налаштовує обробку помилок |
| `Navigate(string url)` | Навігація до URL |
| `ShowError(BrowserError)` | Вручну показує сторінку помилки |
| `HideError()` | Приховує сторінку помилки |
| `Retry()` | Повторна спроба завантаження останнього URL |
| `Dispose()` | Звільняє ресурси |

## Інтеграція з TabWorker

Компонент можна інтегрувати з існуючим `TabWorker`:

```csharp
// В TabWorker
public ErrorHandlingBrowserComponent BrowserComponent { get; }

public TabWorker()
{
    WebView = new WebView();
    BrowserComponent = new ErrorHandlingBrowserComponent(WebView);
    
    // Налаштування обробників
    BrowserComponent.RetryRequested += (_, _) => Reload();
    BrowserComponent.GoBackRequested += (_, _) => GoBack();
    BrowserComponent.GoHomeRequested += (_, _) => Navigate("vetale://search");
    BrowserComponent.SearchRequested += (_, q) => Navigate($"vetale://search?q={q}");
}
```

## Приклад повної інтеграції

```csharp
public class BrowserTab : UserControl
{
    private ErrorHandlingBrowserComponent _browser;
    
    public BrowserTab()
    {
        _browser = new ErrorHandlingBrowserComponent(new WebView());
        
        // Обробка помилок
        _browser.ErrorOccurred += (_, e) =>
        {
            Debug.WriteLine($"Помилка: {e.Error.Title}");
        };
        
        // Обробка дій користувача
        _browser.RetryRequested += (_, _) => _browser.Retry();
        _browser.GoBackRequested += (_, _) => History.GoBack();
        _browser.GoHomeRequested += (_, _) => Navigate("vetale://search");
        _browser.SearchRequested += (_, q) => Navigate($"vetale://search?q={q}");
        
        Content = _browser;
    }
    
    public void Navigate(string url) => _browser.Navigate(url);
}
```

## Файли

- `VetaleBrowser.UI/Controls/ErrorHandlingBrowserComponent.axaml` - XAML розмітка
- `VetaleBrowser.UI/Controls/ErrorHandlingBrowserComponent.axaml.cs` - Code-behind

## Залежності

- `VetaleBrowser.Core.Scripts.ErrorHandlers.WebViewErrorHandler`
- `VetaleBrowser.Core.Scripts.ErrorHandlers.BrowserError`
- `VetaleBrowser.Core.Scripts.ErrorHandlers.BrowserErrorEventArgs`
- `VetaleBrowser.UI.Pages.BrowserErrorPage`
- `WebViewControl.WebView`

