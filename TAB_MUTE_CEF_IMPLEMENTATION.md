# Реалізація вимикання звуку вкладки через CEFGlue

## Огляд

Реалізовано функціонал вимикання звуку для окремої вкладки браузера з використанням нативного API CEFGlue на рівні `CefBrowserHost`.

## Архітектура

### 1. UI рівень (вже існує)
- **Tab.axaml**: Кнопка mute з іконкою динаміка
- **Tab.axaml.cs**: Подія `MuteToggled` при кліку на кнопку
- **MainWindow.axaml.cs**: Обробник події `tab.MuteToggled`

### 2. Бізнес-логіка (TabWorker)

**Файл**: `VetaleBrowser.Core/Scripts/Models/TabWorker.cs`

#### Властивість IsMuted
```csharp
public bool IsMuted
{
    get => _isMuted;
    set 
    { 
        if (_isMuted != value) 
        { 
            _isMuted = value; 
            OnPropertyChanged(); 
            ApplyMuteState(); // Викликається автоматично
        } 
    }
}
```

#### Метод ApplyMuteState() - Каскадна стратегія

Метод застосовує mute у три рівні з fallback:

**Рівень 1: CEF Native API** (найкращий)
- Використовує `CefBrowserHost.SetAudioMuted(bool)`
- Вимикає весь звук браузерного процесу
- Працює через рефлексію для сумісності з WebViewControl-Avalonia
- Метод `TryGetCefBrowserHost()` шукає внутрішній `CefBrowserHost` через рефлексію

**Рівень 2: Windows Audio Sessions** (fallback)
- Використовує `WindowsAudioSessionService.TrySetProcessMute(pid, mute)`
- Працює на рівні OS, контролюючи аудіо сесії процесів
- Застосовується до всіх PID пов'язаних з вкладкою (`_relatedPids`)

**Рівень 3: JavaScript** (останній fallback)
- Виконує JavaScript для вимикання `<video>` та `<audio>` елементів
- Працює тільки для медіа-елементів на сторінці
- Найменш надійний, але універсальний

### 3. Рефлексія для доступу до CEF API

**Метод**: `TryGetCefBrowserHost()`

Шукає внутрішні поля/властивості WebViewControl:
1. `WebView.Browser` або `WebView.CefBrowser` або `WebView._browser`
2. `Browser.Host` або `Browser.BrowserHost` або `Browser.GetHost()`

Повертає `CefBrowserHost` об'єкт для виклику `SetAudioMuted()`.

## Використання

### З UI
1. Користувач клікає на кнопку mute у вкладці
2. `Tab.MuteToggled` подія передається у `MainWindow`
3. `MainWindow` оновлює `worker.IsMuted = !worker.IsMuted`
4. `TabWorker` автоматично застосовує mute через `ApplyMuteState()`

### Програмно
```csharp
// Вимкнути звук
tabWorker.IsMuted = true;
tabWorker.ToggleMute(); // або через метод

// Глобальний mute для всіх вкладок
tabsManager.SetGlobalMute(true);
```

## Технічні деталі

### Чому рефлексія?
WebViewControl-Avalonia обгортає CefGlue, але не експонує `CefBrowserHost` публічно. Рефлексія дозволяє отримати доступ до внутрішніх об'єктів без модифікації пакету.

### Переваги рівнів
- **CEF API**: Вимикає всі звуки (включно з WebRTC, Web Audio API, системні звуки)
- **Windows Audio Sessions**: Працює на рівні OS, охоплює весь процес
- **JavaScript**: Тільки для медіа елементів, але працює завжди

### Логування
Всі операції логуються через `Debug.WriteLine()`:
```
[TabWorker] Audio muted via CEF native API
[TabWorker] Audio muted at system level for PIDs: 1234,5678
[TabWorker] Audio state applied via JavaScript fallback: True
```

## Тестування

### Ручне тестування
1. Відкрити вкладку з YouTube/медіа контентом
2. Натиснути кнопку mute у вкладці
3. Перевірити:
   - Звук вимкнувся
   - Іконка змінилася (помаранчева з перекресленням)
   - Логи показують використаний метод

### Сценарії
- ✅ Один таб з відео
- ✅ Декілька табів (mute окремої вкладки)
- ✅ Глобальний mute через `SetGlobalMute()`
- ✅ Перезавантаження вкладки (стан зберігається)
- ✅ Fullscreen відео з mute

## Обмеження
- Рефлексія може не працювати, якщо WebViewControl змінить внутрішню структуру
- JavaScript fallback не охоплює Web Audio API та WebRTC
- Windows Audio Sessions працює тільки на Windows

## Подальші покращення
1. Додати персистенцію стану mute між сесіями (зберігати в БД)
2. Додати глобальну кнопку mute на панелі інструментів
3. Додати індикатор звуку у вкладці (іконка динаміка коли грає звук)
4. Підтримка Linux/macOS аудіо систем (PulseAudio/CoreAudio)

## Дата реалізації
18 листопада 2024

## Автор
Реалізовано через AI асистента на базі аналізу коду VetaleBrowser

