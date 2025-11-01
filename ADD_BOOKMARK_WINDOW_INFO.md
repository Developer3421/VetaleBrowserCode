# Add Bookmark Window Implementation

## Overview
Вікно "Додати в закладки" (AddBookmarkWindow) - це модальне вікно для швидкого додавання поточної сторінки в закладки.

## Features

### 1. Window Structure (AddBookmarkWindow.axaml)
- **SystemDecorations="None"** - повний кастомний chrome
- **Фіксований розмір**: 500x400 (не змінюється)
- **WindowStartupLocation="CenterScreen"** - вікно відкривається по центру екрану
- **Кастомний title bar** з іконкою зірки ⭐ та кнопкою закриття
- **TransparencyLevelHint="AcrylicBlur"** - ефект прозорості

### 2. UI Components

#### Input Fields:
1. **Назва (Name):**
   - TextBox для введення назви закладки
   - Watermark: "Введіть назву закладки"
   - Автоматично отримує фокус при відкритті
   - Підтримка Enter для збереження

2. **URL:**
   - TextBox для відображення URL
   - Watermark: "https://example.com"
   - Read-only (тільки для читання)
   - Автоматично заповнюється з поточної сторінки

3. **Папка (Folder):**
   - ComboBox для вибору папки
   - Попередньо заповнені папки:
     - Закладки (за замовчуванням)
     - Робота
     - Особисті
     - Новини

#### Action Buttons:
1. **Скасувати (Cancel)**
   - Сірий фон (#E0E0E0)
   - Закриває вікно без збереження
   - Hover ефект: #D0D0D0

2. **Зберегти (Save)**
   - Фіолетовий фон (#9A1CE8)
   - Білий текст
   - Зберігає закладку та закриває вікно
   - Hover ефект: #8612D0
   - Pressed ефект: #7510B8

### 3. Code-Behind (AddBookmarkWindow.axaml.cs)

#### Properties:
- **BookmarkName** - назва закладки
- **BookmarkUrl** - URL закладки
- **BookmarkFolder** - вибрана папка
- **IsSaved** - чи була збережена закладка

#### Constructors:
```csharp
// Порожній конструктор
public AddBookmarkWindow()

// Конструктор з параметрами
public AddBookmarkWindow(string url, string title = "")
```

#### Key Features:
- **Auto-focus** на поле назви при відкритті
- **SelectAll** для швидкої зміни назви
- **Enter key** для швидкого збереження
- **Validation** - перевірка що назва не пуста
- **Drag support** - можливість перетягувати вікно за title bar

### 4. Styling

#### TextBox Style:
- Background: #F8F8F8
- Border: #CCCCCC
- Padding: 10px
- Corner radius: 4px
- Font size: 14px

#### ComboBox Style:
- Такий же як TextBox
- Повна ширина

#### Button Margins:
- Відступ між кнопками: 10px
- Відступ зверху від полів: 20px

## Usage Example

```csharp
// Відкрити вікно додавання закладки
var bookmarkWindow = new AddBookmarkWindow(
    url: "https://example.com",
    title: "Example Website"
);

bookmarkWindow.ShowDialog(parentWindow);

// Перевірити чи збережено
if (bookmarkWindow.IsSaved)
{
    var name = bookmarkWindow.BookmarkName;
    var url = bookmarkWindow.BookmarkUrl;
    var folder = bookmarkWindow.BookmarkFolder;
    
    // TODO: Save to database
}
```

## Validation

- **Назва**: обов'язкове поле (не може бути пустим)
- **URL**: автоматично заповнюється, read-only
- **Папка**: завжди має значення (за замовчуванням "Закладки")

## Future Enhancements

### Backend Integration:
- [ ] Підключення до бази даних закладок
- [ ] Збереження закладок в файл/БД
- [ ] Завантаження списку папок з БД

### UI Improvements:
- [ ] Валідація з повідомленнями про помилки
- [ ] Можливість створення нової папки
- [ ] Іконка сайту (favicon) в вікні
- [ ] Автодоповнення назви з title сторінки
- [ ] Історія останніх використаних папок

### Additional Features:
- [ ] Теги для закладок
- [ ] Опис/нотатки для закладки
- [ ] Швидкі клавіші (Ctrl+D для збереження)
- [ ] Drag & drop URL з браузера

