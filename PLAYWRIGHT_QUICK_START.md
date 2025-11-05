# 🚀 Швидкий старт - Playwright DevTools

## Що зроблено ✅

Playwright Chromium тепер працює **на вимогу** (lazy initialization) - запускається тільки коли потрібно!

### Ключові особливості:

- ❌ **НЕ запускається** автоматично при відкритті DevTools
- ✅ **Запускається** тільки при натисканні кнопки завантаження URL
- ✅ **Економить ресурси** - Chromium НЕ працює в фоні без потреби
- ✅ **Швидкий старт** - DevTools відкривається миттєво
- ✅ **Singleton** - один Chromium процес на всю програму

## Встановлення (один раз)

Після компіляції проекту запустіть:

```bash
# Windows
e:\VetaleBrowser\install_playwright.bat
```

Або вручну:

```bash
pwsh -File VetaleBrowser\bin\Debug\net9.0\playwright.ps1 install chromium
```

## Як працює

### При відкритті DevTools:

1. **DevTools відкривається** - показує UI
2. **Playwright НЕ запускається** - економить ресурси
3. **Моніторинг вкладок працює** - показує інформацію про поточну вкладку
4. **Chromium НЕ запущений** - немає фонових процесів

### При натисканні кнопки:

1. **Користувач натискає кнопку** (Load Current Tab / Go / Capture DOM)
2. **Playwright ініціалізується** (перший раз)
3. **Chromium запускається** - видимий браузер
4. **Відбувається аналіз** - DOM/Performance/Resources/Storage
5. **Дані зберігаються** - в базу даних
6. **Результати показуються** - в DevTools UI

**Потік:**
```
Кнопка → Lazy Init Playwright → Chromium запуск → URL поточної вкладки → Аналіз → База даних → UI
```

## Використання

### Варіант 1: Через вкладку "WebView Worker"

1. Запустіть VetaleBrowser
2. Відкрийте будь-яку сторінку у вкладці
3. Натисніть F12 (DevTools)
4. Перейдіть на вкладку "WebView Worker"
5. Побачите інформацію про поточну вкладку
6. Натисніть кнопку **"📥 Load Current Tab"**
7. **ТЕПЕР** Playwright ініціалізується і відкриє Chromium
8. Можете використовувати інші вкладки DevTools

### Варіант 2: Прямо з Playwright вкладок

1. Запустіть VetaleBrowser
2. Відкрийте будь-яку сторінку
3. Натисніть F12 (DevTools)
4. Перейдіть на одну з Playwright вкладок:
   - **Playwright Elements** → натисніть "Capture DOM"
   - **Playwright Performance** → натисніть "Capture Performance"
   - **Playwright Sources** → натисніть "Capture Resources"
   - **Playwright Application** → натисніть "Capture Storage"
5. **При першому натисканні** Playwright ініціалізується автоматично
6. Відбудеться аналіз поточної вкладки

**Примітка**: Браузер Chromium відкриється у видимому режимі (не headless) для зручності налагодження.

## Важливо! ⚠️

### НЕ запускається автоматично:

- ❌ При відкритті DevTools
- ❌ При зміні активної вкладки
- ❌ При навігації у вкладці

### Запускається тільки:

- ✅ При натисканні кнопки "Load Current Tab"
- ✅ При натисканні кнопки "Go" (ввести URL вручну)
- ✅ При натисканні кнопки "Capture DOM/Performance/Resources/Storage"

**Це дає вам повний контроль над тим, коли запускати Playwright!**

## Переваги

- 🚀 **Швидкий старт** - DevTools відкривається миттєво, без затримок
- 💾 **Економія ресурсів** - Chromium НЕ працює коли не потрібен
- 🎯 **Контроль** - користувач сам вирішує коли запустити аналіз
- 🔄 **Singleton** - тільки один Chromium процес для всіх DevTools вкладок
- ✨ **Простота** - просто натисніть кнопку коли потрібен аналіз

## Компіляція ✅

```bash
cd e:\VetaleBrowser
dotnet build VetaleBrowser.sln
```

Результат: **Build successful!** (14 warnings - це норма)

## Документація

- 📄 `PLAYWRIGHT_LAZY_INITIALIZATION.md` - Детальний опис lazy initialization
- 📄 `PLAYWRIGHT_AUTO_SYNC_IMPLEMENTATION.md` - Попередня версія (auto-sync)
- 📄 `PLAYWRIGHT_INTEGRATION_COMPLETE.md` - Повна документація
- 📄 `playwright-test-example.cs` - Приклад коду

## Що далі?

Просто встановіть Playwright браузери та користуйтесь DevTools з lazy initialization! 🎉

### Тестування:

1. ✅ Відкрийте DevTools (F12) - має бути швидко
2. ✅ Перевірте Task Manager - НЕ має бути процесу Chromium
3. ✅ Натисніть "Load Current Tab"
4. ✅ Перевірте Task Manager - має з'явитись Chromium процес
5. ✅ Chromium браузер відкриється і покаже поточну сторінку
6. ✅ Можете використовувати всі DevTools функції



