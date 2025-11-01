# Нова архітектура системи закладок

## Огляд

Система закладок тепер реалізована в стилі вікон налаштувань та інструментів, з підтримкою сторінок для різних функцій.

## Компоненти

### 1. BookmarksWindow (Вікно закладок)
**Файл:** `VetaleBrowser.UI/Windows/BookmarksWindow.axaml`

**Особливості:**
- Той самий стиль що й SettingsWindow та ToolsWindow
- Топ-бар з іконкою зірки ⭐
- Кнопка "Vetale Browser" для повернення до головного вікна
- Кнопки мінімізації, максимізації, закриття
- ContentControl для відображення різних сторінок

**Конструктори:**
```csharp
// Без параметрів - показує список закладок
var window = new BookmarksWindow();

// З URL та заголовком - показує форму додавання
var window = new BookmarksWindow(url, title);
```

### 2. AddBookmarkPage (Сторінка додавання)
**Файл:** `VetaleBrowser.UI/Pages/AddBookmarkPage.axaml`

**Особливості:**
- Велика іконка зірки ⭐ вгорі
- Поле для введення назви (з auto-focus)
- Поле URL (read-only)
- Вибір папки (ComboBox)
- Кнопки "Скасувати" та "Зберегти"
- Підтримка Enter для швидкого збереження

**Події:**
- `BookmarkSaved` - закладка збережена
- `Cancelled` - скасовано

**Приклад:**
```csharp
var page = new AddBookmarkPage("https://example.com", "Example Site");
page.BookmarkSaved += (s, e) => {
    // Переключитись на список
};
page.Cancelled += (s, e) => {
    // Показати список
};
```

### 3. BookmarksPage (Сторінка списку)
**Файл:** `VetaleBrowser.UI/Pages/BookmarksPage.axaml`

**Особливості:**
- Список всіх закладок
- Фільтр по папках
- Кнопка "Додати закладку"
- Видалення закладок
- Відкриття в браузері

## Потік роботи

### Сценарій 1: Додавання закладки з браузера

```
MainWindow (кнопка ⭐)
    ↓
BookmarksWindow(url, title) відкривається
    ↓
Показується AddBookmarkPage з даними
    ↓
Користувач натискає "Зберегти"
    ↓
Закладка зберігається в БД
    ↓
Автоматично переключається на BookmarksPage
    ↓
Користувач бачить свою нову закладку в списку
```

### Сценарій 2: Скасування

```
AddBookmarkPage
    ↓
Користувач натискає "Скасувати"
    ↓
Автоматично переключається на BookmarksPage
    ↓
Закладка НЕ збережена
```

### Сценарій 3: Перегляд закладок

```
MainWindow або BookmarksWindow
    ↓
BookmarksWindow() без параметрів
    ↓
Відразу показується BookmarksPage
    ↓
Користувач переглядає список
```

## Код інтеграції

### MainWindow.axaml.cs

```csharp
private void OnBookmarkRequested(object? sender, EventArgs e)
{
    var currentUrl = _tabs.Active.Manager.GetCurrentUrl() ?? string.Empty;
    var currentTitle = _tabs.Active.Title ?? "Без назви";

    // Відкриваємо вікно з формою додавання
    var bookmarksWindow = new BookmarksWindow(currentUrl, currentTitle);
    bookmarksWindow.Show();
}
```

### BookmarksWindow.axaml.cs

```csharp
private void ShowAddBookmarkPage(string url, string title)
{
    var addBookmarkPage = new AddBookmarkPage(url, title);
    
    addBookmarkPage.BookmarkSaved += (s, e) =>
    {
        ShowBookmarksPage(); // Перехід на список
    };
    
    addBookmarkPage.Cancelled += (s, e) =>
    {
        ShowBookmarksPage(); // Перехід на список
    };
    
    if (_contentHost != null)
    {
        _contentHost.Content = addBookmarkPage;
    }
}

public void ShowBookmarksPage()
{
    _bookmarksPage = new BookmarksPage();
    _bookmarksPage.SetDatabaseService(DatabaseManager.Instance);
    
    if (_contentHost != null)
    {
        _contentHost.Content = _bookmarksPage;
    }
}
```

## Переваги нової архітектури

1. **Єдиний стиль** - всі вікна виглядають однаково
2. **Модульність** - сторінки можна використовувати окремо
3. **Гнучкість** - легко додати нові сторінки
4. **UX** - плавні переходи між функціями
5. **Консистентність** - той самий топ-бар скрізь

## Структура UI

```
BookmarksWindow (вікно)
├── TopBar (сірий градієнт)
│   ├── ⭐ іконка
│   ├── "Vetale Browser" кнопка
│   └── Window controls (-, □, ✕)
└── ContentHost (білий фон)
    ├── AddBookmarkPage (форма додавання)
    │   ├── ⭐ велика іконка
    │   ├── Назва TextBox
    │   ├── URL TextBox (readonly)
    │   ├── Папка ComboBox
    │   └── Кнопки (Скасувати, Зберегти)
    └── BookmarksPage (список)
        ├── Фільтри по папках
        ├── Кнопка "+ Додати закладку"
        └── Список закладок
```

## Майбутні покращення

1. Анімації переходів між сторінками
2. Сторінка редагування закладки
3. Сторінка управління папками
4. Сторінка імпорту/експорту
5. Пошук по закладках

## Тестування

1. Запустіть браузер
2. Відкрийте будь-який сайт
3. Натисніть ⭐ в NavigationBar
4. Введіть назву закладки
5. Натисніть "Зберегти"
6. Переконайтесь що вікно показує список закладок
7. Знайдіть нову закладку в списку
8. Натисніть "Vetale Browser" щоб повернутись

✅ Все працює!

