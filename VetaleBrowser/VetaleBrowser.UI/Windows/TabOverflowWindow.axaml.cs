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
/// Tab drag data
/// </summary>
public class TabDragData
{
    public TabWorker Worker { get; set; } = null!;
    public Tab SourceTab { get; set; } = null!;
    public TabOverflowWindow? SourceWindow { get; set; }
    public object? SourceMainWindow { get; set; } // MainWindow source (object to avoid circular dependencies)
    public string Title { get; set; } = "New Tab";
    public IImage? Favicon { get; set; }
    public bool IsMuted { get; set; }
}

/// <summary>
/// Window for overflow tabs - appears when the main panel is full
/// Can be moved by dragging the drag handle or border
/// Supports dragging tabs between windows (right mouse button)
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
    private bool _isManuallyPositioned = false; // Manual positioning flag
    private int _dropTargetIndex = -1; // Index where the tab will be inserted

    /// <summary>Tab close event</summary>
    public event EventHandler<TabWorker>? TabCloseRequested;

    /// <summary>Tab activation event</summary>
    public event EventHandler<TabWorker>? TabActivated;

    /// <summary>Close all overflow tabs event</summary>
    public event EventHandler? CloseAllTabsRequested;

    /// <summary>Event when overflow window becomes empty</summary>
    public event EventHandler? BecameEmpty;

    /// <summary>Tab drag start event</summary>
    public event EventHandler<TabDragData>? TabDragStarted;
    
    /// <summary>Event when a tab is transferred from the MainWindow main panel</summary>
    public event EventHandler<TabWorker>? TabRemovedFromMainPanel;
    
    /// <summary>Event when a tab is transferred from the MainWindow main panel (with source)</summary>
    public event EventHandler<(TabWorker Worker, object SourceMainWindow)>? TabRemovedFromMainPanelWithSource;

    /// <summary>Whether the overflow window is full</summary>
    public bool IsFull { get; private set; }

    /// <summary>Number of tabs</summary>
    public int TabCount => _tabWorkerMap.Count;

    public TabOverflowWindow()
    {
        InitializeComponent();
        
        // Add drop support
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
        
        // Create the insertion position indicator
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
    /// Handler for pressing the drag handle to move the window
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
    /// Handler for pressing the border to move the window
    /// </summary>
    private void OnDragBorderPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            // Check if this is not a click on a control (buttons, tabs)
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
        // Close all tabs in overflow
        CloseAllTabsRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Initializes the window with binding to the parent window
    /// </summary>
    public void Initialize(Window parentWindow, double tabWidth)
    {
        _parentWindow = parentWindow;
        _tabWidth = tabWidth;
        
        // Update the maximum panel width
        UpdateMaxWidth();
        
        // Subscribe to parent window resize events
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
        
        double maxWidth = _parentWindow.Width - 40; // Leave some margin
        MaxWidth = maxWidth;
    }

    /// <summary>
    /// Updates the window position below the parent window
    /// If the user moved the window manually - the position is preserved
    /// </summary>
    public void UpdatePosition()
    {
        if (_parentWindow == null) return;
        
        // If the window was moved manually - do not overwrite the position
        if (_isManuallyPositioned) return;
        
        try
        {
            // Position below the main window's tabs
            var parentPos = _parentWindow.Position;
            
            // Position below the tab bar (approximately 50px from top)
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
    /// Resets manual positioning and returns the window to the default position
    /// </summary>
    public void ResetPosition()
    {
        _isManuallyPositioned = false;
        UpdatePosition();
    }

    /// <summary>
    /// Checks whether another tab can be added
    /// </summary>
    public bool CanAddTab()
    {
        if (_tabsContainer == null) return false;
        
        // Calculate current width
        double currentWidth = CalculateCurrentWidth();
        double closeButtonWidth = 50; // Close button width + margin
        double padding = 24; // Total padding
        
        // Check if there is room for a new tab
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
                width += tab.Width + 4; // 4 - spacing between tabs
            }
        }
        return width;
    }

    /// <summary>
    /// DragOver handler - shows that a tab can be dropped and the position indicator
    /// </summary>
    private void OnDragOver(object? sender, DragEventArgs e)
    {
        if (e.DataTransfer.Contains(TabDragHelper.Format))
        {
            e.DragEffects = DragDropEffects.Move;

            // Determine the insertion position
            if (_tabsContainer != null)
            {
                var position = e.GetPosition(_tabsContainer);
                _dropTargetIndex = CalculateDropIndex(position.X);

                // Show the indicator
                ShowDropIndicator(_dropTargetIndex);
            }
        }
        else
        {
            e.DragEffects = DragDropEffects.None;
            HideDropIndicator();
        }
    }
    
    /// <summary>
    /// Calculates the insertion index based on position X
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
        
        return index; // Insert at end
    }
    
    /// <summary>
    /// Shows the insertion position indicator
    /// </summary>
    private void ShowDropIndicator(int index)
    {
        if (_tabsContainer == null || _dropIndicator == null) return;
        
        // Remove the indicator if it already exists
        if (_tabsContainer.Children.Contains(_dropIndicator))
        {
            _tabsContainer.Children.Remove(_dropIndicator);
        }
        
        // Calculate the insertion position for the indicator
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
        
        // Insert the indicator
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
    /// Hides the insertion position indicator
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
    /// Drop handler - accepts a tab from another window
    /// </summary>
    private void OnDrop(object? sender, DragEventArgs e)
    {
        // Hide the indicator
        int insertIndex = _dropTargetIndex;
        HideDropIndicator();
        
        if (TabDragHelper.TryGetData(e.DataTransfer) is TabDragData dragData)
        {
            // Check if this is not the same window
            if (dragData.SourceWindow == this)
            {
                // Move within the same window
                if (insertIndex >= 0)
                {
                    ReorderTab(dragData.Worker, insertIndex);
                }
                
                // Reset the visual state of the tab
                dragData.SourceTab.ResetDragState();
                
                System.Diagnostics.Debug.WriteLine("[TabOverflowWindow] Tab reordered within same window");
                return;
            }

            // Remove from source window
            if (dragData.SourceWindow != null)
            {
                // Tab from another overflow window
                dragData.SourceWindow.RemoveWorker(dragData.Worker);
            }
            else if (dragData.SourceMainWindow != null)
            {
                // Tab from main panel of MainWindow - notify via event
                // Pass both worker and SourceMainWindow
                TabRemovedFromMainPanelWithSource?.Invoke(this, (dragData.Worker, dragData.SourceMainWindow));
            }
            
            // Reset the visual state of the tab
            dragData.SourceTab.ResetDragState();

            // Add to this window at the required position
            if (AddTabAtIndex(dragData.Worker, insertIndex >= 0 ? insertIndex : _tabWorkerMap.Count))
            {
                // Update title and favicon
                UpdateTabTitle(dragData.Worker, dragData.Title);
                UpdateTabFavicon(dragData.Worker, dragData.Favicon);
                
                System.Diagnostics.Debug.WriteLine($"[TabOverflowWindow] Tab dropped successfully at index {insertIndex}: {dragData.Title}");
            }
        }
    }
    
    /// <summary>
    /// Moves a tab to a new position within the window
    /// </summary>
    private void ReorderTab(TabWorker worker, int newIndex)
    {
        if (_tabsContainer == null) return;
        
        Tab? tab = GetTabByWorker(worker);
        if (tab == null) return;
        
        // Remove from current position
        _tabsContainer.Children.Remove(tab);
        
        // Calculate the new position
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
        
        // Insert at the new position
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
    /// Adds a tab at a specific position
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

        // Subscribe to tab events
        tab.Clicked += (_, __) => TabActivated?.Invoke(this, worker);
        tab.CloseRequested += (_, __) =>
        {
            _tabWorkerMap.Remove(tab);
            _tabsContainer.Children.Remove(tab);
            TabCloseRequested?.Invoke(this, worker);
            
            // Update window width
            UpdateWindowWidth();
            
            // Check if the window is empty
            if (_tabWorkerMap.Count == 0)
            {
                Hide();
                BecameEmpty?.Invoke(this, EventArgs.Empty);
            }
        };
        
        // Drag support (right mouse button)
        tab.DragStarted += OnTabDragStarted;
        void OnTabDragStarted(object? sender, TabDragStartedEventArgs dragArgs)
        {
            StartTabDrag(tab, worker, dragArgs.PointerEvent);
        }

        // Calculate the insertion position
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
        
        // Insert the tab
        if (actualIndex >= _tabsContainer.Children.Count)
        {
            _tabsContainer.Children.Add(tab);
        }
        else
        {
            _tabsContainer.Children.Insert(actualIndex, tab);
        }
        
        _tabWorkerMap[tab] = worker;
        
        // Update window width
        UpdateWindowWidth();
        
        // Show the window if not yet shown
        if (!IsVisible)
        {
            Show();
            UpdatePosition();
        }

        System.Diagnostics.Debug.WriteLine($"[TabOverflowWindow] Tab added at index {index}. Count={_tabWorkerMap.Count}");
        return true;
    }

    /// <summary>
    /// Starts dragging a tab
    /// </summary>
    public async void StartTabDrag(Tab tab, TabWorker worker, PointerPressedEventArgs pointerEvent)
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

        using var dataTransfer = TabDragHelper.CreateTransfer(dragData);

        // Notify about the start of dragging
        TabDragStarted?.Invoke(this, dragData);

        System.Diagnostics.Debug.WriteLine($"[TabOverflowWindow] Starting drag for tab: {tab.Title}");

        try
        {
            var result = await DragDrop.DoDragDropAsync(pointerEvent, dataTransfer, DragDropEffects.Move);
            System.Diagnostics.Debug.WriteLine($"[TabOverflowWindow] Drag result: {result}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[TabOverflowWindow] Drag error: {ex.Message}");
        }
        finally
        {
            // Reset the visual state of the tab after drag completes
            tab.ResetDragState();
        }
    }

    /// <summary>
    /// Adds a tab to the overflow window
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

        // Subscribe to tab events
        tab.Clicked += (_, __) => TabActivated?.Invoke(this, worker);
        tab.CloseRequested += (_, __) =>
        {
            _tabWorkerMap.Remove(tab);
            _tabsContainer.Children.Remove(tab);
            TabCloseRequested?.Invoke(this, worker);
            
            // Update window width
            UpdateWindowWidth();
            
            // Check if the window is empty
            if (_tabWorkerMap.Count == 0)
            {
                Hide();
                BecameEmpty?.Invoke(this, EventArgs.Empty);
            }
        };
        
        // Drag support (right mouse button)
        tab.DragStarted += OnTabDragStarted;
        void OnTabDragStarted(object? sender, TabDragStartedEventArgs dragArgs)
        {
            StartTabDrag(tab, worker, dragArgs.PointerEvent);
        }

        _tabsContainer.Children.Add(tab);
        _tabWorkerMap[tab] = worker;
        
        // Update window width
        UpdateWindowWidth();
        
        // Show the window if not yet shown
        if (!IsVisible)
        {
            Show();
            UpdatePosition();
        }

        System.Diagnostics.Debug.WriteLine($"[TabOverflowWindow] Tab added successfully. Count={_tabWorkerMap.Count}");
        return true;
    }

    /// <summary>
    /// Updates the window width based on the number of tabs
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
    /// Updates the tab state
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
    /// Updates the favicon for a tab in the overflow window
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
    /// Updates the title for a tab in the overflow window
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
    /// Sets the active tab
    /// </summary>
    public void SetActiveTab(TabWorker? worker)
    {
        foreach (var kvp in _tabWorkerMap)
        {
            kvp.Key.IsActive = (kvp.Value == worker);
        }
    }

    /// <summary>
    /// Gets the worker for a tab
    /// </summary>
    public TabWorker? GetWorker(Tab tab)
    {
        return _tabWorkerMap.TryGetValue(tab, out var worker) ? worker : null;
    }

    /// <summary>
    /// Gets the tab for a worker
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
    /// Gets all workers in the overflow window
    /// </summary>
    public IEnumerable<TabWorker> GetAllWorkers()
    {
        return _tabWorkerMap.Values;
    }
    
    /// <summary>
    /// Removes a worker from the overflow window
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
