# Vetale Search - Документація

## Огляд

Vetale Search - це локальна пошукова система для браузера VetaleBrowser з оранжево-фіолетовим дизайном, схожим на Google.

## Створені файли

### 1. VetaleSearchHomePage (Головна сторінка)
**Файли:**
- `VetaleSearchHomePage.axaml` - XAML розмітка
- `VetaleSearchHomePage.axaml.cs` - Code-behind

**Особливості:**
- Великий логотип "Vetale Search" в центрі
- Оранжево-фіолетовий градієнтний фон
- Пошукове поле з іконками для голосового пошуку та пошуку за зображенням
- Вибір пошукової системи (ComboBox):
  - Vetale Search (Локальний)
  - Google
  - Bing
  - DuckDuckGo
  - Yandex
- Дві кнопки пошуку:
  - "Пошук" - звичайний пошук
  - "Мені пощастить" - відкрити перший результат
- Швидкі посилання (Закладки, Історія, Налаштування)

### 2. VetaleSearchResultsPage (Сторінка результатів)
**Файли:**
- `VetaleSearchResultsPage.axaml` - XAML розмітка
- `VetaleSearchResultsPage.axaml.cs` - Code-behind

**Особливості:**
- Компактний хедер з оранжево-фіолетовим градієнтом
- Пошукове поле в хедері для нового пошуку
- Кнопка вибору пошукової системи з випадаючим меню
- Вкладки фільтрації (як в Google):
  - Усі
  - Зображення
  - Відео
  - Новини
  - Карти
  - Інструменти
- Статистика пошуку (кількість результатів та час)
- Картки результатів з:
  - URL (сірий текст)
  - Заголовок (оранжево-фіолетовий градієнт)
  - Опис (чорний текст)
  - Ховер ефекти з фіолетовим відтінком
- Пагінація (10 сторінок)

## Стилі та кольори

### Кольорова палітра
- **Помаранчевий**: `#FF8A00`
- **Фіолетовий**: `#9C27B0`
- **Фон**: `#F8F9FA` (світло-сірий)
- **Білий**: `#FFFFFF`
- **Текст основний**: `#3C4043`
- **Текст вторинний**: `#5F6368`
- **Сірий світлий**: `#E0E0E0`

### Градієнти
```xml
<LinearGradientBrush StartPoint="0%,0%" EndPoint="100%,100%">
    <GradientStop Color="#FF8A00" Offset="0"/>
    <GradientStop Color="#9C27B0" Offset="1"/>
</LinearGradientBrush>
```

## Як використовувати

### Інтеграція в проект

1. **Файли вже створені** в папці:
   ```
   E:\VetaleBrowser\VetaleBrowser\VetaleBrowser.UI\Pages\
   ```

2. **Навігація між сторінками:**
   ```csharp
   // Перехід на головну сторінку
   var homePage = new VetaleSearchHomePage();
   
   // Перехід на сторінку результатів з запитом
   var resultsPage = new VetaleSearchResultsPage();
   resultsPage.SetSearchQuery("мій запит");
   ```

### Методи для реалізації

#### VetaleSearchHomePage
```csharp
private void PerformSearch(bool isLucky = false)
{
    // TODO: Реалізувати логіку пошуку
    // Якщо вибрано Vetale Search (index 0) - шукати в локальній БД
    // Інакше - перенаправити на зовнішню пошукову систему
}
```

#### VetaleSearchResultsPage
```csharp
public void SetSearchQuery(string query)
{
    // Встановити текст запиту та виконати пошук
}

private void LoadSearchResults(string query)
{
    // TODO: Завантажити результати з БД або API
    // Очистити _resultsPanel
    // Додати нові результати
}
```

## Функціонал для подальшої реалізації

### Backend інтеграція

1. **Локальний пошук (Vetale Search)**
   - Підключення до локальної БД
   - Індексація відвіданих сторінок
   - Повнотекстовий пошук
   - Ранжування результатів

2. **Зовнішні пошукові системи**
   - Google: `https://www.google.com/search?q={query}`
   - Bing: `https://www.bing.com/search?q={query}`
   - DuckDuckGo: `https://duckduckgo.com/?q={query}`
   - Yandex: `https://yandex.com/search/?text={query}`

3. **Додаткові функції**
   - Голосовий пошук (Web Speech API)
   - Пошук за зображенням
   - Автодоповнення
   - Історія пошуків
   - Персоналізовані результати

### Приклад реалізації пошуку

```csharp
private void PerformSearch(bool isLucky = false)
{
    if (_searchInput == null || string.IsNullOrWhiteSpace(_searchInput.Text))
        return;

    string query = _searchInput.Text.Trim();
    int selectedEngine = _searchEngineSelector?.SelectedIndex ?? 0;

    if (selectedEngine == 0)
    {
        // Локальний пошук Vetale Search
        var resultsPage = new VetaleSearchResultsPage();
        resultsPage.SetSearchQuery(query);
        // Навігація на сторінку результатів
    }
    else
    {
        // Зовнішня пошукова система
        string[] searchUrls = new[]
        {
            "", // Vetale Search
            $"https://www.google.com/search?q={Uri.EscapeDataString(query)}",
            $"https://www.bing.com/search?q={Uri.EscapeDataString(query)}",
            $"https://duckduckgo.com/?q={Uri.EscapeDataString(query)}",
            $"https://yandex.com/search/?text={Uri.EscapeDataString(query)}"
        };
        
        if (selectedEngine < searchUrls.Length)
        {
            // Відкрити URL в браузері
            // NavigateToUrl(searchUrls[selectedEngine]);
        }
    }
}
```

## Responsive дизайн

Сторінки адаптовані для різних розмірів вікон:
- MaxWidth для контенту: 720-800px
- Padding та Margin налаштовані для комфортного перегляду
- ScrollViewer для довгих списків результатів

## Доступність

- Використання емодзі для візуальної ідентифікації
- Tooltips на кнопках
- Placeholder текст в полях вводу
- Курсор "Hand" на клікабельних елементах
- Чіткий контраст кольорів

## Подальші покращення

1. Анімації переходів між сторінками
2. Lazy loading результатів
3. Фільтри та сортування
4. Збереження налаштувань пошукової системи
5. Темна тема
6. Історія пошукових запитів
7. Популярні пошуки
8. Інтеграція з закладками та історією браузера

## Технічні деталі

- **Framework**: Avalonia UI
- **Мова**: C# (.NET)
- **Стиль**: XAML з inline стилями
- **Архітектура**: MVVM-ready (можна додати ViewModels)

---

**Автор**: VetaleBrowser Team  
**Дата**: 15 листопада 2025  
**Версія**: 1.0

