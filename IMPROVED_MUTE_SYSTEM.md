# Покращена система вимкнення звуку (Mute System)

## Огляд

Реалізовано покращену систему вимкнення звуку для вкладок браузера, яка гарантовано працює:
- Для всіх HTML5 video/audio елементів
- Для WebGL ігор (HexGL та інших) що використовують Web Audio API
- Після оновлення/перезавантаження сторінки
- Для динамічного контенту що з'являється після завантаження

## Архітектура

### Трирівнева стратегія мутування

1. **CEF Native API (Пріоритет 1)**
   - Найнадійніший метод - працює на рівні Chromium
   - Використовує `CefBrowserHost.SetAudioMuted()` через рефлексію
   - Мутує весь звук браузера одразу
   - Працює для всіх типів контенту (HTML5, WebGL, Flash)

2. **Windows Audio Session API (Пріоритет 2)**
   - Працює на рівні операційної системи
   - Мутує всі аудіо-потоки від процесів, пов'язаних з вкладкою
   - Використовує `WindowsAudioSessionService.TrySetProcessMute()`

3. **JavaScript Audio Control (Пріоритет 3)**
   - Контролює всі `<video>` та `<audio>` елементи
   - Перехоплює Web Audio API (AudioContext, webkitAudioContext)
   - Відслідковує та мутує GainNode для ігрових аудіо-систем
   - Автоматично suspend/resume AudioContext

### Audio Interceptor Injection
- Ін'єктується при навігації, якщо вкладка замутована
- Перехоплює створення нових AudioContext ще до їх використання
- Гарантує що ігри будуть замутовані з самого початку

### Ключові компоненти

#### ApplyMuteState()
Асинхронний метод що застосовує стан mute:
```csharp
private async void ApplyMuteState()
{
    // 1. Windows Audio Session API
    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && _relatedPids.Count > 0)
    {
        foreach (var pid in _relatedPids.ToList())
        {
            WindowsAudioSessionService.TrySetProcessMute(pid, _isMuted);
        }
    }

    // 2. JavaScript для media елементів + Web Audio API
    var js = _isMuted ? muteScript : unmuteScript;
    await WebView.EvaluateScript<object>(js);
}
```

#### ScheduleMuteReapply()
Планує відкладене повторне застосування mute після навігації:
```csharp
private void ScheduleMuteReapply()
{
    _muteReapplyCount = 4; // 4 рази з інтервалом 500ms
    _muteReapplyTimer.Start();
}
```

#### InjectAudioInterceptorAsync()
Ін'єктує JavaScript для перехоплення AudioContext:
```csharp
private async Task InjectAudioInterceptorAsync()
{
    // Хуки для AudioContext та webkitAudioContext
    // Автоматичний suspend для нових контекстів
    // Відслідковування GainNode
}
```

## Покращення відносно попередньої реалізації

| Попередня версія | Нова версія |
|-----------------|-------------|
| Використовувала рефлексію для CEF API | Без рефлексії - прямий виклик Windows API |
| Mute не зберігався після навігації | Автоматичне повторне застосування mute |
| Не підтримувала Web Audio API | Повна підтримка AudioContext |
| Одноразове застосування | Множинне застосування з таймером |

## JavaScript Interceptor

```javascript
// Відслідковування AudioContext
window._vetaleAudioContexts = [];
window._vetaleGainNodes = [];

// Перехоплення AudioContext
var origAudioContext = window.AudioContext;
window.AudioContext = function() {
    var ctx = new origAudioContext();
    window._vetaleAudioContexts.push(ctx);
    
    if (window._vetaleAudioMuted) {
        ctx.suspend();
    }
    
    // Хук для GainNode
    var origCreateGain = ctx.createGain.bind(ctx);
    ctx.createGain = function() {
        var gain = origCreateGain();
        window._vetaleGainNodes.push(gain);
        if (window._vetaleAudioMuted) {
            gain.gain.value = 0;
        }
        return gain;
    };
    
    return ctx;
};
```

## Тестування

### Тест-кейси
1. **HTML5 Video**: YouTube, Vimeo - звук має вимкнутися
2. **WebGL Game**: HexGL (hexgl.bkcore.com) - звук гри має вимкнутися
3. **Оновлення сторінки**: Mute має зберігатися після F5
4. **Навігація**: Mute має працювати при переході на іншу сторінку
5. **Динамічний контент**: Нові video/audio мають бути замутовані

### Логи для діагностики
```
[TabWorker] Audio muted at system level for PIDs: 1234,5678
[TabWorker] Audio state applied via JavaScript: True
[TabWorker] Audio interceptor injected
```

## Обмеження

1. Windows Audio Session API працює тільки на Windows
2. Деякі ігри можуть використовувати нестандартні методи відтворення звуку
3. Flash контент не підтримується (deprecated)

## Файли

- `TabWorker.cs` - головна логіка mute
- `WindowsAudioSessionService.cs` - Windows Core Audio API

