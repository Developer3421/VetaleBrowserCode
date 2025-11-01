# Settings Window Implementation

## Overview
Вікно налаштувань (SettingsWindow) тепер є повноцінним Avalonia вікном з динамічним контентом, який змінюється залежно від вибраної сторінки налаштувань.

## Changes Made

### 1. Window Structure (SettingsWindow.axaml)
- Встановлено `SystemDecorations="None"` для повного кастомного chrome
- Додано `ContentControl` з ім'ям `PART_ContentHost` для динамічного завантаження сторінок
- Налаштовано розміри вікна: 900x700 (за замовчуванням), мінімум 700x500
- Додано `TransparencyLevelHint="AcrylicBlur"` для ефекту прозорості

### 2. Window Code-Behind (SettingsWindow.axaml.cs)
- Додано поля для ContentControl та SettingsMainPage
- Реалізовано метод `OnLoaded` для ініціалізації контенту після завантаження вікна
- Додано метод `LoadMainPage()` для завантаження головної сторінки налаштувань
- Додано метод `ResizeWindowForPage(width, height)` для зміни розміру вікна відповідно до контенту
- Додано обробники подій для переходу між сторінками:
  - `OnLanguageRequested` - для сторінки налаштувань мови
  - `OnAppearanceRequested` - для сторінки налаштувань вигляду
  - `OnSearchEngineRequested` - для сторінки налаштувань пошукової системи

### 3. Main Settings Page (SettingsMainPage.axaml)
Створено головну сторінку налаштувань з трьома кнопками:

#### Buttons:
1. **Language** - "Choose your preferred language"
2. **Appearance** - "Customize the look and feel"
3. **Search Engine** - "Select your default search engine"

#### Styling:
- Світло-сірий фон кнопок (#F5F5F5)
- Фіолетова рамка при наведенні (#9A1CE8)
- Темніший фон при натисканні (#D0D0D0)
- Заокруглені кути (4px)
- Paddings: 20px 15px
- Margins: 15px між кнопками

### 4. Main Settings Page Code-Behind (SettingsMainPage.axaml.cs)
- Оголошено події для запиту різних сторінок налаштувань
- Реалізовано обробники кліків для кожної кнопки
- Події піднімаються до батьківського вікна для обробки навігації

## Window Resizing
Вікно автоматично змінює свій розмір при завантаженні різних сторінок:
- Головна сторінка: 660x500

## Tools Window
ToolsWindow також оновлено до повноцінного Avalonia вікна з такими ж налаштуваннями:
- SystemDecorations="None"
- Розміри: 800x600 (за замовчуванням), мінімум 640x480
- Кастомний title bar з кнопками управління вікном

## Add Bookmark Window
Створено нове модальне вікно для додавання закладок (AddBookmarkWindow):
- Фіксований розмір: 500x400 (без можливості зміни)
- WindowStartupLocation="CenterScreen"
- SystemDecorations="None"
- 3 поля: Назва (Name), URL, Папка (Folder)
- 2 кнопки: Скасувати, Зберегти
- Підтримка Enter для швидкого збереження
- Валідація обов'язкових полів
- Автофокус на поле назви

## Future Enhancements
- Реалізувати сторінки Language, Appearance, Search Engine
- Додати анімацію переходів між сторінками
- Додати кнопку "Назад" для повернення до головної сторінки
- Додати збереження вибраних налаштувань
- Інтегрувати AddBookmarkWindow з реальною системою закладок

