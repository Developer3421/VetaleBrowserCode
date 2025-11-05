# Тестування Vetale AI - Checklist

## ✅ Перевірка після виправлень

### 1. Базова ініціалізація
- [ ] Запустити браузер
- [ ] Відкрити Vetale AI
- [ ] Перевірити логи: `VetaleAIAgent: Model initialized successfully`
- [ ] Має показати welcome message

### 2. Проста розмова
- [ ] Надіслати: "Hello"
- [ ] Має показати "Thinking..."
- [ ] "Thinking..." має зникнути
- [ ] Має з'явитись відповідь від AI
- [ ] Перевірити логи: `VetaleAIAgent: Token 1, response length: X`

### 3. Українська мова
- [ ] Вибрати "Українська" в селекторі мови
- [ ] Надіслати: "Привіт, як справи?"
- [ ] Має отримати відповідь українською
- [ ] **НЕ має бути помилки InvalidInputBatch**

### 4. Зміна мови
- [ ] Надіслати повідомлення українською
- [ ] Змінити мову на English
- [ ] Надіслати: "What is AI?"
- [ ] Має працювати без помилок
- [ ] Якщо виникла помилка - має автоматично retry

### 5. Reasoning режим
- [ ] Увімкнути 💭 Reasoning
- [ ] Надіслати: "Explain quantum physics"
- [ ] Має показати процес міркування
- [ ] Вимкнути Reasoning
- [ ] Надіслати інше запитання
- [ ] Має працювати без міркувань

### 6. Новий чат
- [ ] Вести розмову (3-5 повідомлень)
- [ ] Натиснути "New Chat" або "➕"
- [ ] Перевірити логи: `VetaleAIAgent: Context reset successfully`
- [ ] Історія має очиститись (крім welcome)
- [ ] Надіслати нове повідомлення
- [ ] Має працювати як нова розмова

### 7. Довга розмова (стрес-тест)
- [ ] Надіслати 10+ повідомлень підряд
- [ ] Якщо виникне InvalidInputBatch - має автоматично відновитись
- [ ] Перевірити логи: `Resetting context and retrying...`
- [ ] Користувач має отримати відповідь після retry

### 8. Clear History
- [ ] Вести розмову
- [ ] Натиснути "Clear History" або "🗑️"
- [ ] Історія має очиститись
- [ ] Контекст має скинутись

### 9. Швидкі повідомлення
- [ ] Надіслати повідомлення
- [ ] Відразу надіслати ще одне (поки генерується перше)
- [ ] Друге має чекати в черзі
- [ ] Обидва мають відобразитись коректно

### 10. Скасування
- [ ] Надіслати довге питання
- [ ] Натиснути "New Chat" під час генерації
- [ ] Генерація має скасуватись
- [ ] Контекст має скинутись
- [ ] Наступне повідомлення має працювати

## 📊 Очікувані логи

### Успішна ініціалізація:
```
VetaleAIChatPage: InitializeAIModel started
VetaleAIChatPage: VetaleAIService created
VetaleAIService: Found model at: [шлях]
VetaleAIAgent: Model initialized successfully
VetaleAIChatPage: AI model initialized successfully
```

### Успішна відповідь:
```
VetaleAIChatPage: Getting AI response for: [текст]
VetaleAIService: GenerateResponseAsync called
VetaleAIAgent: Starting response generation
VetaleAIAgent: Starting token generation...
VetaleAIAgent: Token 1, response length: 5
VetaleAIAgent: Token 20, response length: 150
VetaleAIAgent: Generation complete. Total tokens: 50
VetaleAIService: Response generated successfully, length: 200
VetaleAIChatPage: Got response: [перші 100 символів]
```

### Автоматичний retry:
```
VetaleAIChatPage: Context error on attempt 1: InvalidInputBatch
VetaleAIChatPage: Resetting context and retrying...
VetaleAIAgent: Resetting context...
VetaleAIAgent: Context reset successfully
VetaleAIChatPage: Calling AI service (attempt 2)
VetaleAIAgent: Starting token generation...
VetaleAIService: Response generated successfully
```

### New Chat:
```
VetaleAIChatPage: New chat requested
VetaleAIChatPage: Resetting AI context...
VetaleAIAgent: Resetting context...
VetaleAIAgent: Context reset successfully
VetaleAIChatPage: AI context reset complete
```

## ❌ Помилки які НЕ повинні виникати

- ❌ `InvalidInputBatch` без автоматичного retry
- ❌ Зависання на "Thinking..." без відповіді
- ❌ Відповідь не відображається
- ❌ Crash при зміні мови
- ❌ Crash при новому чаті
- ❌ Подвоєння відповідей

## 🐛 Якщо щось не працює

### Thinking... не зникає:
1. Перевірте логи - де зупинилась генерація
2. Перевірте чи модель ініціалізована
3. Перевірте чи є токени в логах

### InvalidInputBatch навіть після retry:
1. Натисніть "New Chat"
2. Перевірте логи reset
3. Якщо проблема залишається - перезапустіть браузер

### Помилка ініціалізації:
1. Перевірте що модель є: `VetaleBrowser.AI/Model/gemma-3-1b-it-UD-Q2_K_XL.gguf`
2. Перебілдіть проект: `dotnet build`
3. Перевірте логи шляхів до моделі

### Повільна генерація:
- Це нормально на CPU (30-60 секунд на відповідь)
- Для прискорення потрібна GPU підтримка (в майбутньому)

## 🎯 Критерії успіху

✅ Всі пункти вище працюють без помилок
✅ Логи показують правильний flow
✅ Автоматичний retry спрацьовує при InvalidInputBatch
✅ "New Chat" правильно скидає контекст
✅ Зміна мови працює без проблем

## 📝 Звіт про тестування

Після тестування заповніть:

**Дата тестування**: ___________
**Версія**: 2.0
**ОС**: Windows ___

**Результати**:
- [ ] Всі тести пройдені
- [ ] Є проблеми (вказати які): ___________

**Логи** (прикріпити якщо є проблеми):
```
[вставити логи тут]
```

---

**Створено**: 5 листопада 2025

