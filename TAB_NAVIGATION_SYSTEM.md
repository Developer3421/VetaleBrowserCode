# Система навігації та історії вкладок

## Огляд

Реалізована професійна система навігації для VetaleBrowser з підтримкою:
- ✅ Єдиної історії навігації для кожної вкладки
- ✅ Підтримки внутрішніх (vetale://) та зовнішніх (http(s)://) URL
- ✅ Коректної роботи кнопок "Назад" та "Вперед"
- ✅ Кнопки вимкнення звуку (Mute) для вкладок

## Архітектура

### 1. NavigationEntry (TabWorker.cs)
Представляє один запис в історії навігації:

```csharp
public class NavigationEntry
{
    public string Url { get; set; }                    // URL сторінки
    public string? Title { get; set; }                 // Заголовок
    public bool IsInternal { get; set; }               // Чи це vetale:// URL
    public UserControl? InternalPageContent { get; set; } // Контент для внутрішніх сторінок
    public DateTime Timestamp { get; set; }            // Час створення
}
```

### 2. NavigationHistory (TabWorker.cs)
Керує стеком історії навігації:

```csharp
public class NavigationHistory
{
    public NavigationEntry? CurrentEntry { get; }      // Поточна сторінка
    public bool CanGoBack { get; }                     // Чи можна назад
    public bool CanGoForward { get; }                  // Чи можна вперед
    
    public void AddEntry(NavigationEntry entry);       // Додати запис
    public NavigationEntry? GoBack();                  // Назад
    public NavigationEntry? GoForward();               // Вперед
}
```

### 3. TabWorker
Кожна вкладка має власну історію:

```csharp
public class TabWorker
{
    public NavigationHistory History { get; }          // Історія навігації
    
    public void Navigate(string url, UserControl? content = null);  // Навігація
    public void GoBack();                              // Назад
    public void GoForward();                           // Вперед
    
    public event EventHandler<NavigationEntry> NavigationChanged;  // Подія зміни
}
```

## Потік навігації

### Сценарій 1: Пошук в VetaleSearch → Перехід на сайт → Назад

1. **Користувач відкриває VetaleSearch**
   ```
   CreateNewTab("vetale://search")
   → worker.Navigate("vetale://search", VetaleSearchHomePage)
   → History.AddEntry(url="vetale://search", IsInternal=true, content=VetaleSearchHomePage)
   ```

2. **Користувач шукає "github"**
   ```
   HandleInternalNavigation("vetale://search?q=github")
   → worker.Navigate("vetale://search?q=github", VetaleSearchResultsPage)
   → History.AddEntry(url="vetale://search?q=github", IsInternal=true, content=ResultsPage)
   
   Історія: [VetaleSearchHome, VetaleSearchResults] ← current
   ```

3. **Користувач клікає на результат → перехід на github.com**
   ```
   OnSearchResultNavigateRequested("https://github.com")
   → worker.Navigate("https://github.com")
   → History.AddEntry(url="https://github.com", IsInternal=false)
   → WebView.Address = "https://github.com"
   
   Історія: [VetaleSearchHome, VetaleSearchResults, github.com] ← current
   ```

4. **Користувач натискає "Назад"**
   ```
   NavigationBar.OnBackButtonClick()
   → worker.GoBack()
   → History.GoBack() → повертає VetaleSearchResults entry
   → NavigateToHistoryEntry(VetaleSearchResults)
   → NavigationChanged(entry) → OnWorkerNavigationChanged()
   → ActivateWorkerForInternalPage(worker, VetaleSearchResultsPage)
   → Показується ResultsPage
   
   Історія: [VetaleSearchHome, VetaleSearchResults ← current, github.com]
   ```

5. **Користувач натискає "Вперед"**
   ```
   NavigationBar.OnForwardButtonClick()
   → worker.GoForward()
   → History.GoForward() → повертає github.com entry
   → NavigateToHistoryEntry(github.com)
   → WebView.Address = "https://github.com"
   → Показується WebView
   
   Історія: [VetaleSearchHome, VetaleSearchResults, github.com] ← current
   ```

### Сценарій 2: Навігація всередині сайту

1. **Користувач на github.com клікає посилання → github.com/features**
   ```
   WebView автоматично навігує
   → WebViewOnPropertyChanged(Address = "github.com/features")
   → Перевірка: !_isNavigating && newAddress != Address
   → History.AddEntry(url="github.com/features", IsInternal=false)
   
   Історія: [..., github.com, github.com/features] ← current
   ```

2. **Користувач натискає "Назад"**
   ```
   → worker.GoBack()
   → WebView.Address = "github.com"
   
   Історія: [..., github.com ← current, github.com/features]
   ```

## Ключові механізми

### 1. Запобігання дублікатам
Прапорець `_isNavigating` в TabWorker запобігає рекурсивному додаванню записів:

```csharp
// При програмній навігації
public void Navigate(string url, UserControl? content = null)
{
    _isNavigating = true;
    try {
        // Навігація + додавання в історію
    }
    finally {
        _isNavigating = false;
    }
}

// При автоматичній навігації WebView
private void WebViewOnPropertyChanged(...)
{
    if (!_isNavigating && newAddress != Address) {
        // Додаємо в історію тільки якщо це НЕ програмна навігація
    }
}
```

### 2. Синхронізація UI
При зміні навігації оновлюється:
- Контейнер контенту (WebView або UserControl)
- Адресний рядок
- Кнопки Назад/Вперед
- Заголовок вкладки

```csharp
private void OnWorkerNavigationChanged(object? sender, NavigationEntry entry)
{
    if (entry.IsInternal && entry.InternalPageContent != null)
        ActivateWorkerForInternalPage(worker, entry.InternalPageContent);
    else
        ActivateWorker(worker); // Показує WebView
    
    UpdateTabTitle(worker, entry.Title);
    UpdateNavigationBar(entry.Url);
}
```

### 3. Кнопка Mute
Вже реалізована в Tab.axaml.cs:

```csharp
public class Tab : TemplatedControl
{
    public bool IsMuted { get; set; }
    public event EventHandler? MuteToggled;
}
```

Синхронізується з TabWorker.IsMuted:
```csharp
tab.MuteToggled += async (_, __) =>
{
    var desired = !worker.IsMuted;
    // JS mute для медіа елементів
    worker.IsMuted = desired;
    tab.IsMuted = worker.IsMuted;
};
```

## Внутрішні URL (vetale://)

Підтримувані:
- `vetale://search` - домашня сторінка пошуку
- `vetale://search?q=запит` - результати пошуку
- `vetale://bookmarks` - закладки
- `vetale://history` - історія
- `vetale://settings` - налаштування
- `vetale://downloads` - завантаження

## Файли змінені

### Core
- `VetaleBrowser.Core/Scripts/Models/TabWorker.cs`
  - Додано `NavigationEntry`, `NavigationHistory`
  - Додано `Navigate()`, `GoBack()`, `GoForward()`
  - Додано автоматичне відстеження навігації WebView
  - Подія `NavigationChanged`

### UI
- `VetaleBrowser.UI/Еlements/NavigationBar.axaml.cs`
  - Додано `SetTabWorker()`
  - Оновлено `OnBackButtonClick()`, `OnForwardButtonClick()`
  - Використання `TabWorker.GoBack()/GoForward()`

### MainWindow
- `MainWindow.axaml.cs`
  - Оновлено `CreateNewTab()` - уникнення подвійної навігації
  - Додано `OnWorkerNavigationChanged()` - обробка змін навігації
  - Додано `SubscribeToInternalPageEvents()`
  - Оновлено `HandleInternalNavigation()`
  - Оновлено `ActivateWorkerForInternalPage()` - використання History
  - Додано `UpdateTabTitle()`, `UpdateNavigationBar()`

## Тестування

### Тест 1: Базова навігація
1. Відкрити VetaleSearch
2. Шукати "test"
3. Клікнути на результат
4. Натиснути "Назад" → має показати результати
5. Натиснути "Вперед" → має показати веб-сайт

### Тест 2: Множинна навігація
1. VetaleSearch → пошук "github" → github.com
2. github.com → github.com/features → github.com/pricing
3. Назад × 2 → має бути github.com
4. Назад → має бути результати пошуку VetaleSearch
5. Вперед × 3 → має бути github.com/pricing

### Тест 3: Нова вкладка з внутрішнім URL
1. Відкрити нову вкладку з vetale://bookmarks
2. Перейти на google.com
3. Назад → має показати закладки

### Тест 4: Mute
1. Відкрити YouTube відео
2. Клікнути на іконку mute у вкладці
3. Звук має вимкнутися
4. Клікнути знову → звук має увімкнутися

## Переваги

✅ **Професійна поведінка** - як Chrome/Firefox
✅ **Єдина система** - без окремих "VetaleSearch вкладок"
✅ **Історія працює** - можна переходити між внутрішніми та зовнішніми сторінками
✅ **Mute підтримка** - вимкнення звуку для кожної вкладки
✅ **Чистий код** - централізована логіка в TabWorker
✅ **Розширюваність** - легко додати нові внутрішні URL

## Відомі обмеження

1. **Серіалізація історії** - UserControl не серіалізується при збереженні сесії
   - **Рішення**: При відновленні створювати новий UserControl через `InternalUrlHandler.CreatePageContent(url)`

2. **WebView власна історія** - WebView.CanGoBack/CanGoForward більше не використовуються
   - **Рішення**: Використовуємо тільки TabWorker.History

3. **Favicon для внутрішніх сторінок** - потрібні статичні іконки
   - **Рішення**: `InternalUrlHandler.GetPageIcon(url)`

---

**Статус:** ✅ Реалізовано та готово до тестування
**Дата:** 18 листопада 2025

