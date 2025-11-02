using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using VetaleBrowser.VetaleBrowser.UI.Еlements;

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

    public NormalModePage()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
        
        _tabsHost = this.FindControl<StackPanel>("TabsHost");
        _addTabButton = this.FindControl<Button>("PART_AddTabButton");
        _webViewContainer = this.FindControl<Grid>("WebViewContainer");
        _navigationBar = this.FindControl<NavigationBar>("NavigationBar");
        _navigationBarRow = this.FindControl<Grid>("NavigationBarRow");
        
        _minimizeButton = this.FindControl<Button>("MinimizeButton");
        _maximizeButton = this.FindControl<Button>("MaximizeButton");
        _closeButton = this.FindControl<Button>("CloseButton");
        _tabBarRow = this.FindControl<Grid>("TabBarRow");
    }
}
