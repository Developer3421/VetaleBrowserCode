# Тестування повноекранного режиму для відео

## Реалізований функціонал

Додано підтримку повноекранного режиму для відео в WebView. Коли відео на веб-сторінці входить в fullscreen (наприклад, на YouTube), браузер автоматично приховує всі панелі UI і розширює WebView на весь екран вікна.

## Що було змінено

### MainWindow.axaml.cs

1. **Додано поля для зберігання посилань на UI елементи:**
   - `_tabBarRow` - панель вкладок
   - `_navigationBarRow` - панель навігації
   - `_webViewContainer` - контейнер WebView
   - `_isFullscreen` - стан fullscreen режиму
   - `_preFullscreenWindowState` - попередній стан вікна

2. **Реалізовано методи управління fullscreen:**
   - `EnterFullscreen()` - вхід в повноекранний режим
   - `ExitFullscreen()` - вихід з повноекранного режиму
   - `ToggleFullscreen()` - перемикання fullscreen режиму

3. **Обробка подій:**
   - `OnWorkerFullscreenChanged()` - реагує на зміну fullscreen стану в TabWorker
   - `OnWindowKeyDown()` - обробка клавіш F11 (toggle) та Escape (exit)

### Логіка EnterFullscreen()

При вході в fullscreen режим:
1. Приховуються панелі: `TabBarRow.IsVisible = false`, `NavigationBarRow.IsVisible = false`
2. WebView контейнер розширюється на всі 3 ряди Grid
3. Встановлюється `ZIndex = 1000` щоб контейнер був поверх інших елементів
4. Скидаються margins і padding
5. Вікно переходить в `WindowState.FullScreen`
6. Приховуються декорації вікна: `SystemDecorations = SystemDecorations.None`

### Логіка ExitFullscreen()

При виході з fullscreen режиму:
1. WebView контейнер повертається в ряд 2 (Grid.Row = 2, RowSpan = 1)
2. Відновлюється `ZIndex = 0`
3. Показуються панелі: `TabBarRow.IsVisible = true`, `NavigationBarRow.IsVisible = true`
4. Відновлюється padding вікна (8px)
5. Вікно повертається до попереднього стану
6. Відновлюються декорації: `SystemDecorations = SystemDecorations.BorderOnly`

## Як тестувати

### Варіант 1: Автоматичний fullscreen (від відео)

1. Запустіть браузер
2. Відкрийте YouTube або будь-який сайт з відео
3. Натисніть кнопку fullscreen в плеєрі відео
4. **Очікуваний результат:** 
   - Панель вкладок зникає
   - Панель навігації зникає
   - Відео займає весь екран
   - Вікно браузера в fullscreen режимі

5. Натисніть Escape або кнопку виходу з fullscreen в плеєрі
6. **Очікуваний результат:**
   - Панелі з'являються знову
   - Відео повертається до нормального розміру
   - Вікно повертається до попереднього стану

### Варіант 2: Ручний fullscreen (клавіатура)

1. Запустіть браузер
2. Натисніть F11
3. **Очікуваний результат:** 
   - Панелі зникають
   - WebView займає весь екран
   
4. Натисніть F11 знову або Escape
5. **Очікуваний результат:**
   - Все повертається до нормального вигляду

## Діагностика

В Debug виводі ви побачите повідомлення:
- `[MainWindow] InitializeComponent: TabBarRow found: True/False` - чи знайдено панель вкладок
- `[MainWindow] InitializeComponent: NavigationBarRow found: True/False` - чи знайдено панель навігації
- `[MainWindow] Worker fullscreen changed: True/False` - коли відео змінює стан fullscreen
- `[MainWindow] Entering fullscreen` - при вході в fullscreen
- `[MainWindow] Exiting fullscreen` - при виході з fullscreen
- Детальна інформація про кожен крок приховування/показу панелей

## Можливі проблеми та рішення

### Проблема: Панелі не зникають

**Можливі причини:**
1. Елементи не знайдено в XAML (перевірте Debug вивід)
2. Padding вікна не скидається (вже виправлено)
3. Grid.SetRow не працює правильно

**Рішення:**
- Перевірте Debug вивід при запуску
- Переконайтеся що в XAML є елементи з іменами: `TabBarRow`, `NavigationBarRow`, `WebViewContainer`

### Проблема: Fullscreen не спрацьовує від відео

**Можливі причини:**
1. TabWorker не детектує fullscreen стан
2. Подія `FullscreenChanged` не підписана

**Рішення:**
- Перевірте Debug вивід: `[TabWorker] Fullscreen polled: True/False`
- Перевірте що в `WireActiveWebViewPropertyChanged` є підписка на `FullscreenChanged`

## Технічні деталі

- **Polling інтервал:** TabWorker перевіряє fullscreen стан кожні 250мс
- **Підтримувані браузерні API:** `document.fullscreenElement`, `document.webkitFullscreenElement`, `document.msFullscreenElement`
- **Клавіші:** F11 (toggle), Escape (exit з fullscreen)

## Статус

✅ Реалізовано вхід в fullscreen
✅ Реалізовано вихід з fullscreen
✅ Додано діагностичні повідомлення
✅ Додано обробку клавіш F11 та Escape
✅ Інтегровано з TabWorker через подію FullscreenChanged
✅ Скидається Padding вікна в fullscreen режимі

