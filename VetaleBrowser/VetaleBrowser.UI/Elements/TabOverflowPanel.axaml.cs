using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using System;
using System.Collections.Generic;

namespace VetaleBrowser.VetaleBrowser.UI.Elements;

/// <summary>
/// Panel for overflow tabs - appears when the main panel is full
/// </summary>
public class TabOverflowPanel : TemplatedControl
{
    public static readonly StyledProperty<double> MaxPanelWidthProperty =
        AvaloniaProperty.Register<TabOverflowPanel, double>(nameof(MaxPanelWidth), 800);

    private StackPanel? _tabsContainer;
    private Button? _closeAllButton;
    private readonly List<Tab> _overflowTabs = new();

    /// <summary>Maximum panel width (usually = main window width)</summary>
    public double MaxPanelWidth
    {
        get => GetValue(MaxPanelWidthProperty);
        set => SetValue(MaxPanelWidthProperty, value);
    }

    /// <summary>Number of tabs in the overflow panel</summary>
    public int TabCount => _overflowTabs.Count;

    /// <summary>Event for closing all overflow tabs</summary>
    public event EventHandler? CloseAllRequested;

    /// <summary>Event when the overflow panel becomes empty</summary>
    public event EventHandler? BecameEmpty;

    /// <summary>Event when the overflow panel is full (reached maximum width)</summary>
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
    /// Checks whether another tab can be added
    /// </summary>
    public bool CanAddTab(double tabWidth)
    {
        if (_tabsContainer == null) return false;
        
        // Calculate current width
        double currentWidth = CalculateCurrentWidth();
        double closeButtonWidth = 32; // Close button width + margin
        double padding = 16; // Total padding
        
        // Check if there is room for a new tab
        return (currentWidth + tabWidth + closeButtonWidth + padding) <= MaxPanelWidth;
    }

    /// <summary>
    /// Adds a tab to the overflow panel
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
        
        // Subscribe to tab close event
        tab.CloseRequested += OnTabCloseRequested;
        
        return true;
    }

    /// <summary>
    /// Removes a tab from the overflow panel
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
    /// Clears all tabs from the overflow panel
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
    /// Gets all tabs in the overflow panel
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

