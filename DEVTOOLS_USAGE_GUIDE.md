# 🔧 Як відкрити DevTools в VetaleBrowser

## Метод 1: Через вікно "Інструменти" ✅

1. Відкрийте браузер VetaleBrowser
2. Натисніть на кнопку "Інструменти" (🛠️) в головному вікні
3. У списку інструментів знайдіть **"Vetale DevTools"** з іконкою 🔧
4. Клікніть на назву або опис інструменту
5. Вікно Developer Tools відкриється

**Переваги:**
- ✅ Найпростіший спосіб
- ✅ Доступно з будь-якого місця
- ✅ Повторне використання вікна (не створює дубліkatів)

## Метод 2: Клавіатурне скорочення (TODO)

⚠️ **Ще не реалізовано**

Планується додати:
- `F12` - Відкрити/Закрити DevTools
- `Ctrl+Shift+I` - Відкрити DevTools (Elements)
- `Ctrl+Shift+J` - Відкрити Console

## Що доступно в DevTools

### 🏠 Головна сторінка
- Список всіх інструментів розробника
- Швидка навігація до потрібного інструменту

### ✏️ HTML Editor ⭐ (Працює)
- Створення нових HTML файлів
- Відкриття існуючих файлів
- Збереження файлів
- 4 готових шаблони (Blank, HTML5, Bootstrap, Responsive)
- Редактор коду з monospace шрифтом
- Панель статусу з інформацією про файл
- Toggle preview (приховати/показати preview)

**Можливості:**
- ✅ Новий файл (📄)
- ✅ Відкрити (📁)
- ✅ Зберегти (💾)
- ✅ Вибір шаблону
- ✅ Підрахунок символів та рядків
- ⚠️ Live Preview (потрібен CefSharp WebView)
- ⚠️ Підсвітка синтаксису (потрібен AvaloniaEdit)
- ⚠️ Форматування коду (TODO)
- ⚠️ Валідація HTML (TODO)

### 🔍 Elements (В розробці)
Планується:
- DOM tree viewer
- Element properties editor
- CSS styles inspector
- Event listeners viewer

### 🌐 Network (В розробці)
Планується:
- HTTP/HTTPS requests monitor
- Request/Response details
- HAR export

### 📁 Sources (В розробці)
Планується:
- Code viewer with syntax highlighting
- Debugging (breakpoints)
- Code formatting

### 💾 Application (В розробці)
Планується:
- Cookies management
- Local/Session Storage
- IndexedDB viewer
- Cache viewer

### ⚡ Performance (В розробці)
Планується:
- CPU profiling
- Memory profiling
- Timeline visualization

### 🖥️ Console (Працює як окреме вікно)
- Відкривається як окреме вікно
- Показує логи та помилки
- Автооновлення
- Експорт логів

## Навігація в DevTools

### Табки (верхня панель):
- 🏠 **Home** - Головна сторінка з переліком інструментів
- 🔍 **Elements** - Інспектор DOM (в розробці)
- 🌐 **Network** - Мережа (в розробці)
- 📁 **Sources** - Джерела (в розробці)
- ✏️ **HTML Editor** - Редактор HTML (працює!)
- 💾 **Application** - Storage (в розробці)
- ⚡ **Performance** - Продуктивність (в розробці)
- 🖥️ **Console** - Консоль (відкриває окреме вікно)

### Панель керування:
- **Vetale Browser** - повернутися до головного вікна
- **Minimize** - згорнути вікно
- **Close** - закрити вікно

## Приклад використання HTML Editor

1. Відкрийте DevTools → вкладка **HTML Editor**
2. Виберіть шаблон зі списку (наприклад, "HTML5 Boilerplate")
3. Редагуйте код в лівій панелі
4. (Опціонально) Сховайте preview, якщо він не потрібен
5. Збережіть файл: **💾 Save**
6. Введіть назву файлу та оберіть розташування
7. Готово! Файл збережено

## Локалізація

DevTools повністю підтримує локалізацію:
- 🇬🇧 English
- 🇺🇦 Українська

Мова змінюється автоматично згідно з налаштуваннями браузера.

## Технічні деталі

### Архітектура:
- **Вікно:** `DevToolsWindow` (Windows/DevToolsWindow.axaml)
- **Сторінки:** Окремі UserControl в папці Pages/
- **Навігація:** TabControl з динамічним ContentControl
- **Memory Management:** Статичне посилання для уникнення витоків

### Повторне використання вікна:
При повторному відкритті DevTools:
- Якщо вікно вже відкрите → активується існуюче
- Якщо вікно закрите → створюється нове
- Автоматичне очищення посилань при закритті

## Troubleshooting

### DevTools не відкривається
1. Перевірте, чи встановлені всі залежності
2. Подивіться логи в Debug output
3. Переконайтеся, що проект скомпільовано без помилок

### Preview не показує HTML
⚠️ Live Preview ще не реалізований - потрібна інтеграція CefSharp WebView

### Немає підсвітки синтаксису
⚠️ Підсвітка синтаксису ще не реалізована - потрібен AvaloniaEdit

## Плани на майбутнє

### Високий пріоритет:
- [ ] F12 клавіша для відкриття DevTools з головного вікна
- [ ] Live Preview в HTML Editor (CefSharp)
- [ ] Підсвітка синтаксису (AvaloniaEdit)
- [ ] Elements Page - DOM inspector

### Середній пріоритет:
- [ ] Network Page - requests monitor
- [ ] Sources Page - code viewer
- [ ] HTML автодоповнення
- [ ] Форматування коду

### Низький пріоритет:
- [ ] Application Page - storage manager
- [ ] Performance Page - profiling
- [ ] Темна тема для редактора

---

**Автор:** VetaleBrowser Team  
**Версія:** 1.0  
**Дата:** 4 листопада 2025  
**Статус:** ✅ Базова версія працює

