# Використання Іконок - Icon Usage Guide

## Глобальний Словник Ресурсів / Global Resource Dictionary

Всі іконки з папки `VetaleBrowser.UI/Sources/Icons` тепер доступні глобально через словник ресурсів `IconResources.axaml`.

### Доступні Іконки / Available Icons:

1. **MinButtonImage** - Іконка мінімізації вікна
2. **MaxButtonImage** - Іконка максимізації вікна  
3. **CloseButtonImage** - Іконка закриття вікна
4. **BackButtonImage** - Іконка кнопки "Назад"
5. **ForwardButtonImage** - Іконка кнопки "Вперед"

### Як Використовувати / How to Use:

#### У XAML файлах:

```xml
<!-- Використання в Image контролі -->
<Image Source="{DynamicResource MinButtonImage}" Width="16" Height="16"/>

<!-- Використання в Button -->
<Button>
    <Image Source="{DynamicResource BackButtonImage}" Width="20" Height="20"/>
</Button>

<!-- З прив'язкою до іншого розміру -->
<Image Source="{DynamicResource CloseButtonImage}" Width="24" Height="24"/>
```

#### Додавання нових іконок:

1. Додайте PNG файл до `VetaleBrowser.UI/Sources/Icons/`
2. Відкрийте `IconResources.axaml`
3. Додайте новий ресурс:
```xml
<Bitmap x:Key="НоваІконкаImage">avares://VetaleBrowser/VetaleBrowser.UI/Sources/Icons/НоваІконка.png</Bitmap>
```

### Важливо! Правильні шляхи:

Проект називається `VetaleBrowser`, тому всі шляхи до ресурсів повинні використовувати:
```
avares://VetaleBrowser/VetaleBrowser.UI/Sources/Icons/[ім'я_файлу].png
```

### Файли які було змінено:

- ✅ `App.axaml` - Додано глобальний словник ресурсів
- ✅ `MainWindow.axaml` - Оновлено кнопки керування вікном на іконки
- ✅ `VetaleBrowser.csproj` - Додано іконки як Avalonia ресурси
- ✅ `IconResources.axaml` - Створено новий словник ресурсів

### Примітки:

- Усі іконки автоматично включаються як AvaloniaResource через `VetaleBrowser.csproj`
- Використовуйте `DynamicResource` для доступу до іконок
- Розмір іконки можна налаштувати через властивості Width та Height

