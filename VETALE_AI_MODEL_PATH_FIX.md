# Виправлення шляху до моделі AI / Model Path Fix

## Проблема
Модель шукалась в `bin` директорії замість директорії проекту, що призводило до помилки "AI model not found".

## Рішення

### 1. Оновлено VetaleAIService.cs
Тепер сервіс шукає модель в декількох місцях (в порядку пріоритету):

```csharp
var possiblePaths = new[]
{
    // В bin output (скопійовано при білді)
    Path.Combine(baseDir, "VetaleBrowser.AI", "Model", "gemma-3-1b-it-UD-Q2_K_XL.gguf"),
    
    // В директорії проекту (відносний шлях)
    Path.Combine(baseDir, "..", "..", "..", "VetaleBrowser.AI", "Model", "gemma-3-1b-it-UD-Q2_K_XL.gguf"),
    
    // Альтернативне розташування
    Path.Combine(baseDir, "Model", "gemma-3-1b-it-UD-Q2_K_XL.gguf")
};
```

### 2. Додано в VetaleBrowser.csproj
Автоматичне копіювання моделі в output папку:

```xml
<!-- Copy AI model to output directory -->
<ItemGroup>
  <None Include="VetaleBrowser.AI\Model\*.gguf" CopyToOutputDirectory="PreserveNewest" />
</ItemGroup>
```

### 3. Покращене логування
Тепер в логах видно:
- ✅ Якщо модель знайдена: `VetaleAIService: Found model at: [шлях]`
- ⚠️ Якщо не знайдена: показує всі перевірені шляхи

## Де має бути модель

### В проекті:
```
E:\VetaleBrowser\VetaleBrowser\VetaleBrowser.AI\Model\gemma-3-1b-it-UD-Q2_K_XL.gguf
```

### Після білду (автоматично):
```
E:\VetaleBrowser\VetaleBrowser\bin\Debug\net9.0\VetaleBrowser.AI\Model\gemma-3-1b-it-UD-Q2_K_XL.gguf
```

## Як працює

1. **Debug режим**: 
   - Модель копіюється з проекту в `bin/Debug/net9.0/VetaleBrowser.AI/Model/`
   - Якщо копіювання не спрацювало, шукається в директорії проекту

2. **Release режим**:
   - Модель копіюється в output
   - При publish включається в пакет

3. **Fallback**:
   - Якщо модель не знайдена в жодному місці, показується список перевірених шляхів
   - Використовується перший шлях як default (може дати FileNotFoundException при ініціалізації)

## Перевірка

### Переконайтесь що модель на місці:
```bash
dir E:\VetaleBrowser\VetaleBrowser\VetaleBrowser.AI\Model\
```

Має показати:
```
gemma-3-1b-it-UD-Q2_K_XL.gguf
```

### Перебілдіть проект:
```bash
cd E:\VetaleBrowser\VetaleBrowser
dotnet clean
dotnet build
```

### Перевірте output:
```bash
dir bin\Debug\net9.0\VetaleBrowser.AI\Model\
```

Модель має бути скопійована.

## Оновлено файли

- ✅ `VetaleBrowser.AI/VetaleAIService.cs` - Пошук моделі
- ✅ `VetaleBrowser.csproj` - Копіювання при білді
- ✅ `VETALE_AI_IMPLEMENTATION.md` - Документація
- ✅ `VETALE_AI_QUICK_START.md` - Швидкий старт

## Статус

✅ **ВИПРАВЛЕНО**

Модель тепер знаходиться правильно як в Debug, так і в Release режимах.

---

**Дата**: 5 листопада 2025

