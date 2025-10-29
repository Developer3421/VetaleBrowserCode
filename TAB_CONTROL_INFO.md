# Tab Control - Інструкція з використання

## Створено контрол вкладки для Vetale Browser

### Файли:
1. `VetaleBrowser/VetaleBrowser.UI/Еlements/Tab.axaml` - стилі контролу
2. `VetaleBrowser/VetaleBrowser.UI/Еlements/Tab.axaml.cs` - код контролу

### Налаштування:

#### 1. App.axaml
Додано підключення стилів:
```xml
<StyleInclude Source="avares://VetaleBrowser/VetaleBrowser.UI/Еlements/Tab.axaml"/>
```

#### 2. MainWindow.axaml
Додано namespace:
```xml
xmlns:controls="using:VetaleBrowser.VetaleBrowser.UI.Еlements"
```

Використання контролу:
```xml
<controls:Tab Title="Vetale Browser" IsActive="True" Width="200" />
```

### Властивості контролу:
- `Title` - текст вкладки
- `IsActive` - чи активна вкладка (true/false)
- `IconPath` - шлях до іконки
- `IsCloseButtonVisible` - показувати кнопку закриття (true/false)
- `Width` - ширина вкладки (100-240px)

### Дизайн:
- Світло-фіолетовий градієнт для активної вкладки
- Висота: 38px
- Заокруглені кути зверху
- Кнопка закриття з hover ефектами

### Розташування:
Вкладка розміщена в лівому верхньому куті вікна браузера, в першому рядку (Grid.Row="0").

