using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using System;
using System.Collections.Generic;
using VetaleBrowser.VetaleBrowser.UI.Elements;
using VetaleBrowser.VetaleBrowser.Core.Scripts.Models;

namespace VetaleBrowser.VetaleBrowser.UI.Windows;

/// <summary>
/// Вікно для overflow вкладок - з'являється коли основна панель переповнена
/// </summary>
public partial class TabOverflowWindow : Window
{
    private TabOverflowPanel? _overflowPanel;
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
    public int TabCount => _overflowPanel?.TabCount ?? 0;

    public TabOverflowWindow()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
        
        _overflowPanel = this.FindControl<TabOverflowPanel>("OverflowPanel");
        
        if (_overflowPanel != null)
        {
            _overflowPanel.CloseAllRequested += OnCloseAllRequested;
            _overflowPanel.BecameEmpty += OnBecameEmpty;
            _overflowPanel.BecameFull += OnBecameFull;
        }
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
        if (_parentWindow == null || _overflowPanel == null) return;
        
        double maxWidth = _parentWindow.Width - 40; // Залишаємо відступ
        _overflowPanel.MaxPanelWidth = maxWidth;
        
        // Обмежуємо ширину вікна
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
            var parentBounds = _parentWindow.Bounds;
            
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
        return _overflowPanel?.CanAddTab(_tabWidth) ?? false;
    }

    /// <summary>
    /// Додає вкладку до overflow вікна
    /// </summary>
    public bool AddTab(TabWorker worker)
    {
        if (_overflowPanel == null || worker == null) return false;

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
            _overflowPanel.RemoveTab(tab);
            TabCloseRequested?.Invoke(this, worker);
            
            // Оновлюємо ширину вікна
            UpdateWindowWidth();
        };

        if (!_overflowPanel.AddTab(tab))
        {
            return false;
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

        return true;
    }

    /// <summary>
    /// Оновлює ширину вікна відповідно до кількості вкладок
    /// </summary>
    private void UpdateWindowWidth()
    {
        if (_overflowPanel == null) return;
        
        int tabCount = _overflowPanel.TabCount;
        double closeButtonWidth = 40;
        double padding = 20;
        
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

    private void OnCloseAllRequested(object? sender, EventArgs e)
    {
        CloseAllTabsRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnBecameEmpty(object? sender, EventArgs e)
    {
        IsFull = false;
        Hide();
        BecameEmpty?.Invoke(this, EventArgs.Empty);
    }

    private void OnBecameFull(object? sender, EventArgs e)
    {
        IsFull = true;
    }

    protected override void OnClosed(EventArgs e)
    {
        if (_parentWindow != null)
        {
            _parentWindow.PropertyChanged -= OnParentPropertyChanged;
            _parentWindow.PositionChanged -= OnParentPositionChanged;
        }
        
        base.OnClosed(e);
    }
}

