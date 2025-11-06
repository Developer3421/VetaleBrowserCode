# Quick Reference: Modern Chat Design

## Структура повідомлення

```
Border.message-container (full width, background)
├── Padding: 24,16
├── Background: #F7F7F8 (user) / #FFFFFF (assistant)
└── StackPanel.message-content (MaxWidth 800px)
    ├── Spacing: 6
    ├── TextBlock (Label - 13px, SemiBold)
    └── TextBlock (Content - 15px, LineHeight 24)
```

## Класи стилів

### AXAML
- `.message-container` - контейнер повідомлення (повна ширина)
- `.message-content` - контент (MaxWidth 800px)
- `.user-message` - фон користувача
- `.assistant-message` - фон AI

### Кольори
- User bg: `#F7F7F8`
- Assistant bg: `#FFFFFF`
- User label: `#666666`
- Assistant label: `#4CAF50`
- Thinking: `#999999`

## Методи C#

```csharp
AddUserMessage(string text)           // Додати повідомлення користувача
AddAssistantMessage(string text)      // Додати повідомлення AI
AddThinkingMessage()                  // Додати індикатор "думає"
RemoveThinkingMessage()               // Прибрати індикатор
CreateAssistantMessageBubble(text)    // Створити для streaming
```

## Параметри

- **MaxWidth контенту**: 800px
- **Padding контейнера**: 24,16
- **Font size**: 15px
- **LineHeight**: 24
- **Label size**: 13px
- **Spacing**: 6px
- **Bottom margin**: 40px

## Порівняння зі старим

| Параметр | Старий | Новий |
|----------|--------|-------|
| Layout | Bubbles | Full width |
| MaxWidth | 700-900px | 800px (content) |
| Alignment | Center/Right | Left |
| Font size | 14px | 15px |
| LineHeight | - | 24 |
| Padding | 16,12 | 24,16 |
| User bg | #F0F0F0 | #F7F7F8 |
| AI bg | #E8F5E9 | #FFFFFF |

## Як працює

1. Border займає всю ширину з фоном
2. Всередині StackPanel з MaxWidth 800px
3. Текст вирівняний зліва
4. Альтернативний фон для user/AI
5. ScrollViewer без padding (0)
6. Margin 40px знизу для скролу

