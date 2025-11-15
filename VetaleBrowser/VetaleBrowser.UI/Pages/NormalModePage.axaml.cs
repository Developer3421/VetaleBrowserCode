using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using VetaleBrowser.VetaleBrowser.UI.Еlements;
using System;

namespace VetaleBrowser.VetaleBrowser.UI.Pages;

public partial class NormalModePage : UserControl
{
    private StackPanel? _tabsHost;
    private Button? _addTabButton;
    private Grid? _webViewContainer;
    private NavigationBar? _navigationBar;
    private Grid? _navigationBarRow;
    
    // Window controls
    private Button? _minimizeButton;
    private Button? _maximizeButton;
    private Button? _closeButton;
    private Grid? _tabBarRow;

    public StackPanel? TabsHostPanel => _tabsHost;
    public Button? AddTabBtn => _addTabButton;
    public Grid? WebViewGrid => _webViewContainer;
    public NavigationBar? NavBar => _navigationBar;
    public Grid? NavBarRow => _navigationBarRow;
    public Button? MinBtn => _minimizeButton;
    public Button? MaxBtn => _maximizeButton;
    public Button? ClsBtn => _closeButton;
    public Grid? TabBar => _tabBarRow;

    /// <summary>
    /// Повертає активний контент вкладки (якщо це внутрішня сторінка),
    /// який відображається всередині контейнера WebViewContainer.
    /// Використовується MainWindow для визначення, чи показується зараз VetaleSearchResultsPage.
    /// </summary>
    public UserControl? GetActiveTabContent()
    {
        if (_webViewContainer == null)
            return null;

        if (_webViewContainer.Children.Count == 1 && _webViewContainer.Children[0] is UserControl uc)
            return uc;

        return null;
    }

    public NormalModePage()
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("[NormalModePage] Constructor called");
            InitializeComponent();
            System.Diagnostics.Debug.WriteLine("[NormalModePage] Constructor completed successfully");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[NormalModePage] CRITICAL ERROR in constructor: {ex}");
            throw;
        }
    }

    private void InitializeComponent()
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("[NormalModePage] InitializeComponent: Loading XAML...");
            AvaloniaXamlLoader.Load(this);
            System.Diagnostics.Debug.WriteLine("[NormalModePage] InitializeComponent: XAML loaded");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[NormalModePage] ERROR loading XAML: {ex}");
            throw;
        }
        
        try
        {
            _tabsHost = this.FindControl<StackPanel>("TabsHost");
            System.Diagnostics.Debug.WriteLine($"[NormalModePage] TabsHost: {(_tabsHost != null ? "Found" : "NULL")}");
            
            _addTabButton = this.FindControl<Button>("PART_AddTabButton");
            System.Diagnostics.Debug.WriteLine($"[NormalModePage] AddTabButton: {(_addTabButton != null ? "Found" : "NULL")}");
            
            _webViewContainer = this.FindControl<Grid>("WebViewContainer");
            System.Diagnostics.Debug.WriteLine($"[NormalModePage] WebViewContainer: {(_webViewContainer != null ? "Found" : "NULL")}");
            
            _navigationBar = this.FindControl<NavigationBar>("NavigationBar");
            System.Diagnostics.Debug.WriteLine($"[NormalModePage] NavigationBar: {(_navigationBar != null ? "Found" : "NULL")}");
            
            _navigationBarRow = this.FindControl<Grid>("NavigationBarRow");
            System.Diagnostics.Debug.WriteLine($"[NormalModePage] NavigationBarRow: {(_navigationBarRow != null ? "Found" : "NULL")}");
            
            _minimizeButton = this.FindControl<Button>("MinimizeButton");
            System.Diagnostics.Debug.WriteLine($"[NormalModePage] MinimizeButton: {(_minimizeButton != null ? "Found" : "NULL")}");
            
            _maximizeButton = this.FindControl<Button>("MaximizeButton");
            System.Diagnostics.Debug.WriteLine($"[NormalModePage] MaximizeButton: {(_maximizeButton != null ? "Found" : "NULL")}");
            
            _closeButton = this.FindControl<Button>("CloseButton");
            System.Diagnostics.Debug.WriteLine($"[NormalModePage] CloseButton: {(_closeButton != null ? "Found" : "NULL")}");
            
            _tabBarRow = this.FindControl<Grid>("TabBarRow");
            System.Diagnostics.Debug.WriteLine($"[NormalModePage] TabBarRow: {(_tabBarRow != null ? "Found" : "NULL")}");
            
            System.Diagnostics.Debug.WriteLine("[NormalModePage] InitializeComponent completed");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[NormalModePage] ERROR finding controls: {ex.Message}");
            throw;
        }
    }
}
