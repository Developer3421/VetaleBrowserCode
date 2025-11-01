# Інструкція по використанню AddBookmarkWindow

## Як працює система закладок

### 1. Додавання закладки з головного вікна

Коли користувач натискає кнопку "зірка" (⭐) в NavigationBar:

1. Викликається метод `OnBookmarkRequested` в `MainWindow.axaml.cs`
2. Отримується поточний URL та заголовок активної вкладки
3. Відкривається вікно `AddBookmarkWindow` з цими даними
4. Користувач може відредагувати назву та вибрати папку
5. Після збереження закладка додається в базу даних через `DatabaseManager`

### 2. Код виклику вікна

```csharp
// В MainWindow.axaml.cs
private async void OnBookmarkRequested(object? sender, EventArgs e)
{
    var currentUrl = _tabs.Active.Manager.GetCurrentUrl() ?? string.Empty;
    var currentTitle = _tabs.Active.Title ?? "Без назви";

    var bookmarkWindow = new AddBookmarkWindow(currentUrl, currentTitle);
    await bookmarkWindow.ShowDialog(this);

    if (bookmarkWindow.IsSaved)
    {
        DatabaseManager.Instance.AddBookmark(
            bookmarkWindow.BookmarkUrl,
            bookmarkWindow.BookmarkName,
            bookmarkWindow.BookmarkFolder
        );
    }
}
```

### 3. Структура AddBookmarkWindow

**Властивості:**
- `BookmarkName` - назва закладки
- `BookmarkUrl` - URL закладки (read-only в UI)
- `BookmarkFolder` - обрана папка
- `IsSaved` - чи була закладка збережена

**Папки за замовчуванням:**
- Закладки (за замовчуванням)
- Робота
- Особисті
- Новини

### 4. UI Features

- **Кастомний title bar** з іконкою зірки ⭐
- **Перетягування** вікна за title bar
- **Enter** для швидкого збереження
- **Auto-focus** на поле назви з автовибором тексту
- **Валідація** - назва не може бути порожньою

### 5. Використання бази даних

Всі закладки автоматично:
- **Шифруються** AES-256
- **Зберігаються** в LiteDB
- **Організовані** по папках
- **Доступні** через `DatabaseManager.Instance`

### 6. Приклади

#### Отримати всі закладки
```csharp
var allBookmarks = DatabaseManager.Instance.GetBookmarks();
```

#### Отримати закладки з папки
```csharp
var workBookmarks = DatabaseManager.Instance.GetBookmarks("Робота");
```

#### Додати закладку програмно
```csharp
var bookmarkId = DatabaseManager.Instance.AddBookmark(
    url: "https://example.com",
    title: "Example Website",
    folder: "Особисті"
);
```

#### Видалити закладку
```csharp
DatabaseManager.Instance.DeleteBookmark(bookmarkId);
```

### 7. Сторінки для перегляду

Використовуйте `BookmarksPage.axaml` для відображення списку закладок:

```csharp
var bookmarksPage = new BookmarksPage();
bookmarksPage.SetDatabaseService(DatabaseManager.Instance);
```

### 8. Автоматичне збереження вкладок

Кожна відкрита вкладка може автоматично зберігатися в історію:

```csharp
DatabaseManager.AddTabToCurrentSession(
    url: "https://example.com",
    title: "Example",
    isActive: true
);
```

## Тестування

1. Запустіть браузер
2. Відкрийте будь-який сайт
3. Натисніть кнопку зірки ⭐ в NavigationBar
4. Введіть назву закладки
5. Виберіть папку
6. Натисніть "Зберегти" або Enter
7. Закладка з'явиться в базі даних

## База даних

**Розташування:** `%APPDATA%\VetaleBrowser\Data\browser.db`

**Максимальні обмеження:**
- Закладок: 50,000
- Вкладок: 10,000
- Сесій: 100
- Розмір БД: 500 MB

При досягненні лімітів старі дані автоматично видаляються.

