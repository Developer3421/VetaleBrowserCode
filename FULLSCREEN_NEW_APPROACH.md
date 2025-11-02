# Fullscreen - Новий Простий Підхід

## Змінений підхід

Відмовився від складного підходу з видаленням/додаванням елементів з Grid.
Тепер використовується простіший і надійніший підхід:

### EnterFullscreen()
1. **Приховує панелі** через `IsVisible = false`
   - TabBarRow.IsVisible = false
   - NavigationBarRow.IsVisible = false

2. **Встановлює висоту рядків Grid на 0**
   - `_mainGrid.RowDefinitions[0].Height = new GridLength(0)`
   - `_mainGrid.RowDefinitions[1].Height = new GridLength(0)`
   - Це гарантує що навіть invisible елементи не займають місце

3. **Скидає padding** вікна на 0

4. **Встановлює fullscreen режим**
   - WindowState.FullScreen
   - SystemDecorations.None

### ExitFullscreen()
1. **Відновлює висоту рядків Grid**
   - `_mainGrid.RowDefinitions[0].Height = GridLength.Auto`
   - `_mainGrid.RowDefinitions[1].Height = GridLength.Auto`

2. **Показує панелі**
   - TabBarRow.IsVisible = true
   - NavigationBarRow.IsVisible = true

3. **Відновлює padding** вікна (8px)

4. **Виходить з fullscreen**
   - SystemDecorations.BorderOnly
   - WindowState = попереднє значення

## Чому це має спрацювати

1. **Не маніпулюємо Children колекцією** - це часто проблематично
2. **Не змінюємо кількість RowDefinitions** - працюємо з існуючими
3. **Комбінуємо IsVisible + Height = 0** - подвійна гарантія що панелі зникнуть
4. **Простіший код** - менше місць для помилок

## Тестування

```cmd
cd E:\VetaleBrowser
dotnet run
```

Потім:
1. Натисніть F11
2. Або відкрийте YouTube і натисніть fullscreen на відео
3. Перевірте чи зникли панелі

Дивіться Debug вивід:
```
[MainWindow] === ENTERING FULLSCREEN ===
[MainWindow] TabBarRow before: IsVisible=True, Height=42
[MainWindow] TabBarRow after: IsVisible=False
[MainWindow] NavigationBarRow before: IsVisible=True, Height=36
[MainWindow] NavigationBarRow after: IsVisible=False
[MainWindow] Setting Grid row heights to 0
[MainWindow] === FULLSCREEN ENTERED ===
```

При виході:
```
[MainWindow] === EXITING FULLSCREEN ===
[MainWindow] Restoring Grid row heights
[MainWindow] Showing TabBarRow
[MainWindow] Showing NavigationBarRow
[MainWindow] === FULLSCREEN EXITED ===
```

## Якщо все ще не працює

Можливі причини:
1. **Подія fullscreen не спрацьовує** - перевірте чи є повідомлення `[MainWindow] Worker fullscreen changed`
2. **IsVisible не працює** - можливо потрібен Dispatcher.UIThread
3. **Grid не перераховує layout** - можливо потрібен InvalidateArrange()

Надішліть Debug вивід щоб я міг діагностувати проблему.

