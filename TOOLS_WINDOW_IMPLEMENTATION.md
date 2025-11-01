# Інструменти - Інформація про реалізацію

## Огляд
Вікно інструментів (`ToolsWindow`) тепер має повнофункціональну головну сторінку зі списком інструментів та окрему сторінку з WebView для перегляду зовнішніх інструментів.

## Структура файлів

### Сторінки
1. **ToolsMainPage.axaml/cs** - головна сторінка зі списком інструментів
2. **ToolsWebViewPage.axaml/cs** - сторінка з WebView для перегляду інструментів

### Вікна
- **ToolsWindow.axaml/cs** - головне вікно інструментів з підтримкою перемикання між сторінками

## Функціонал

### ToolsMainPage
Головна сторінка містить список інструментів:

#### AI Інструменти (зовнішні)
- **DuckDuckGo AI Chat** - безкоштовний AI чат
  - Іконка: favicon з duckduckgo.com
  - URL: https://duckduckgo.com/aichat
  
- **Microsoft Copilot** - AI асистент від Microsoft
  - Іконка: favicon з copilot.microsoft.com
  - URL: https://copilot.microsoft.com
  
- **Google Gemini** - AI від Google
  - Іконка: favicon з gemini.google.com
  - URL: https://gemini.google.com

- **Replika AI** - AI компаньйон для спілкування
  - Іконка: favicon з replika.com
  - URL: https://replika.com

#### Внутрішні інструменти
- **Vetale AI Chat** 🤖 - чат з ШІ Vetale (TODO)
- **Vetale DevTools** 🔧 - інструменти розробника (TODO)
- **Історія** 📜 - історія відвідувань (TODO)

### Кнопки дій

Для кожного елемента списку доступні дві дії:

1. **Клік по елементу** - відкриває інструмент в WebView всередині вікна інструментів
2. **Кнопка "→ Перейти в браузері"** (тільки для зовнішніх інструментів) - відкриває інструмент у поточній вкладці головного вікна браузера

### ToolsWebViewPage

Сторінка з WebView для перегляду інструментів містить:
- **Кнопка "Назад"** - повертає до головної сторінки зі списком
- **Назва інструменту** - відображається в заголовку
- **Кнопка "Оновити"** - перезавантажує WebView
- **WebView контейнер** - відображає вміст інструменту

При натисканні "Назад" WebView автоматично видаляється для звільнення ресурсів.

## Використання FaviconService

Для зовнішніх інструментів використовується `FaviconService` для завантаження іконок:
- Розмір іконки: 32x32 пікселя
- Кешування в пам'яті та на диску
- Fallback на різні сервіси (favicon.im, Google S2, DuckDuckGo)

## Стилізація

### ToolsMainPage
- Елементи списку мають rounded corners (6px)
- Hover ефект з фіолетовою рамкою (#9A1CE8)
- Кнопки навігації з прозорим фоном та фіолетовою рамкою
- При hover кнопки міняють фон на фіолетовий з білим текстом

### ToolsWebViewPage
- Верхня панель з сірим фоном (#F8F8F8)
- Кнопка "Назад" зі стрілкою та текстом
- Responsive WebView контейнер

## Події

### ToolsMainPage події:
- `NavigateInWebView` - викликається при кліку на елемент для відкриття в WebView
- `NavigateInMainTab` - викликається при кліку на кнопку "→ Перейти в браузері"

### ToolsWebViewPage події:
- `BackRequested` - викликається при натисканні кнопки "Назад"

## TODO
1. Реалізувати Vetale AI Chat
2. Реалізувати Vetale DevTools
3. Реалізувати відкриття сторінки історії
4. Додати можливість навігації в активній вкладці головного вікна (потрібен API в MainWindow)

## Приклад використання

```csharp
// Відкрити вікно інструментів
var toolsWindow = new ToolsWindow();
toolsWindow.Show();

// При кліку на елемент - відкриється WebView з інструментом
// При кліку на кнопку "→ Перейти в браузері" - URL відкриється в головному вікні
```

## Архітектура

```
ToolsWindow
├── ContentArea (Grid)
    ├── ToolsMainPage (початкова)
    │   ├── Header
    │   ├── ScrollViewer
    │   │   └── ToolsListPanel (StackPanel)
    │   │       └── Tool Items (Border)
    │   │           ├── Icon (Image або Emoji)
    │   │           ├── Text (Name + Description)
    │   │           └── Navigation Button (для зовнішніх)
    │
    └── ToolsWebViewPage (при виборі інструменту)
        ├── Header (Back, Title, Refresh)
        └── WebViewContainer (Grid)
            └── WebView
```

