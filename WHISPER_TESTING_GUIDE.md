# Тестування голосового пошуку Whisper.NET

## Швидка перевірка функціональності

### Крок 1: Запустіть VetaleBrowser

```powershell
cd E:\VetaleBrowser
dotnet run --configuration Release
```

### Крок 2: Перевірте завантаження модулів

У Debug Output повинні з'явитися:
```
Loaded Assembly 'NAudio.WinMM.dll'
Loaded Assembly 'NAudio.Core.dll'
Loaded Assembly 'Whisper.net.dll'
```

✅ Якщо бачите ці повідомлення - модулі завантажені!

### Крок 3: Тестуйте голосовий пошук

1. **Відкрийте домашню сторінку VetaleSearch** (vetale://search або натисніть Home)

2. **Натисніть кнопку мікрофона** 🎤

3. **Перевірте консоль**:
   ```
   [VOICE][HOME] VoiceButton_Click
   [VOICE][HOME] IsAvailable=True
   [VOICE][HOME] StartListeningAsync
   [VoiceRecognition] Whisper processor initialized
   ```

4. **Говоріть запит** (наприклад, "привіт світ" або "hello world")

5. **Натисніть стоп** ⏹️

6. **Перевірте консоль**:
   ```
   [VoiceRecognition] Processing X bytes of audio
   [VoiceRecognition] Converted to X float samples
   [VoiceRecognition] Segment: <ваш текст>
   [VoiceRecognition] Recognized text: <ваш текст>
   [VOICE][HOME] ✓ Voice text recognized: '<ваш текст>'
   [VOICE][HOME] Setting search input text...
   [VOICE][HOME] ✓ Search input text set to: '<ваш текст>'
   [VOICE][HOME] Performing search...
   [VetaleSearchHomePage] PerformSearch called
   [VetaleSearchHomePage] Query: '<ваш текст>', Engine: 0
   [VetaleSearchHomePage] Generated Vetale Search URL: vetale://search/results?q=...
   [VetaleSearchHomePage] ✓ NavigateRequested invoked
   ```

7. **Результат**: Автоматично відкриється сторінка результатів пошуку з вашим запитом

### Очікувана поведінка

✅ **Правильно**:
- Кнопка мікрофона змінюється на ⏹️ під час запису
- Після зупинки з'являється ⏳ (обробка)
- Текст з'являється в полі пошуку
- Автоматично виконується пошук через Vetale Search
- Відкривається сторінка результатів

❌ **Неправильно**:
- Кнопка неактивна (перевірте мікрофон)
- Текст не з'являється (перевірте логи)
- Пошук не виконується (перевірте NavigateRequested)

## Діагностика проблем

### Проблема: Кнопка мікрофона неактивна

**Перевірте консоль**:
```
[VOICE][SERVICE] IsAvailable called
[VOICE][SERVICE] ModelExists=?, DeviceCount=?
```

- `ModelExists=false` → Модель не знайдена (але це не повинно статися з вбудованою моделлю!)
- `DeviceCount=0` → Мікрофон не підключено

**Рішення**:
1. Підключіть мікрофон
2. Надайте дозвіл Windows: Параметри → Конфіденційність → Мікрофон

### Проблема: Текст не розпізнається

**Перевірте консоль**:
```
[VoiceRecognition] Processing X bytes of audio
```

- Якщо `X = 0` → Мікрофон не записує
- Якщо немає сегментів → Модель не працює або аудіо занадто тихе

**Рішення**:
1. Говоріть голосніше
2. Перевірте рівень мікрофона в Windows
3. Говоріть 2-3 секунди мінімум

### Проблема: Пошук не виконується

**Перевірте консоль**:
```
[VOICE][HOME] ✓ Search input text set to: '<текст>'
[VOICE][HOME] Performing search...
[VetaleSearchHomePage] PerformSearch called
```

- Якщо `PerformSearch` не викликається → Перевірте OnVoiceTextRecognized
- Якщо `NavigateRequested is NULL` → Не підписано на подію

**Рішення**:
1. Перевірте, що MainWindow підписується на NavigateRequested
2. Перезапустіть браузер

## Тестові фрази

### Українська 🇺🇦
- "привіт світ"
- "котик"
- "погода сьогодні"
- "що таке програмування"

### Англійська 🇬🇧
- "hello world"
- "search for cats"
- "weather today"
- "programming tutorial"

### Російська 🇷🇺
- "привет мир"
- "погода сегодня"

### Змішані
Whisper автоматично визначить мову!

## Відомі обмеження

1. **Затримка обробки**: 1-3 секунди після зупинки (нормально для Whisper)
2. **Мінімальна тривалість**: Говоріть мінімум 1-2 секунди
3. **Фоновий шум**: Може погіршити якість розпізнавання

## Логи для відправки в баг-репорт

Якщо щось не працює, скопіюйте всі повідомлення з префіксами:
- `[VOICE][HOME]`
- `[VoiceRecognition]`
- `[VetaleSearchHomePage]`

---

**Готово до тестування!** 🚀

Запустіть браузер, натисніть 🎤, говоріть і перевіряйте логи!

