# Виправлення помилки BoxShadow у Vetale Search сторінках

## Проблема

При відкритті Vetale Search або створенні нової вкладки з'являлася помилка:

```
System.FormatException: Invalid color string: 'rgba(0'.
   at Avalonia.Media.Color.Parse(String s)
   at Avalonia.Media.BoxShadow.Parse(String s)
```

## Причина

У XAML файлах VetaleSearchHomePage та VetaleSearchResultsPage використовувався CSS-синтаксис для `BoxShadow` з кольорами у форматі `rgba()`, який **не підтримується в Avalonia**.

Avalonia очікує формат кольору `#AARRGGBB`, де:
- `AA` - альфа-канал (прозорість) у hex (00-FF)
- `RR` - червоний канал
- `GG` - зелений канал
- `BB` - синій канал

## Виправлення

### VetaleSearchHomePage.axaml

1. **BoxShadow для search-container** (рядок 42):
   - ❌ Було: `BoxShadow="0 4 16 rgba(0,0,0,0.15)"`
   - ✅ Стало: `BoxShadow="0 4 16 #26000000"`
   - Пояснення: 0.15 opacity = 15% = 0x26 в hex

2. **BoxShadow для engine selector** (рядок 151):
   - ❌ Було: `BoxShadow="0 2 8 rgba(0,0,0,0.1)"`
   - ✅ Стало: `BoxShadow="0 2 8 #1A000000"`
   - Пояснення: 0.1 opacity = 10% = 0x1A в hex

3. **Background для icon-button:pointerover** (рядок 93):
   - ❌ Було: `Background="rgba(0,0,0,0.05)"`
   - ✅ Стало: `Background="#0D000000"`
   - Пояснення: 0.05 opacity = 5% = 0x0D в hex

### VetaleSearchResultsPage.axaml

1. **BoxShadow для search-box-header** (рядок 34):
   - ❌ Було: `BoxShadow="0 2 8 rgba(0,0,0,0.1)"`
   - ✅ Стало: `BoxShadow="0 2 8 #1A000000"`

2. **BoxShadow для result-item:pointerover** (рядок 82):
   - ❌ Було: `BoxShadow="0 2 12 rgba(156,39,176,0.15)"`
   - ✅ Стало: `BoxShadow="0 2 12 #269C27B0"`
   - Пояснення: Фіолетовий колір #9C27B0 з 15% прозорістю = #269C27B0

## Конверсія opacity → hex

| Opacity | Відсоток | Hex  |
|---------|----------|------|
| 0.05    | 5%       | 0x0D |
| 0.10    | 10%      | 0x1A |
| 0.15    | 15%      | 0x26 |
| 0.20    | 20%      | 0x33 |
| 0.50    | 50%      | 0x80 |
| 1.00    | 100%     | 0xFF |

Формула: `hex_value = Math.Round(opacity * 255)`

## Результат

Тепер при виборі Vetale Search у налаштуваннях і відкритті нової вкладки:
- ✅ Помилка `Invalid color string` більше не з'являється
- ✅ VetaleSearchHomePage коректно відображається
- ✅ VetaleSearchResultsPage також працює без помилок
- ✅ Тіні (BoxShadow) відображаються коректно з правильною прозорістю

## Як перевірити

1. Запустити браузер
2. Відкрити налаштування → Пошукові системи
3. Вибрати **Vetale Search**
4. Натиснути **Save**
5. Перевірити, що вкладка оновлюється на Vetale Search Home без помилок
6. Створити нову вкладку (+) - має відкритися Vetale Search
7. Виконати пошук - перейти на сторінку результатів без помилок

