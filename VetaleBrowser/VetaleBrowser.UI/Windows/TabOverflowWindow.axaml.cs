using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using System;
using System.Collections.Generic;
using System.Linq;
using VetaleBrowser.VetaleBrowser.UI.Elements;
using VetaleBrowser.VetaleBrowser.Core.Scripts.Models;

namespace VetaleBrowser.VetaleBrowser.UI.Windows;

/// <summary>
/// Дані для перетягування вкладки
/// </summary>
public class TabDragData
{
    public TabWorker Worker { get; set; } = null!;
    public Tab SourceTab { get; set; } = null!;
    public TabOverflowWindow? SourceWindow { get; set; }
    public object? SourceMainWindow { get; set; } // MainWindow-джерело (object щоб уникнути циклічних залежностей)
    public string Title { get; set; } = "New Tab";
    public IImage? Favicon { get; set; }
    public bool IsMuted { get; set; }
}

/// <summary>
/// Вікно для overflow вкладок - з'являється коли основна панель переповнена
/// Можна переміщати, перетягуючи за drag handle або за border
/// Підтримує перетягування вкладок між вікнами (правою кнопкою миші)
/// </summary>
public partial class TabOverflowWindow : Window
{
    private StackPanel? _tabsContainer;
    private Button? _closeButton;
    private Border? _dragBorder;
    private Border? _dropIndicator;
    private Window? _parentWindow;
    private readonly Dictionary<Tab, TabWorker> _tabWorkerMap = new();
    private double _tabWidth = 200;
    private bool _isManuallyPositioned = false; // Прапор ручного позиціонування
    private int _dropTargetIndex = -1; // Індекс куди буде вставлена вкладка

    /// <summary>Подія закриття вкладки</summary>
    public event EventHandler<TabWorker>? TabCloseRequested;

    /// <summary>Подія активації вкладки</summary>
    public event EventHandler<TabWorker>? TabActivated;

    /// <summary>Подія закриття всіх overflow вкладок</summary>
    public event EventHandler? CloseAllTabsRequested;

    /// <summary>Подія коли overflow вікно стає порожнім</summary>
    public event EventHandler? BecameEmpty;

    /// <summary>Подія початку перетягування вкладки</summary>
    public event EventHandler<TabDragData>? TabDragStarted;
    
    /// <summary>Подія коли вкладка переноситься з основної панелі MainWindow</summary>
    public event EventHandler<TabWorker>? TabRemovedFromMainPanel;
    
    /// <summary>Подія коли вкладка переноситься з основної панелі MainWindow (з джерелом)</summary>
    public event EventHandler<(TabWorker Worker, object SourceMainWindow)>? TabRemovedFromMainPanelWithSource;

    /// <summary>Чи переповнено overflow вікно</summary>
    public bool IsFull { get; private set; }

    /// <summary>Кількість вкладок</summary>
    public int TabCount => _tabWorkerMap.Count;

    public TabOverflowWindow()
    {
        InitializeComponent();
        
        // Додаємо підтримку drop
        AddHandler(DragDrop.DropEvent, OnDropHandler);
        AddHandler(DragDrop.DragOverEvent, OnDragOverHandler);
        DragDrop.SetAllowDrop(this, true);
    }
    
    private void OnDropHandler(object? sender, DragEventArgs e) => OnDrop(sender, e);
    private void OnDragOverHandler(object? sender, DragEventArgs e) => OnDragOver(sender, e);

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
        
        _tabsContainer = this.FindControl<StackPanel>("TabsContainer");
        _closeButton = this.FindControl<Button>("CloseButton");
        _dragBorder = this.FindControl<Border>("DragBorder");
        
        if (_closeButton != null)
        {
            _closeButton.Click += OnCloseButtonClick;
        }
        
        // Створюємо індикатор місця вставки
        _dropIndicator = new Border
        {
            Width = 3,
            Height = 30,
            Background = new SolidColorBrush(Color.Parse("#7CB342")),
            CornerRadius = new CornerRadius(2),
            IsVisible = false,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            Margin = new Thickness(-1.5, 0, -1.5, 0)
        };
        
        System.Diagnostics.Debug.WriteLine($"[TabOverflowWindow] InitializeComponent: TabsContainer={_tabsContainer != null}, CloseButton={_closeButton != null}, DragBorder={_dragBorder != null}");
    }

    /// <summary>
    /// Обробник натискання на drag handle для переміщення вікна
    /// </summary>
    private void OnDragHandlePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            _isManuallyPositioned = true;
            BeginMoveDrag(e);
        }
    }

    /// <summary>
    /// Обробник натискання на border для переміщення вікна
    /// </summary>
    private void OnDragBorderPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            // Перевіряємо чи це не клік на контролі (кнопки, вкладки)
            var source = e.Source;
            if (source is Border || source is Grid || source is TextBlock)
            {
                _isManuallyPositioned = true;
                BeginMoveDrag(e);
            }
        }
    }

    private void OnCloseButtonClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        // Закриваємо всі вкладки в overflow
        CloseAllTabsRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Ініціалізує вікно з прив'язкою до батьківського вікна
    /// </summary>
    public void Initialize(Window parentWindow, double tabWidth)
    {
        _parentWindow = parentWindow;
        _tabWidth = tabWidth;
        
        // Оновлюємо максимальну ширину панелі
        UpdateMaxWidth();
        
        // Підписуємося на зміни розміру батьківського вікна
        _parentWindow.PropertyChanged += OnParentPropertyChanged;
        _parentWindow.PositionChanged += OnParentPositionChanged;
        
        System.Diagnostics.Debug.WriteLine($"[TabOverflowWindow] Initialized with tabWidth={tabWidth}");
    }

    private void OnParentPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == BoundsProperty || e.Property == WidthProperty)
        {
            UpdateMaxWidth();
            UpdatePosition();
        }
    }

    private void OnParentPositionChanged(object? sender, PixelPointEventArgs e)
    {
        UpdatePosition();
    }

    private void UpdateMaxWidth()
    {
        if (_parentWindow == null) return;
        
        double maxWidth = _parentWindow.Width - 40; // Залишаємо відступ
        MaxWidth = maxWidth;
    }

    /// <summary>
    /// Оновлює позицію вікна під батьківським вікном
    /// Якщо користувач переміщав вікно вручну - позиція зберігається
    /// </summary>
    public void UpdatePosition()
    {
        if (_parentWindow == null) return;
        
        // Якщо вікно було переміщено вручну - не перезаписуємо позицію
        if (_isManuallyPositioned) return;
        
        try
        {
            // Позиціонуємо під табами головного вікна
            var parentPos = _parentWindow.Position;
            
            // Позиція під tab bar (приблизно 50px від верху)
            int x = parentPos.X + 10;
            int y = parentPos.Y + 50;
            
            Position = new PixelPoint(x, y);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[TabOverflowWindow] UpdatePosition error: {ex.Message}");
        }
    }

    /// <summary>
    /// Скидає ручне позиціонування і повертає вікно до стандартної позиції
    /// </summary>
    public void ResetPosition()
    {
        _isManuallyPositioned = false;
        UpdatePosition();
    }

    /// <summary>
    /// Перевіряє чи можна додати ще одну вкладку
    /// </summary>
    public bool CanAddTab()
    {
        if (_tabsContainer == null) return false;
        
        // Розрахунок поточної ширини
        double currentWidth = CalculateCurrentWidth();
        double closeButtonWidth = 50; // Ширина кнопки закриття + margin
        double padding = 24; // Загальний padding
        
        // Перевіряємо чи є місце для нової вкладки
        return (currentWidth + _tabWidth + closeButtonWidth + padding) <= MaxWidth;
    }
    
    private double CalculateCurrentWidth()
    {
        if (_tabsContainer == null) return 0;
        
        double width = 0;
        foreach (var child in _tabsContainer.Children)
        {
            if (child is Tab tab)
            {
                width += tab.Width + 4; // 4 - spacing між вкладками
            }
        }
        return width;
    }

    /// <summary>
    /// Обробник DragOver - показує що можна скинути вкладку та індикатор позиції
    /// </summary>
    private void OnDragOver(object? sender, DragEventArgs e)
    {
#pragma warning disable CS0618 // Data is obsolete
        if (e.Data.Contains("TabDragData"))
        {
            e.DragEffects = DragDropEffects.Move;
            
            // Визначаємо позицію для вставки
            if (_tabsContainer != null)
            {
                var position = e.GetPosition(_tabsContainer);
                _dropTargetIndex = CalculateDropIndex(position.X);
                
                // Показуємо індикатор
                ShowDropIndicator(_dropTargetIndex);
            }
        }
        else
        {
            e.DragEffects = DragDropEffects.None;
            HideDropIndicator();
        }
#pragma warning restore CS0618
    }
    
    /// <summary>
    /// Обчислює індекс для вставки на основі позиції X
    /// </summary>
    private int CalculateDropIndex(double x)
    {
        if (_tabsContainer == null) return 0;
        
        double currentX = 0;
        int index = 0;
        
        foreach (var child in _tabsContainer.Children)
        {
            if (child is Tab tab)
            {
                double tabCenter = currentX + (tab.Width + 4) / 2;
                
                if (x < tabCenter)
                {
                    return index;
                }
                
                currentX += tab.Width + 4;
                index++;
            }
        }
        
        return index; // Вставка в кінець
    }
    
    /// <summary>
    /// Показує індикатор місця вставки
    /// </summary>
    private void ShowDropIndicator(int index)
    {
        if (_tabsContainer == null || _dropIndicator == null) return;
        
        // Видаляємо індикатор якщо він вже є
        if (_tabsContainer.Children.Contains(_dropIndicator))
        {
            _tabsContainer.Children.Remove(_dropIndicator);
        }
        
        // Обчислюємо позицію для вставки індикатора
        int insertIndex = 0;
        int tabIndex = 0;
        
        for (int i = 0; i < _tabsContainer.Children.Count; i++)
        {
            if (_tabsContainer.Children[i] is Tab)
            {
                if (tabIndex == index)
                {
                    insertIndex = i;
                    break;
                }
                tabIndex++;
                insertIndex = i + 1;
            }
        }
        
        // Вставляємо індикатор
        _dropIndicator.IsVisible = true;
        
        if (insertIndex >= _tabsContainer.Children.Count)
        {
            _tabsContainer.Children.Add(_dropIndicator);
        }
        else
        {
            _tabsContainer.Children.Insert(insertIndex, _dropIndicator);
        }
    }
    
    /// <summary>
    /// Приховує індикатор місця вставки
    /// </summary>
    private void HideDropIndicator()
    {
        if (_dropIndicator != null)
        {
            _dropIndicator.IsVisible = false;
            _tabsContainer?.Children.Remove(_dropIndicator);
        }
        _dropTargetIndex = -1;
    }

    /// <summary>
    /// Обробник Drop - приймає вкладку з іншого вікна
    /// </summary>
    private void OnDrop(object? sender, DragEventArgs e)
    {
        // Приховуємо індикатор
        int insertIndex = _dropTargetIndex;
        HideDropIndicator();
        
#pragma warning disable CS0618 // Data is obsolete
        if (e.Data.Get("TabDragData") is TabDragData dragData)
#pragma warning restore CS0618
        {
            // Перевіряємо чи це не те саме вікно
            if (dragData.SourceWindow == this)
            {
                // Переміщуємо в межах того ж вікна
                if (insertIndex >= 0)
                {
                    ReorderTab(dragData.Worker, insertIndex);
                }
                
                // Скидаємо візуальний стан вкладки
                dragData.SourceTab.ResetDragState();
                
                System.Diagnostics.Debug.WriteLine("[TabOverflowWindow] Tab reordered within same window");
                return;
            }

            // Видаляємо з вікна-джерела
            if (dragData.SourceWindow != null)
            {
                // Вкладка з іншого overflow вікна
                dragData.SourceWindow.RemoveWorker(dragData.Worker);
            }
            else if (dragData.SourceMainWindow != null)
            {
                // Вкладка з основної панелі MainWindow - сповіщаємо через подію
                // Передаємо і worker, і SourceMainWindow
                TabRemovedFromMainPanelWithSource?.Invoke(this, (dragData.Worker, dragData.SourceMainWindow));
            }
            
            // Скидаємо візуальний стан вкладки
            dragData.SourceTab.ResetDragState();

            // Додаємо до цього вікна в потрібну позицію
            if (AddTabAtIndex(dragData.Worker, insertIndex >= 0 ? insertIndex : _tabWorkerMap.Count))
            {
                // Оновлюємо title та favicon
                UpdateTabTitle(dragData.Worker, dragData.Title);
                UpdateTabFavicon(dragData.Worker, dragData.Favicon);
                
                System.Diagnostics.Debug.WriteLine($"[TabOverflowWindow] Tab dropped successfully at index {insertIndex}: {dragData.Title}");
            }
        }
    }
    
    /// <summary>
    /// Переміщує вкладку на нову позицію в межах вікна
    /// </summary>
    private void ReorderTab(TabWorker worker, int newIndex)
    {
        if (_tabsContainer == null) return;
        
        Tab? tab = GetTabByWorker(worker);
        if (tab == null) return;
        
        // Видаляємо з поточної позиції
        _tabsContainer.Children.Remove(tab);
        
        // Обчислюємо нову позицію
        int actualIndex = 0;
        int tabIndex = 0;
        
        for (int i = 0; i < _tabsContainer.Children.Count; i++)
        {
            if (_tabsContainer.Children[i] is Tab)
            {
                if (tabIndex == newIndex)
                {
                    actualIndex = i;
                    break;
                }
                tabIndex++;
                actualIndex = i + 1;
            }
        }
        
        // Вставляємо на нову позицію
        if (actualIndex >= _tabsContainer.Children.Count)
        {
            _tabsContainer.Children.Add(tab);
        }
        else
        {
            _tabsContainer.Children.Insert(actualIndex, tab);
        }
    }
    
    /// <summary>
    /// Додає вкладку в конкретну позицію
    /// </summary>
    public bool AddTabAtIndex(TabWorker worker, int index)
    {
        if (_tabsContainer == null || worker == null)
        {
            System.Diagnostics.Debug.WriteLine($"[TabOverflowWindow] AddTabAtIndex failed: TabsContainer={_tabsContainer != null}, worker={worker != null}");
            return false;
        }

        var tab = new Tab
        {
            Title = worker.Title ?? "New Tab",
            IsActive = false,
            IsCloseButtonVisible = true,
            IsMuted = worker.IsMuted,
            Width = _tabWidth
        };

        // Підписуємося на події вкладки
        tab.Clicked += (_, __) => TabActivated?.Invoke(this, worker);
        tab.CloseRequested += (_, __) =>
        {
            _tabWorkerMap.Remove(tab);
            _tabsContainer.Children.Remove(tab);
            TabCloseRequested?.Invoke(this, worker);
            
            // Оновлюємо ширину вікна
            UpdateWindowWidth();
            
            // Перевіряємо чи порожнє вікно
            if (_tabWorkerMap.Count == 0)
            {
                Hide();
                BecameEmpty?.Invoke(this, EventArgs.Empty);
            }
        };
        
        // Підтримка перетягування (правою кнопкою миші)
        tab.DragStarted += OnTabDragStarted;
        void OnTabDragStarted(object? sender, TabDragStartedEventArgs dragArgs)
        {
            StartTabDrag(tab, worker, dragArgs.PointerEvent);
        }

        // Обчислюємо позицію для вставки
        int actualIndex = 0;
        int tabIndex = 0;
        
        for (int i = 0; i < _tabsContainer.Children.Count; i++)
        {
            if (_tabsContainer.Children[i] is Tab)
            {
                if (tabIndex == index)
                {
                    actualIndex = i;
                    break;
                }
                tabIndex++;
                actualIndex = i + 1;
            }
        }
        
        // Вставляємо вкладку
        if (actualIndex >= _tabsContainer.Children.Count)
        {
            _tabsContainer.Children.Add(tab);
        }
        else
        {
            _tabsContainer.Children.Insert(actualIndex, tab);
        }
        
        _tabWorkerMap[tab] = worker;
        
        // Оновлюємо ширину вікна
        UpdateWindowWidth();
        
        // Показуємо вікно якщо ще не показане
        if (!IsVisible)
        {
            Show();
            UpdatePosition();
        }

        System.Diagnostics.Debug.WriteLine($"[TabOverflowWindow] Tab added at index {index}. Count={_tabWorkerMap.Count}");
        return true;
    }

    /// <summary>
    /// Запускає перетягування вкладки
    /// </summary>
    public async void StartTabDrag(Tab tab, TabWorker worker, PointerEventArgs pointerEvent)
    {
        var dragData = new TabDragData
        {
            Worker = worker,
            SourceTab = tab,
            SourceWindow = this,
            Title = tab.Title,
            Favicon = tab.FaviconSource,
            IsMuted = tab.IsMuted
        };

#pragma warning disable CS0618 // DataObject is obsolete
        var dataObject = new DataObject();
        dataObject.Set("TabDragData", dragData);
#pragma warning restore CS0618

        // Повідомляємо про початок перетягування
        TabDragStarted?.Invoke(this, dragData);

        System.Diagnostics.Debug.WriteLine($"[TabOverflowWindow] Starting drag for tab: {tab.Title}");

        try
        {
#pragma warning disable CS0618 // DoDragDrop is obsolete
            var result = await DragDrop.DoDragDrop(pointerEvent, dataObject, DragDropEffects.Move);
#pragma warning restore CS0618
            System.Diagnostics.Debug.WriteLine($"[TabOverflowWindow] Drag result: {result}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[TabOverflowWindow] Drag error: {ex.Message}");
        }
        finally
        {
            // Скидаємо візуальний стан вкладки після завершення drag
            tab.ResetDragState();
        }
    }

    /// <summary>
    /// Додає вкладку до overflow вікна
    /// </summary>
    public bool AddTab(TabWorker worker)
    {
        if (_tabsContainer == null || worker == null)
        {
            System.Diagnostics.Debug.WriteLine($"[TabOverflowWindow] AddTab failed: TabsContainer={_tabsContainer != null}, worker={worker != null}");
            return false;
        }

        var tab = new Tab
        {
            Title = worker.Title ?? "New Tab",
            IsActive = false,
            IsCloseButtonVisible = true,
            IsMuted = worker.IsMuted,
            Width = _tabWidth
        };

        // Підписуємося на події вкладки
        tab.Clicked += (_, __) => TabActivated?.Invoke(this, worker);
        tab.CloseRequested += (_, __) =>
        {
            _tabWorkerMap.Remove(tab);
            _tabsContainer.Children.Remove(tab);
            TabCloseRequested?.Invoke(this, worker);
            
            // Оновлюємо ширину вікна
            UpdateWindowWidth();
            
            // Перевіряємо чи порожнє вікно
            if (_tabWorkerMap.Count == 0)
            {
                Hide();
                BecameEmpty?.Invoke(this, EventArgs.Empty);
            }
        };
        
        // Підтримка перетягування (правою кнопкою миші)
        tab.DragStarted += OnTabDragStarted;
        void OnTabDragStarted(object? sender, TabDragStartedEventArgs dragArgs)
        {
            StartTabDrag(tab, worker, dragArgs.PointerEvent);
        }

        _tabsContainer.Children.Add(tab);
        _tabWorkerMap[tab] = worker;
        
        // Оновлюємо ширину вікна
        UpdateWindowWidth();
        
        // Показуємо вікно якщо ще не показане
        if (!IsVisible)
        {
            Show();
            UpdatePosition();
        }

        System.Diagnostics.Debug.WriteLine($"[TabOverflowWindow] Tab added successfully. Count={_tabWorkerMap.Count}");
        return true;
    }

    /// <summary>
    /// Оновлює ширину вікна відповідно до кількості вкладок
    /// </summary>
    private void UpdateWindowWidth()
    {
        int tabCount = _tabWorkerMap.Count;
        double closeButtonWidth = 50;
        double padding = 24;
        
        double newWidth = (tabCount * _tabWidth) + (tabCount * 4) + closeButtonWidth + padding;
        newWidth = Math.Min(newWidth, MaxWidth);
        newWidth = Math.Max(newWidth, MinWidth);
        
        Width = newWidth;
    }

    /// <summary>
    /// Оновлює стан вкладки
    /// </summary>
    public void UpdateTab(TabWorker worker)
    {
        foreach (var kvp in _tabWorkerMap)
        {
            if (kvp.Value == worker)
            {
                kvp.Key.Title = worker.Title ?? "New Tab";
                kvp.Key.IsMuted = worker.IsMuted;
                break;
            }
        }
    }

    /// <summary>
    /// Оновлює favicon для вкладки в overflow вікні
    /// </summary>
    public void UpdateTabFavicon(TabWorker worker, Avalonia.Media.IImage? favicon)
    {
        foreach (var kvp in _tabWorkerMap)
        {
            if (kvp.Value == worker)
            {
                kvp.Key.FaviconSource = favicon;
                System.Diagnostics.Debug.WriteLine($"[TabOverflowWindow] Favicon updated for tab: {worker.Title}");
                break;
            }
        }
    }

    /// <summary>
    /// Оновлює title для вкладки в overflow вікні
    /// </summary>
    public void UpdateTabTitle(TabWorker worker, string title)
    {
        foreach (var kvp in _tabWorkerMap)
        {
            if (kvp.Value == worker)
            {
                kvp.Key.Title = title;
                System.Diagnostics.Debug.WriteLine($"[TabOverflowWindow] Title updated for tab: {title}");
                break;
            }
        }
    }

    /// <summary>
    /// Встановлює активну вкладку
    /// </summary>
    public void SetActiveTab(TabWorker? worker)
    {
        foreach (var kvp in _tabWorkerMap)
        {
            kvp.Key.IsActive = (kvp.Value == worker);
        }
    }

    /// <summary>
    /// Отримує worker по вкладці
    /// </summary>
    public TabWorker? GetWorker(Tab tab)
    {
        return _tabWorkerMap.TryGetValue(tab, out var worker) ? worker : null;
    }

    /// <summary>
    /// Отримує вкладку по worker
    /// </summary>
    public Tab? GetTabByWorker(TabWorker worker)
    {
        foreach (var kvp in _tabWorkerMap)
        {
            if (kvp.Value == worker)
            {
                return kvp.Key;
            }
        }
        return null;
    }

    /// <summary>
    /// Отримує всіх workers в overflow вікні
    /// </summary>
    public IEnumerable<TabWorker> GetAllWorkers()
    {
        return _tabWorkerMap.Values;
    }
    
    /// <summary>
    /// Видаляє worker з overflow вікна
    /// </summary>
    public void RemoveWorker(TabWorker worker)
    {
        Tab? tabToRemove = null;
        foreach (var kvp in _tabWorkerMap)
        {
            if (kvp.Value == worker)
            {
                tabToRemove = kvp.Key;
                break;
            }
        }
        
        if (tabToRemove != null)
        {
            _tabWorkerMap.Remove(tabToRemove);
            _tabsContainer?.Children.Remove(tabToRemove);
            UpdateWindowWidth();
            
            if (_tabWorkerMap.Count == 0)
            {
                Hide();
                BecameEmpty?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        if (_parentWindow != null)
        {
            _parentWindow.PropertyChanged -= OnParentPropertyChanged;
            _parentWindow.PositionChanged -= OnParentPositionChanged;
        }
        
        if (_closeButton != null)
        {
            _closeButton.Click -= OnCloseButtonClick;
        }
        
        base.OnClosed(e);
    }
}

