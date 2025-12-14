using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using System;
using System.Collections.Generic;

namespace VetaleBrowser.VetaleBrowser.UI.Elements;

/// <summary>
/// Панель для overflow вкладок - з'являється коли основна панель переповнена
/// </summary>
public class TabOverflowPanel : TemplatedControl
{
    public static readonly StyledProperty<double> MaxPanelWidthProperty =
        AvaloniaProperty.Register<TabOverflowPanel, double>(nameof(MaxPanelWidth), 800);

    private StackPanel? _tabsContainer;
    private Button? _closeAllButton;
    private readonly List<Tab> _overflowTabs = new();

    /// <summary>Максимальна ширина панелі (зазвичай = ширина головного вікна)</summary>
    public double MaxPanelWidth
    {
        get => GetValue(MaxPanelWidthProperty);
        set => SetValue(MaxPanelWidthProperty, value);
    }

    /// <summary>Кількість вкладок в overflow панелі</summary>
    public int TabCount => _overflowTabs.Count;

    /// <summary>Подія закриття всіх overflow вкладок</summary>
    public event EventHandler? CloseAllRequested;

    /// <summary>Подія коли overflow панель стає порожньою</summary>
    public event EventHandler? BecameEmpty;

    /// <summary>Подія коли overflow панель заповнена (досягла максимальної ширини)</summary>
    public event EventHandler? BecameFull;

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        // Cleanup old handlers
        if (_closeAllButton != null)
        {
            _closeAllButton.Click -= OnCloseAllClick;
        }

        _tabsContainer = e.NameScope.Find<StackPanel>("PART_TabsContainer");
        _closeAllButton = e.NameScope.Find<Button>("PART_CloseAllButton");

        if (_closeAllButton != null)
        {
            _closeAllButton.Click += OnCloseAllClick;
        }
    }

    private void OnCloseAllClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        CloseAllRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Перевіряє чи можна додати ще одну вкладку
    /// </summary>
    public bool CanAddTab(double tabWidth)
    {
        if (_tabsContainer == null) return false;
        
        // Розрахунок поточної ширини
        double currentWidth = CalculateCurrentWidth();
        double closeButtonWidth = 32; // Ширина кнопки закриття + margin
        double padding = 16; // Загальний padding
        
        // Перевіряємо чи є місце для нової вкладки
        return (currentWidth + tabWidth + closeButtonWidth + padding) <= MaxPanelWidth;
    }

    /// <summary>
    /// Додає вкладку до overflow панелі
    /// </summary>
    public bool AddTab(Tab tab)
    {
        if (_tabsContainer == null || tab == null) return false;
        
        double tabWidth = tab.Width > 0 ? tab.Width : 200;
        
        if (!CanAddTab(tabWidth))
        {
            BecameFull?.Invoke(this, EventArgs.Empty);
            return false;
        }

        _overflowTabs.Add(tab);
        _tabsContainer.Children.Add(tab);
        
        // Підписуємося на закриття вкладки
        tab.CloseRequested += OnTabCloseRequested;
        
        return true;
    }

    /// <summary>
    /// Видаляє вкладку з overflow панелі
    /// </summary>
    public void RemoveTab(Tab tab)
    {
        if (_tabsContainer == null || tab == null) return;

        tab.CloseRequested -= OnTabCloseRequested;
        _overflowTabs.Remove(tab);
        _tabsContainer.Children.Remove(tab);
        
        if (_overflowTabs.Count == 0)
        {
            BecameEmpty?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnTabCloseRequested(object? sender, EventArgs e)
    {
        if (sender is Tab tab)
        {
            RemoveTab(tab);
        }
    }

    /// <summary>
    /// Очищає всі вкладки з overflow панелі
    /// </summary>
    public void ClearAllTabs()
    {
        if (_tabsContainer == null) return;
        
        foreach (var tab in _overflowTabs.ToArray())
        {
            tab.CloseRequested -= OnTabCloseRequested;
        }
        
        _overflowTabs.Clear();
        _tabsContainer.Children.Clear();
        
        BecameEmpty?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Отримує всі вкладки в overflow панелі
    /// </summary>
    public IReadOnlyList<Tab> GetTabs() => _overflowTabs.AsReadOnly();

    private double CalculateCurrentWidth()
    {
        if (_tabsContainer == null) return 0;
        
        double width = 0;
        foreach (var child in _tabsContainer.Children)
        {
            if (child is Tab tab)
            {
                width += tab.Width > 0 ? tab.Width : 200;
                width += 4; // Spacing
            }
        }
        return width;
    }
}

