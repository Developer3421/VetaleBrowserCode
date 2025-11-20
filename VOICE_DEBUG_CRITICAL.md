# КРИТИЧНА ДІАГНОСТИКА: Чому не працює голосовий пошук

## Що було зроблено

Додано **ДУЖЕ ВИДИМЕ** логування на всіх критичних етапах:

### 1. Конструктор VetaleSearchHomePage
```
[VOICE][HOME] VetaleSearchHomePage constructor called
[VOICE][HOME] VetaleSearchHomePage constructor completed
```

### 2. SetVoiceRecognitionService
```
═══════════════════════════════════════════════════════
[VOICE][HOME] ✓✓✓ SetVoiceRecognitionService CALLED ✓✓✓
═══════════════════════════════════════════════════════
```

### 3. VoiceButton_Click
```
🎤🎤🎤🎤🎤🎤🎤🎤🎤🎤🎤🎤🎤🎤🎤🎤🎤🎤🎤🎤🎤🎤🎤🎤🎤🎤🎤🎤🎤
🎤 [VOICE][HOME] VOICE BUTTON CLICKED!!! 🎤
🎤🎤🎤🎤🎤🎤🎤🎤🎤🎤🎤🎤🎤🎤🎤🎤🎤🎤🎤🎤🎤🎤🎤🎤🎤🎤🎤🎤🎤
```

### 4. InternalUrlHandler
```
[InternalUrlHandler] CreateSearchHomePage called for: vetale://search
[InternalUrlHandler] GlobalVoiceRecognitionService null? False/True
[InternalUrlHandler] Calling SetVoiceRecognitionService...
```

---

## Інструкції з тестування

### Крок 1: Збери проект
```powershell
cd E:\VetaleBrowser
dotnet build --configuration Release
```

### Крок 2: Запусти браузер

У Debug Output шукай:

#### При старті браузера:
```
[VOICE][MAIN] Global services configured. VoiceService null?=False
```
- Якщо `null?=True` → сервіс не створився

#### При відкритті домашньої сторінки:
```
[InternalUrlHandler] CreateSearchHomePage called for: vetale://search
[VOICE][HOME] VetaleSearchHomePage constructor called
═══════════════════════════════════════════════════════
[VOICE][HOME] ✓✓✓ SetVoiceRecognitionService CALLED ✓✓✓
═══════════════════════════════════════════════════════
[VOICE][HOME] ✓ Successfully subscribed to all voice events
```

#### При натисканні на 🎤:
```
🎤🎤🎤🎤🎤🎤🎤🎤🎤🎤🎤🎤🎤🎤🎤🎤🎤🎤🎤🎤🎤🎤🎤🎤🎤🎤🎤🎤🎤
🎤 [VOICE][HOME] VOICE BUTTON CLICKED!!! 🎤
🎤 [VOICE][HOME] Service null? False
🎤🎤🎤🎤🎤🎤🎤🎤🎤🎤🎤🎤🎤🎤🎤🎤🎤🎤🎤🎤🎤🎤🎤🎤🎤🎤🎤🎤🎤
```

Потім:
```
[VOICE][HOME] Checking IsAvailable...
[VOICE][SERVICE] IsAvailable called. modelPath=...
[VOICE][SERVICE] ModelExists=True, DeviceCount=1
[VOICE][HOME] IsAvailable = True
[VOICE][HOME] ✓ Starting voice recognition...
[VOICE][SERVICE] StartListeningAsync called
[VOICE][SERVICE] Initializing recognizer...
[VoiceRecognition] Found local Whisper model at: ...\Models\whisper\ggml-base.bin
[VoiceRecognition] Loading custom Whisper model from: ...
[VoiceRecognition] Whisper processor initialized successfully
[VOICE][SERVICE] Starting recording...
[VoiceRecognition] Recording started
[VoiceRecognition] Audio buffer size: 3200 bytes
[VoiceRecognition] Audio buffer size: 6400 bytes
...
```

---

## Діагностика за логами

### Сценарій 1: Нема взагалі жодних логів `[VOICE]`

**Проблема**: Debug Output не показує `Debug.WriteLine`

**Рішення**:
1. У Rider відкрий: **Run → Debug → Output**
2. Переконайся, що вибрано процес VetaleBrowser
3. Перевір, чи є там хоч щось (наприклад, `[MainWindow]` логи)

### Сценарій 2: Є лог конструктора, але НЕ `SetVoiceRecognitionService`

**Проблема**: `InternalUrlHandler.GlobalVoiceRecognitionService == null`

**Лог буде**:
```
[InternalUrlHandler] GlobalVoiceRecognitionService null? True
[InternalUrlHandler] WARNING: GlobalVoiceRecognitionService is NULL!
```

**Рішення**:
- Перевір `OnWindowLoaded` у MainWindow
- Має бути: `InternalUrlHandler.GlobalVoiceRecognitionService = _voiceRecognitionService;`

### Сценарій 3: Є `SetVoiceRecognitionService`, але НЕ `VoiceButton_Click`

**Проблема**: Кнопка не прив'язана або не той екземпляр сторінки

**Лог буде**:
```
═══════════════════════════════════════════════════════
[VOICE][HOME] ✓✓✓ SetVoiceRecognitionService CALLED ✓✓✓
═══════════════════════════════════════════════════════
(але при натисканні на 🎤 нічого не відбувається)
```

**Рішення**:
- Перевір XAML: `<Button x:Name="VoiceButton" Click="VoiceButton_Click">`
- Можливо, кнопка в іншому instance сторінки

### Сценарій 4: Є `VoiceButton_Click`, але сервіс NULL

**Лог буде**:
```
🎤 [VOICE][HOME] VOICE BUTTON CLICKED!!! 🎤
🎤 [VOICE][HOME] Service null? True
[VOICE][HOME] ✗✗✗ Voice recognition service NOT INITIALIZED ✗✗✗
```

**Це означає**: між `SetVoiceRecognitionService` і кліком сторінку перестворили

**Рішення**:
- Перевір навігацію: можливо, сторінка створюється двічі
- Додай лог у `Dispose` або `Unload` події VetaleSearchHomePage

### Сценарій 5: Все є, але `IsAvailable = False`

**Лог буде**:
```
[VOICE][HOME] IsAvailable = False
[VOICE][SERVICE] ModelExists=False, DeviceCount=0
```

**Якщо ModelExists=False**:
- Модель не знайдена
- Перевір: `E:\VetaleBrowser\VetaleBrowser\bin\Release\net10.0\win-x64\Models\whisper\ggml-base.bin`

**Якщо DeviceCount=0**:
- Мікрофон не підключено
- Перевір налаштування Windows

### Сценарій 6: StartListeningAsync викликається, але падає

**Лог буде**:
```
[VOICE][SERVICE] StartListeningAsync called
[VOICE][SERVICE] Initializing recognizer...
[VoiceRecognition] Failed to start: <помилка>
```

**Дивись на текст помилки** — це скаже, що саме не так (модель, мікрофон, Whisper.NET)

### Сценарій 7: Запис йде, але нічого не розпізнається

**Лог буде**:
```
[VoiceRecognition] Recording started
[VoiceRecognition] Audio buffer size: 3200 bytes
...
[VoiceRecognition] Recording stopped, processing audio...
[VoiceRecognition] Processing 320000 bytes of audio
[VoiceRecognition] Converted to 160000 float samples
(тут або помилка від Whisper, або "No text recognized")
```

**Можливо**:
- Говориш занадто тихо
- Модель не ініціалізувалась
- Проблема з Whisper.NET API

---

## Підсумок

Зараз у тебе є **НАДПОТУЖНЕ** логування на кожному кроці:

1. ✅ Створення сторінки
2. ✅ Передача сервісу
3. ✅ Клік на кнопку
4. ✅ Перевірка доступності
5. ✅ Ініціалізація розпізнавача
6. ✅ Запис аудіо
7. ✅ Обробка Whisper
8. ✅ Розпізнаний текст
9. ✅ Запуск пошуку

**Якщо ти бачиш ЦІ ЛОГИ — можна точно знати, де саме рветься ланцюг.**

**Якщо НЕ бачиш — значить, Debug Output налаштований неправильно або дивишся не той вивід.**

---

## Швидкий чеклист

- [ ] Запусти браузер
- [ ] Шукай `🎤🎤🎤🎤🎤` у Debug Output
- [ ] Якщо його немає — кнопка не спрацьовує
- [ ] Якщо є — дивись далі по логах, що відбувається
- [ ] Скопіюй ВСІ логи з `[VOICE]` і покажи мені

---

**Готово!** Тепер ти точно побачиш, на якому кроці все зупиняється! 🎤✨

