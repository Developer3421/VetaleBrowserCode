using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using System;
using System.Collections.Generic;
using System.Linq;
using VetaleBrowser.VetaleBrowser.UI.Elements;
using VetaleBrowser.VetaleBrowser.Core.Scripts.Models;

namespace VetaleBrowser.VetaleBrowser.UI.Windows;

/// <summary>
/// Вікно для overflow вкладок - з'являється коли основна панель переповнена
/// </summary>
public partial class TabOverflowWindow : Window
{
    private StackPanel? _tabsContainer;
    private Button? _closeButton;
    private Window? _parentWindow;
    private readonly Dictionary<Tab, TabWorker> _tabWorkerMap = new();
    private double _tabWidth = 200;

    /// <summary>Подія закриття вкладки</summary>
    public event EventHandler<TabWorker>? TabCloseRequested;

    /// <summary>Подія активації вкладки</summary>
    public event EventHandler<TabWorker>? TabActivated;

    /// <summary>Подія закриття всіх overflow вкладок</summary>
    public event EventHandler? CloseAllTabsRequested;

    /// <summary>Подія коли overflow вікно стає порожнім</summary>
    public event EventHandler? BecameEmpty;

    /// <summary>Чи переповнено overflow вікно</summary>
    public bool IsFull { get; private set; }

    /// <summary>Кількість вкладок</summary>
    public int TabCount => _tabWorkerMap.Count;

    public TabOverflowWindow()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
        
        _tabsContainer = this.FindControl<StackPanel>("TabsContainer");
        _closeButton = this.FindControl<Button>("CloseButton");
        
        if (_closeButton != null)
        {
            _closeButton.Click += OnCloseButtonClick;
        }
        
        System.Diagnostics.Debug.WriteLine($"[TabOverflowWindow] InitializeComponent: TabsContainer={_tabsContainer != null}, CloseButton={_closeButton != null}");
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
    /// </summary>
    public void UpdatePosition()
    {
        if (_parentWindow == null) return;
        
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

