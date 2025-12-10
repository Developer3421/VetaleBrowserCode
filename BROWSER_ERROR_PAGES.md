# Browser Error Pages - Локалізовані сторінки помилок

## ✅ Статус: Повністю реалізовано

## Огляд

Реалізовано систему локалізованих сторінок помилок браузера для VetaleBrowser. Система перехоплює помилки CefGlue/Chromium та відображає їх як фірмові, зрозумілі сторінки Avalonia UI.

## Структура файлів

```
VetaleBrowser.Core/Scripts/ErrorHandlers/
├── BrowserErrorModels.cs      # Моделі даних помилок
├── BrowserErrorService.cs     # Сервіс локалізації помилок
└── WebViewErrorHandler.cs     # Обробник помилок WebView

VetaleBrowser.UI/Pages/
├── BrowserErrorPage.axaml     # UI сторінки помилки
└── BrowserErrorPage.axaml.cs  # Код сторінки помилки
```

## Категорії помилок

Система розділяє помилки на 4 категорії з різними стилями:

### 1. Мережеві помилки (NetworkError)
- **Колір**: Помаранчевий (#FF9800)
- **Іконка**: 🌐
- **Приклади**: ERR_NAME_NOT_RESOLVED, ERR_INTERNET_DISCONNECTED, ERR_CONNECTION_REFUSED

### 2. Помилки сервера (ServerError)
- **Колір**: Червоний (#F44336)
- **Іконка**: 🖥️
- **Приклади**: ERR_EMPTY_RESPONSE, ERR_TOO_MANY_REDIRECTS, HTTP 5xx

### 3. Помилки безпеки (SecurityError)
- **Колір**: Темно-червоний (#B71C1C)
- **Іконка**: 🔒
- **Приклади**: ERR_CERT_INVALID, ERR_SSL_PROTOCOL_ERROR

### 4. Загальні помилки (GeneralError)
- **Колір**: Сірий (#607D8B)
- **Іконка**: ⚠️
- **Приклади**: ERR_ABORTED, ERR_FILE_NOT_FOUND

## Коди помилок CefGlue

### Мережеві помилки
| Код | Назва | Опис |
|-----|-------|------|
| -105 | ERR_NAME_NOT_RESOLVED | DNS не знайдено |
| -106 | ERR_INTERNET_DISCONNECTED | Немає Інтернету |
| -102 | ERR_CONNECTION_REFUSED | З'єднання відхилено |
| -101 | ERR_CONNECTION_RESET | З'єднання перервано |
| -100 | ERR_CONNECTION_CLOSED | З'єднання закрито |
| -118 | ERR_CONNECTION_TIMED_OUT | Таймаут з'єднання |

### Помилки безпеки
| Код | Назва | Опис |
|-----|-------|------|
| -107 | ERR_SSL_PROTOCOL_ERROR | Помилка SSL |
| -201 | ERR_CERT_DATE_INVALID | Прострочений сертифікат |
| -202 | ERR_CERT_AUTHORITY_INVALID | Невірний CA |
| -207 | ERR_CERT_INVALID | Недійсний сертифікат |

### HTTP помилки
| Код | Назва | Опис |
|-----|-------|------|
| 400 | Bad Request | Поганий запит |
| 401 | Unauthorized | Потрібна авторизація |
| 403 | Forbidden | Доступ заборонено |
| 404 | Not Found | Сторінку не знайдено |
| 500 | Internal Server Error | Помилка сервера |
| 502 | Bad Gateway | Поганий шлюз |
| 503 | Service Unavailable | Сервіс недоступний |

## Використання

### Підписка на помилки в Tab.axaml.cs

```csharp
// В конструкторі Tab або при створенні TabWorker:
tabWorker.ErrorOccurred += OnBrowserErrorOccurred;

private void OnBrowserErrorOccurred(object? sender, BrowserErrorEventArgs e)
{
    // Створюємо сторінку помилки
    var errorPage = e.CreateErrorPage();
    
    // Підписуємось на дії
    errorPage.RetryRequested += (_, _) => tabWorker.Reload();
    errorPage.GoBackRequested += (_, _) => tabWorker.GoBack();
    errorPage.GoHomeRequested += (_, _) => tabWorker.Navigate("vetale://search");
    errorPage.SearchRequested += (_, query) => NavigateToSearch(query);
    
    // Показуємо сторінку помилки замість WebView
    ShowErrorPage(errorPage);
    
    e.Handled = true;
}
```

### Ручне повідомлення про помилку

```csharp
// Для CefGlue помилок
tabWorker.ReportError(-105, "https://example.com", "ERR_NAME_NOT_RESOLVED");

// Для HTTP помилок
tabWorker.ReportHttpError(404, "https://example.com/page");
```

### Створення сторінки помилки напряму

```csharp
// Для CefGlue помилки
var errorPage = BrowserErrorPage.CreateForCefError(-105, "https://example.com");

// Для HTTP помилки
var errorPage = BrowserErrorPage.CreateForHttpError(404, "https://example.com/page");
```

## UI Елементи сторінки помилки

Сторінка помилки містить:

1. **Заголовок** - іконка та назва помилки з категорією
2. **Опис** - детальне пояснення проблеми
3. **URL** - адреса, яка не завантажилась
4. **Код помилки** - технічна інформація
5. **Поради** - нумерований список рекомендацій
6. **Секція пошуку** - для помилок "не знайдено" (404, ERR_NAME_NOT_RESOLVED)
7. **Кнопки дій**:
   - "Спробувати знову" - перезавантажити сторінку
   - "Назад" - повернутися на попередню сторінку
   - "На головну" - перейти на vetale://search

## Локалізація

Додано локалізаційні рядки в:
- `VetaleBrowser.UI/TranslationsDictionaries/Strings.uk.axaml` (українська)
- `VetaleBrowser.UI/TranslationsDictionaries/Strings.en.axaml` (англійська)

Ключі локалізації:
- `BrowserError.{ErrorType}.Title` - заголовок
- `BrowserError.{ErrorType}.Description` - опис
- `BrowserError.{ErrorType}.Tip1/Tip2/Tip3` - поради
- `BrowserError.Http{Code}.Title/Description` - HTTP помилки
- `BrowserError.Retry/GoBack/GoHome/Search` - кнопки
- `BrowserError.Category.{Type}` - назви категорій

## Стилізація

Стилі відповідають ToolsMainPage:
- Фон сторінки змінюється залежно від категорії помилки
- Картки з білим фоном (#F8F8F8) та округленими кутами
- Зелені кнопки (#4CAF50) для основних дій
- Прозорі кнопки з зеленою рамкою для вторинних дій

## Майбутні покращення

1. **~~Інтеграція з Tab.axaml.cs~~** - ✅ Інтегровано з MainWindow.axaml.cs
2. **Кешування сторінок помилок** - не створювати нову сторінку для кожної помилки
3. **Offline режим** - спеціальна сторінка для роботи без Інтернету
4. **Додаткові мови** - локалізація для інших мов (ru, de, tr)
5. **Анімації** - плавні переходи при показі сторінки помилки

## Інтеграція (виконано)

Обробник помилок інтегровано в `MainWindow.axaml.cs`:

1. В `CreateNewTab()` додано підписку на `ErrorOccurred`:
```csharp
worker.ErrorOccurred += OnWorkerErrorOccurred;
```

2. Метод `OnWorkerErrorOccurred` обробляє помилки та показує локалізовану сторінку

3. Кнопки сторінки помилки підключені:
   - "Спробувати знову" - перезавантажує сторінку
   - "Назад" - повертається в історію
   - "На головну" - navigує до `vetale://search`
   - "Шукати" - виконує пошук через Vetale Search

