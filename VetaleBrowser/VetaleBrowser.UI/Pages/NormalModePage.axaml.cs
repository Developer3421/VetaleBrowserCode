using Avalonia.Controls;
using Avalonia.Markup.Xaml;

using System;
using VetaleBrowser.VetaleBrowser.UI.Elements;

namespace VetaleBrowser.VetaleBrowser.UI.Pages;

public partial class NormalModePage : UserControl
{
    private StackPanel? _tabsHost;
    private Button? _addTabButton;
    private Button? _newWindowButton;
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
    public Button? NewWindowBtn => _newWindowButton;
    public Grid? WebViewGrid => _webViewContainer;
    public NavigationBar? NavBar => _navigationBar;
    public Grid? NavBarRow => _navigationBarRow;
    public Button? MinBtn => _minimizeButton;
    public Button? MaxBtn => _maximizeButton;
    public Button? ClsBtn => _closeButton;
    public Grid? TabBar => _tabBarRow;

    /// <summary>
    /// Правильно монтує контент (WebView або внутрішню сторінку) як елемент
    /// контейнера: розтягнення, видимість, фокус. Єдина точка монтування.
    /// </summary>
    public void MountContent(Control content)
    {
        if (_webViewContainer == null || content == null)
            return;

        if (content.Parent is Panel prev && !ReferenceEquals(prev, _webViewContainer))
            prev.Children.Remove(content);

        content.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch;
        content.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch;
        content.IsVisible = true;

        if (!_webViewContainer.Children.Contains(content))
            _webViewContainer.Children.Add(content);

        for (int i = _webViewContainer.Children.Count - 1; i >= 0; i--)
        {
            var c = _webViewContainer.Children[i];
            if (!ReferenceEquals(c, content))
                _webViewContainer.Children.RemoveAt(i);
        }

        try { content.Focus(); } catch { }
    }

    /// <summary>
    /// Returns the active tab content (if it is an internal page)
    /// that is displayed inside the WebViewContainer.
    /// Used by MainWindow to determine whether VetaleSearchResultsPage is currently shown.
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
            _webViewContainer = this.FindControl<Grid>("WebViewHost");
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
            
            _newWindowButton = this.FindControl<Button>("PART_NewWindowButton");
            System.Diagnostics.Debug.WriteLine($"[NormalModePage] NewWindowButton: {(_newWindowButton != null ? "Found" : "NULL")}");
            
            // WebViewHost is the AXAML browser container. Do not overwrite it
            // with the removed legacy WebViewContainer lookup.
            System.Diagnostics.Debug.WriteLine($"[NormalModePage] WebViewHost: {(_webViewContainer != null ? "Found" : "NULL")}");
            
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
