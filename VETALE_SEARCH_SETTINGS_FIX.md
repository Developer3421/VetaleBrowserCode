# Виправлення вибору Vetale Search в налаштуваннях пошукових систем

## Що було зроблено

### 1. Vetale Search додана в список пошукових систем
- Тепер Vetale Search відображається як перший RadioButton в списку пошукових систем (а не окрема кнопка)
- Розташування: вгорі списку, перед "Онлайн пошукові системи"
- Має характерний дизайн із фіолетовим кольором та детальним описом

### 2. Логіка збереження виправлена
- При виборі Vetale Search і натисканні Save:
  - Зберігається в налаштуваннях: `Name = "Vetale Search"`, `Url = ""` (порожній рядок як маркер локального пошуку)
  - Налаштування коректно завантажуються при повторному відкритті сторінки налаштувань

### 3. Оновлення поточної вкладки після збереження
- При збереженні Vetale Search як поточної пошукової системи:
  - Активна вкладка MainWindow автоматично оновлюється на VetaleSearchHomePage
  - Користувач одразу бачить домашню сторінку Vetale Search
  - Вікно налаштувань закривається після успішного оновлення

### 4. Детальне логування
- Додано розширене логування для відстеження всіх кроків:
  - `NavigateToSelectedSearchHomeAsync` - початок і результат навігації
  - `GetSearchHomePageAsync` - визначення домашньої сторінки (показує Name/URL із налаштувань)
  - `OpenVetaleSearchInCurrentTab` - процес створення та відображення VetaleSearchHomePage

## Як перевірити

### Тест 1: Збереження Vetale Search
1. Запустити браузер
2. Відкрити налаштування (Settings)
3. Перейти в розділ "Пошукові системи" (Search Engine Settings)
4. Вибрати **Vetale Search** (перший пункт зі значком 🔍)
5. Натиснути **Зберегти** (Save)
6. **Очікуваний результат:**
   - Вікно налаштувань закривається
   - Активна вкладка показує домашню сторінку Vetale Search (логотип, поле пошуку)
   - URL в адресному рядку: `vetale://search`

### Тест 2: Перезапуск браузера
1. Закрити браузер
2. Запустити знову
3. Відкрити налаштування → Пошукові системи
4. **Очікуваний результат:**
   - RadioButton **Vetale Search** обраний (IsChecked = true)
   - Інші пошукові системи не обрані

### Тест 3: Перемикання між пошуковими системами
1. Відкрити налаштування → Пошукові системи
2. Вибрати Google
3. Зберегти → перевірити, що відкривається google.com
4. Знову відкрити налаштування
5. Вибрати Vetale Search
6. Зберегти
7. **Очікуваний результат:**
   - Вкладка оновлюється на Vetale Search домашню сторінку

### Тест 4: Нова вкладка з Vetale Search як поточною системою
1. Встановити Vetale Search як поточну (через тест 1)
2. Натиснути кнопку "+" (додати нову вкладку)
3. **Очікуваний результат:**
   - Нова вкладка відкривається з Vetale Search домашньою сторінкою

## Логи для відстеження

Після запуску у Debug режимі в Output Window шукати рядки:

```
[MainWindow] ===== NavigateToSelectedSearchHomeAsync STARTED =====
[MainWindow] GetSearchHomePageAsync STARTED
[MainWindow] Settings: Name='Vetale Search', URL=''
[MainWindow] Detected Vetale Search! Returning vetale://search
[MainWindow] Home page URL: vetale://search
[MainWindow] Detected Vetale Search - calling OpenVetaleSearchInCurrentTab()
[MainWindow] ===== OpenVetaleSearchInCurrentTab STARTED =====
[MainWindow] Created new VetaleSearchHomePage instance: True
[MainWindow] Target container found, clearing children (count=X)
[MainWindow] Added VetaleSearchHomePage to container (new count=1)
[MainWindow] ===== OpenVetaleSearchInCurrentTab COMPLETED =====
```

Якщо ці логи з'являються після натискання Save, але UI не оновлюється — проблема десь в іншому місці (можливо, WebView не приховується або контейнер не той).

## Змінені файли

1. **SearchEngineSettingsPage.axaml**
   - Замінено окремий Border з кнопкою на RadioButton для Vetale Search
   - Vetale Search тепер частина списку з GroupName="SearchEngine"

2. **SearchEngineSettingsPage.axaml.cs**
   - Додано константи `VetaleSearchName` і `VetaleSearchUrl`
   - Додано обробку Vetale Search в `LoadCurrentSettings()`
   - Додано збереження Vetale Search в `OnSaveClick()`
   - Прибрано виклик `OnBackClick()` після Save (замість цього SettingsWindow закриється автоматично)

3. **SettingsWindow.axaml.cs**
   - Змінено `OnSearchEngineSettingsSaved` на async
   - Додано `await` для `NavigateToSelectedSearchHomeAsync()`
   - Додано явне закриття вікна налаштувань після навігації
   - Додано активацію MainWindow

4. **MainWindow.axaml.cs**
   - Оновлено `GetSearchHomePageAsync()` для розпізнавання Vetale Search (name + порожній URL)
   - Оновлено `NavigateToSelectedSearchHomeAsync()` для виклику `OpenVetaleSearchInCurrentTab()`
   - Повністю переписано `OpenVetaleSearchInCurrentTab()` з:
     - Виконанням у UI потоці через `Dispatcher.UIThread.Post`
     - Явним створенням VetaleSearchHomePage
     - Прямим додаванням до контейнера (замість виклику `ActivateWorkerForInternalPage`)
     - Детальним логуванням кожного кроку
     - Оновленням заголовка вкладки і навбару
   - Додано розширене логування в `GetSearchHomePageAsync()`

## Можливі проблеми

Якщо після всіх змін UI все ще не оновлюється:

1. **Перевірити логи** - чи взагалі викликається `OpenVetaleSearchInCurrentTab`?
2. **Перевірити контейнер** - можливо `_normalModePage?.WebViewGrid` є null
3. **Перевірити WebView** - можливо WebView не приховується і залишається поверх UserControl
4. **Перевірити z-index** - можливо потрібно явно встановити z-order
5. **Перевірити VetaleSearchHomePage** - чи правильно створюється UserControl, чи є InitializeComponent?

## Наступні кроки

Після успішного тестування:

1. Додати аналогічну логіку для інших внутрішніх сторінок (закладки, історія)
2. Створити модель `SearchEngineDefinition` для більш структурованого зберігання пошукових систем
3. Додати можливість індексації кількох локальних пошукових систем
4. Реалізувати SearchEngineService для централізованого управління

