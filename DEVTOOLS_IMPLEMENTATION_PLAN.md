# 🛠️ План реалізації DevTools для VetaleBrowser

## 📋 Загальний огляд

**Мета:** Створити повноцінне вікно Developer Tools (DevTools) в стилі інших вікон браузера з розділенням функціоналу по сторінках, як в професійних веб-браузерах (Chrome DevTools, Firefox Developer Tools).

**Дата створення:** 4 листопада 2025

---

## 🎯 Архітектура

### 1. Структура вікон

```
DevToolsWindow (головне вікно)
├── Elements Page (Інспектор DOM)
├── Console Page (Консоль JavaScript - окреме вікно)
├── Network Page (Мережа)
├── Sources Page (Джерела)
├── Performance Page (Продуктивність)
├── Application Page (Застосунок/Storage)
└── HTML Editor Page (Редактор HTML)
```

### 2. Окремі вікна

- **ConsoleWindow** - вже існує, буде використовуватися для логів помилок
- **DevToolsWindow** - нове головне вікно з вкладками

---

## 📁 Структура файлів

### Нові файли для створення:

```
VetaleBrowser/VetaleBrowser.DevTools/
├── Pages/
│   ├── DevToolsMainPage.axaml       # Головна сторінка з вибором інструментів
│   ├── DevToolsMainPage.axaml.cs
│   ├── ElementsPage.axaml            # Інспектор DOM елементів
│   ├── ElementsPage.axaml.cs
│   ├── NetworkPage.axaml             # Мережеві запити
│   ├── NetworkPage.axaml.cs
│   ├── SourcesPage.axaml             # Перегляд джерел
│   ├── SourcesPage.axaml.cs
│   ├── PerformancePage.axaml         # Аналіз продуктивності
│   ├── PerformancePage.axaml.cs
│   ├── ApplicationPage.axaml         # Storage, кеш, cookies
│   ├── ApplicationPage.axaml.cs
│   ├── HtmlEditorPage.axaml          # Редактор HTML
│   └── HtmlEditorPage.axaml.cs
├── Models/
│   ├── DomElement.cs                 # Модель DOM елемента
│   ├── NetworkRequest.cs             # Модель мережевого запиту
│   ├── StorageItem.cs                # Модель збереженого елемента
│   └── ConsoleMessage.cs             # Модель повідомлення консолі
├── Services/
│   ├── DomInspectorService.cs        # Сервіс інспекції DOM
│   ├── NetworkMonitorService.cs      # Сервіс моніторингу мережі
│   ├── PerformanceService.cs         # Сервіс аналізу продуктивності
│   └── HtmlEditorService.cs          # Сервіс редагування HTML
└── Controls/
    ├── DomTreeView.axaml             # Контрол для дерева DOM
    ├── DomTreeView.axaml.cs
    ├── CodeEditor.axaml              # Контрол редактора коду
    └── CodeEditor.axaml.cs

VetaleBrowser/VetaleBrowser.UI/Windows/
└── DevToolsWindow.axaml              # Головне вікно DevTools
└── DevToolsWindow.axaml.cs
```

---

## 🎨 Дизайн вікна DevToolsWindow

### Стиль вікна (як ToolsWindow/HistoryWindow):
- Orange gradient background (`#FF8B4513`)
- Gray gradient top bar (`GrayGradient`)
- Системні кнопки: мінімізувати, закрити
- Кнопка "Vetale Browser" для переходу до головного вікна
- Іконка DevTools в топ-барі
- Зелені кнопки (`#4CAF50`)

### Структура:
```xml
<Grid RowDefinitions="Auto,Auto,*">
  <!-- Row 0: Top Bar (42px) -->
  <!-- Row 1: Tabs Navigation (40px) -->
  <!-- Row 2: Content Area -->
</Grid>
```

---

## 📄 Функціонал сторінок

### 1️⃣ **DevToolsMainPage** - Головна сторінка

**Призначення:** Навігація по всіх інструментах DevTools

**Елементи:**
- Заголовок "🔧 Developer Tools"
- Список інструментів з іконками та описами:
  - 🔍 Elements - Інспектор DOM
  - 🖥️ Console - Консоль (відкриває окреме вікно)
  - 🌐 Network - Мережеві запити
  - 📁 Sources - Перегляд джерел
  - ⚡ Performance - Продуктивність
  - 💾 Application - Storage та кеш
  - ✏️ HTML Editor - Редактор HTML

**Стиль:** Оранжевий фон `#FF9800`, картки інструментів як в ToolsMainPage

---

### 2️⃣ **ElementsPage** - Інспектор DOM

**Призначення:** Перегляд та редагування DOM структури поточної сторінки

**Компоненти:**
- **Ліва панель (50%)**: Дерево DOM елементів
  - TreeView з ієрархією елементів
  - Підсвітка синтаксису HTML
  - Можливість згортання/розгортання вузлів
  - Пошук елементів

- **Права панель (50%)**: Властивості вибраного елемента
  - **Styles**: CSS стилі (computed, inline, inherited)
  - **Attributes**: HTML атрибути
  - **Properties**: JavaScript властивості
  - **Event Listeners**: Обробники подій
  - **Accessibility**: Дані доступності

**Функціонал:**
- Вибір елемента на сторінці (picker mode)
- Редагування HTML
- Редагування CSS в реальному часі
- Додавання/видалення атрибутів
- Копіювання HTML, CSS, XPath, selector

**Технічна реалізація:**
- Використання CefSharp DevTools Protocol
- Метод `Runtime.Evaluate` для отримання DOM
- Метод `DOM.getDocument`, `DOM.querySelector`
- Підсвітка синтаксису з AvaloniaEdit

---

### 3️⃣ **NetworkPage** - Мережеві запити

**Призначення:** Моніторинг всіх HTTP/HTTPS запитів

**Компоненти:**
- **Панель фільтрів** (верх):
  - All, XHR, JS, CSS, Img, Media, Font, Doc, WS, Other
  - Пошук по URL
  - Фільтр по статусу (200, 404, 500+)

- **Таблиця запитів** (основна частина):
  | Name | Status | Type | Size | Time | Waterfall |
  |------|--------|------|------|------|-----------|
  | ... | ... | ... | ... | ... | график |

- **Деталі запиту** (нижня панель - splitter):
  - **Headers**: Request/Response headers
  - **Preview**: Попередній перегляд відповіді
  - **Response**: Тіло відповіді
  - **Timing**: Детальний timing діаграма
  - **Cookies**: Cookies запиту/відповіді

**Функціонал:**
- Запис/Стоп запису мережі
- Очистити логи
- Експорт HAR файлу
- Фільтрація запитів
- Пошук в запитах
- Копіювання запиту як cURL, Fetch

**Технічна реалізація:**
- `IRequestHandler` в CefSharp для перехоплення запитів
- `OnBeforeResourceLoad`, `OnResourceLoadComplete`
- Збереження даних в `ObservableCollection<NetworkRequest>`
- DataGrid для відображення

---

### 4️⃣ **SourcesPage** - Перегляд джерел

**Призначення:** Перегляд та налагодження JavaScript, CSS, HTML файлів

**Компоненти:**
- **Ліва панель (25%)**: Дерево файлів
  - Ієрархія завантажених ресурсів
  - Page, Scripts, Stylesheets, Images, Fonts, etc.
  - Пошук файлів

- **Центральна панель (55%)**: Редактор коду
  - Підсвітка синтаксису для JS, CSS, HTML, JSON
  - Нумерація рядків
  - Брейкпоінти (для JS)
  - Пошук та заміна
  - Форматування коду (Prettify)

- **Права панель (20%)**: Інструменти налагодження
  - **Breakpoints**: Список брейкпоінтів
  - **Call Stack**: Стек викликів
  - **Scope**: Локальні змінні
  - **Watch**: Слідкування за змінними

**Функціонал:**
- Перегляд всіх завантажених файлів
- Редагування JavaScript (з перезавантаженням)
- Встановлення брейкпоінтів
- Step over/into/out
- Форматування мініфікованого коду
- Збереження змін локально
- Пошук в файлах (Ctrl+Shift+F)

**Технічна реалізація:**
- `Debugger` domain в DevTools Protocol
- AvaloniaEdit для редактора з підсвіткою
- TextMate Grammar для синтаксису
- `Debugger.setBreakpoint`, `Debugger.pause`, `Debugger.resume`

---

### 5️⃣ **PerformancePage** - Аналіз продуктивності

**Призначення:** Профілювання продуктивності сторінки

**Компоненти:**
- **Панель керування** (верх):
  - Кнопка "Record" (Почати запис)
  - Кнопка "Stop" (Зупинити)
  - Кнопка "Clear" (Очистити)
  - Кнопка "Load profile" (Завантажити)
  - Кнопка "Save profile" (Зберегти)

- **Timeline діаграма** (основна частина):
  - CPU usage график
  - Network activity
  - Screenshots (опціонально)
  - Timeline подій (Scripting, Rendering, Painting)

- **Деталі** (нижня панель):
  - **Summary**: Загальна статистика (Scripting, Rendering, Painting, Idle)
  - **Bottom-Up**: Функції по часу виконання
  - **Call Tree**: Дерево викликів
  - **Event Log**: Детальний лог подій

**Функціонал:**
- Запис CPU профілю
- Запис Memory профілю
- Скріншоти під час запису
- Flame chart
- Експорт/Імпорт профілів
- Фільтрація по категоріях

**Технічна реалізація:**
- `Profiler` domain в DevTools Protocol
- `Profiler.start`, `Profiler.stop`
- `Performance.getMetrics`
- Візуалізація з Canvas або OxyPlot

---

### 6️⃣ **ApplicationPage** - Застосунок/Storage

**Призначення:** Управління даними застосунку (Storage, Cache, Cookies)

**Компоненти:**
- **Ліва панель (30%)**: Дерево категорій
  - 🍪 **Cookies**
  - 💾 **Local Storage**
  - 📦 **Session Storage**
  - 🗄️ **IndexedDB**
  - 📂 **Cache Storage**
  - 🌐 **Service Workers**
  - 🖼️ **Frames**

- **Права панель (70%)**: Таблиця даних
  | Key | Value | Domain | Path | Expires | Size |
  |-----|-------|--------|------|---------|------|
  | ... | ... | ... | ... | ... | ... |

**Функціонал для Cookies:**
- Перегляд всіх cookies
- Додати новий cookie
- Редагувати cookie
- Видалити cookie/cookies
- Очистити всі cookies
- Фільтрація по домену
- Пошук cookies

**Функціонал для Storage:**
- Перегляд ключів та значень
- Додати/Редагувати/Видалити запис
- Очистити storage
- Експорт в JSON

**Функціонал для Cache:**
- Перегляд кешованих ресурсів
- Видалення кешу
- Оновлення кешу

**Технічна реалізація:**
- `Storage` domain: `DOMStorage.getDOMStorageItems`
- `Network` domain: `Network.getCookies`, `Network.deleteCookies`
- `IndexedDB` domain: `IndexedDB.requestDatabaseNames`
- `CacheStorage` domain: `CacheStorage.requestCacheNames`
- DataGrid для таблиць

---

### 7️⃣ **HtmlEditorPage** - Редактор HTML

**Призначення:** Професійний редактор HTML з live preview

**Компоненти:**
- **Верхня панель**: Toolbar з інструментами
  - 📁 New, Open, Save, Save As
  - ↶ Undo, Redo
  - ✂️ Cut, Copy, Paste
  - 🔍 Find, Replace
  - ▶️ Preview/Edit mode toggle
  - 🎨 Format code (Prettify)
  - ✔️ Validate HTML

- **Головна область** (Split Panel):
  - **Ліва панель (50%)**: Code Editor
    - AvaloniaEdit з підсвіткою HTML
    - Нумерація рядків
    - Автодоповнення тегів
    - Підсвітка парних тегів
    - Складання блоків коду
    - Міні-мапа коду (опціонально)

  - **Права панель (50%)**: Live Preview
    - CefSharp WebView для preview
    - Автоматичне оновлення при зміні коду
    - Режим повного екрану для preview

- **Нижня панель**: Інформаційна панель
  - Лінія:Колонка
  - Кількість рядків, символів
  - Кодування файлу
  - Статус валідації
  - Помилки/Попередження

**Функціонал:**
- Підсвітка синтаксису HTML, CSS, JavaScript
- Автодоповнення тегів (<div> → </div>)
- Emmet abbreviations (div.container>ul>li*5)
- Форматування коду (HTML Beautifier)
- Валідація HTML (W3C Validator або власний)
- Live preview з автооновленням
- Збереження в файл
- Відкриття існуючих файлів
- Експорт в PDF (опціонально)
- Пошук та заміна з regex
- Множинний курсор (Ctrl+Click)
- Вибір теми редактора (Light/Dark)

**Шаблони (Templates):**
- HTML5 Boilerplate
- Blank HTML
- Bootstrap template
- Responsive layout

**Технічна реалізація:**
- **AvaloniaEdit** для редактора
- **TextMate Grammar** для підсвітки синтаксису
- **HtmlAgilityPack** для парсингу та валідації
- **CefSharp** для live preview
- Debouncing для автооновлення preview (500ms затримка)
- **HtmlBeautifier/PrettyPrint** для форматування

---

## 🔧 Технічна реалізація

### Залежності (NuGet пакети):

```xml
<!-- VetaleBrowser.DevTools.csproj -->
<PackageReference Include="Avalonia" Version="11.0.0" />
<PackageReference Include="AvaloniaEdit" Version="11.0.0" />
<PackageReference Include="CefSharp.Wpf" Version="120.0.0" />
<PackageReference Include="Newtonsoft.Json" Version="13.0.3" />
<PackageReference Include="HtmlAgilityPack" Version="1.11.54" />
<PackageReference Include="OxyPlot.Avalonia" Version="2.1.0" />
```

### Інтеграція з CefSharp DevTools Protocol:

```csharp
// Отримання DevTools client
var devToolsClient = chromiumWebBrowser.GetDevToolsClient();

// Виклик методів DevTools Protocol
var result = await devToolsClient.SendAsync(new 
    DevToolsMethodParams("DOM.getDocument"));

// Runtime Evaluation
var jsResult = await devToolsClient.ExecuteDevToolsMethodAsync(
    "Runtime.evaluate", 
    new { expression = "document.body.innerHTML" });
```

---

## 🎨 UI/UX Особливості

### Навігація між сторінками:
- Горизонтальні табки в DevToolsWindow
- TabControl з кастомним стилем
- Іконки для кожної вкладки
- Підсвітка активної вкладки зеленим кольором

### Розділення панелей:
- GridSplitter для змінення розміру панелей
- Збереження позиції splitter в настройках
- Можливість згортання бічних панелей

### Теми:
- Light theme (за замовчуванням)
- Dark theme (для редактора коду)
- Збереження вибраної теми в настройках

### Клавіатурні скорочення:
- `F12` - Відкрити/Закрити DevTools
- `Ctrl+Shift+C` - Inspect element
- `Ctrl+Shift+I` - Відкрити DevTools (Elements)
- `Ctrl+Shift+J` - Відкрити Console
- `Ctrl+F` - Пошук
- `Ctrl+S` - Зберегти (в HTML Editor)
- `F5` - Refresh preview

---

## 📊 Інтеграція з ConsoleWindow

**ConsoleWindow вже існує** - буде використовуватися для перегляду логів та помилок.

### Зміни в ConsoleWindow:
- ✅ Вже виправлені витоки пам'яті
- Додати кнопку "Open in DevTools" для відкриття DevToolsWindow
- Синхронізація логів між Console та DevTools

### Виклик ConsoleWindow з DevTools:
```csharp
// В DevToolsMainPage.axaml.cs
private void OpenConsoleWindow()
{
    if (_consoleWindow == null || !_consoleWindow.IsVisible)
    {
        _consoleWindow = new ConsoleWindow();
        _consoleWindow.Show();
    }
    else
    {
        _consoleWindow.Activate();
    }
}
```

---

## 🚀 Етапи реалізації

### Етап 1: Створення базової структури ✅
- [ ] Створити папку VetaleBrowser.DevTools
- [ ] Створити DevToolsWindow.axaml/cs
- [ ] Створити DevToolsMainPage.axaml/cs
- [ ] Налаштувати навігацію по сторінках
- [ ] Додати виклик DevTools з головного вікна (кнопка F12)

### Етап 2: ElementsPage - Інспектор DOM
- [ ] Створити ElementsPage.axaml/cs
- [ ] Реалізувати DomTreeView контрол
- [ ] Інтеграція з CefSharp DOM API
- [ ] Відображення DOM дерева
- [ ] Панель властивостей елемента
- [ ] Редагування атрибутів та стилів

### Етап 3: NetworkPage - Мережа
- [ ] Створити NetworkPage.axaml/cs
- [ ] Реалізувати NetworkMonitorService
- [ ] Перехоплення HTTP запитів через IRequestHandler
- [ ] Таблиця запитів з фільтрацією
- [ ] Деталі запиту/відповіді
- [ ] Експорт HAR

### Етап 4: SourcesPage - Джерела
- [ ] Створити SourcesPage.axaml/cs
- [ ] Інтеграція AvaloniaEdit
- [ ] Дерево файлів
- [ ] Підсвітка синтаксису
- [ ] Базове налагодження (breakpoints)
- [ ] Форматування коду

### Етап 5: HtmlEditorPage - Редактор HTML ⭐
- [ ] Створити HtmlEditorPage.axaml/cs
- [ ] Інтеграція AvaloniaEdit з HTML підсвіткою
- [ ] Live preview з CefSharp
- [ ] Автодоповнення тегів
- [ ] Форматування коду (Prettify)
- [ ] Валідація HTML
- [ ] Збереження/Відкриття файлів
- [ ] Шаблони HTML

### Етап 6: ApplicationPage - Storage
- [ ] Створити ApplicationPage.axaml/cs
- [ ] Перегляд Cookies
- [ ] Управління Local/Session Storage
- [ ] IndexedDB viewer
- [ ] Cache viewer
- [ ] CRUD операції для всіх типів storage

### Етап 7: PerformancePage - Продуктивність
- [ ] Створити PerformancePage.axaml/cs
- [ ] Інтеграція з Profiler API
- [ ] Timeline візуалізація
- [ ] CPU/Memory профілювання
- [ ] Експорт/Імпорт профілів

### Етап 8: Фінальна інтеграція
- [ ] Тестування всіх сторінок
- [ ] Оптимізація продуктивності
- [ ] Виправлення витоків пам'яті
- [ ] Локалізація (UA/EN)
- [ ] Документація використання

---

## 🌐 Локалізація

### Ключі для локалізації (Languages.xaml):

```xml
<!-- DevTools Window -->
<s:String x:Key="DevTools.Title">Developer Tools</s:String>
<s:String x:Key="DevTools.Header">🔧 Developer Tools</s:String>

<!-- Tabs -->
<s:String x:Key="DevTools.Tab.Elements">Elements</s:String>
<s:String x:Key="DevTools.Tab.Console">Console</s:String>
<s:String x:Key="DevTools.Tab.Network">Network</s:String>
<s:String x:Key="DevTools.Tab.Sources">Sources</s:String>
<s:String x:Key="DevTools.Tab.Performance">Performance</s:String>
<s:String x:Key="DevTools.Tab.Application">Application</s:String>
<s:String x:Key="DevTools.Tab.HtmlEditor">HTML Editor</s:String>

<!-- HTML Editor -->
<s:String x:Key="HtmlEditor.New">New</s:String>
<s:String x:Key="HtmlEditor.Open">Open</s:String>
<s:String x:Key="HtmlEditor.Save">Save</s:String>
<s:String x:Key="HtmlEditor.Preview">Preview</s:String>
<s:String x:Key="HtmlEditor.Format">Format Code</s:String>
<s:String x:Key="HtmlEditor.Validate">Validate HTML</s:String>

<!-- Network -->
<s:String x:Key="Network.Name">Name</s:String>
<s:String x:Key="Network.Status">Status</s:String>
<s:String x:Key="Network.Type">Type</s:String>
<s:String x:Key="Network.Size">Size</s:String>
<s:String x:Key="Network.Time">Time</s:String>

<!-- Elements -->
<s:String x:Key="Elements.Styles">Styles</s:String>
<s:String x:Key="Elements.Computed">Computed</s:String>
<s:String x:Key="Elements.EventListeners">Event Listeners</s:String>
<s:String x:Key="Elements.Properties">Properties</s:String>
```

### Українська локалізація (Languages_UA.xaml):

```xml
<s:String x:Key="DevTools.Title">Інструменти розробника</s:String>
<s:String x:Key="DevTools.Header">🔧 Інструменти розробника</s:String>
<s:String x:Key="DevTools.Tab.Elements">Елементи</s:String>
<s:String x:Key="DevTools.Tab.Console">Консоль</s:String>
<s:String x:Key="DevTools.Tab.Network">Мережа</s:String>
<s:String x:Key="DevTools.Tab.Sources">Джерела</s:String>
<s:String x:Key="DevTools.Tab.Performance">Продуктивність</s:String>
<s:String x:Key="DevTools.Tab.Application">Застосунок</s:String>
<s:String x:Key="DevTools.Tab.HtmlEditor">Редактор HTML</s:String>
<s:String x:Key="HtmlEditor.New">Новий</s:String>
<s:String x:Key="HtmlEditor.Open">Відкрити</s:String>
<s:String x:Key="HtmlEditor.Save">Зберегти</s:String>
<s:String x:Key="HtmlEditor.Preview">Попередній перегляд</s:String>
<s:String x:Key="HtmlEditor.Format">Форматувати код</s:String>
<s:String x:Key="HtmlEditor.Validate">Перевірити HTML</s:String>
```

---

## 📝 Приклад використання

### Відкриття DevTools з MainWindow:

```csharp
// В MainWindow.axaml.cs
private DevToolsWindow? _devToolsWindow;

private void OpenDevTools()
{
    if (_devToolsWindow == null || !_devToolsWindow.IsVisible)
    {
        _devToolsWindow = new DevToolsWindow();
        _devToolsWindow.Closed += (s, e) => _devToolsWindow = null;
        _devToolsWindow.Show();
    }
    else
    {
        _devToolsWindow.Activate();
    }
}

// Обробник клавіші F12
protected override void OnKeyDown(KeyEventArgs e)
{
    if (e.Key == Key.F12)
    {
        OpenDevTools();
        e.Handled = true;
    }
    base.OnKeyDown(e);
}
```

---

## ✅ Критерії успіху

- [ ] DevTools відкривається по F12
- [ ] Всі 7 сторінок реалізовані та функціональні
- [ ] HTML Editor має live preview та працює коректно
- [ ] Elements показує актуальний DOM
- [ ] Network відображає всі HTTP запити
- [ ] Консоль показує логи та помилки (окреме вікно)
- [ ] Відсутні витоки пам'яті
- [ ] Повна локалізація UA/EN
- [ ] Професійний вигляд як в Chrome DevTools

---

## 📚 Посилання та ресурси

- [Chrome DevTools Protocol](https://chromedevtools.github.io/devtools-protocol/)
- [CefSharp DevTools](https://github.com/cefsharp/CefSharp/wiki/General-Usage#devtools)
- [AvaloniaEdit Documentation](https://github.com/AvaloniaUI/AvaloniaEdit)
- [TextMate Grammars](https://github.com/microsoft/vscode-textmate)
- [HTML Agility Pack](https://html-agility-pack.net/)

---

## 🎯 Пріоритети реалізації

### Високий пріоритет (Must Have):
1. ✅ DevToolsWindow з навігацією
2. ✅ ElementsPage - Інспектор DOM
3. ✅ **HtmlEditorPage** - Редактор HTML ⭐ (найважливіше!)
4. ✅ NetworkPage - Мережа
5. ✅ Console (вже є як окреме вікно)

### Середній пріоритет (Should Have):
6. SourcesPage - Перегляд джерел
7. ApplicationPage - Storage

### Низький пріоритет (Nice to Have):
8. PerformancePage - Профілювання

---

**Автор:** VetaleBrowser Team  
**Версія:** 1.0  
**Дата:** 4 листопада 2025  
**Статус:** 📋 План створено, очікує реалізації

