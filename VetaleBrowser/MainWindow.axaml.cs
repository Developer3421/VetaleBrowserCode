using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using VetaleBrowser.VetaleBrowser.UI.Scripts;
using VetaleBrowser.VetaleBrowser.Core.Scripts.GlobalManagers;
using WebViewControl;

namespace VetaleBrowser;

public partial class MainWindow : Window
{
    private readonly WindowManager _windowManager;
    private readonly WebViewManager _webViewManager;

    public WebViewManager WebView => _webViewManager;

    public MainWindow()
    {
        InitializeComponent();
        _windowManager = new WindowManager(this);
        _webViewManager = new WebViewManager();

        // Initialize after the window is loaded
        this.Loaded += OnWindowLoaded;
        this.Closed += OnWindowClosed;
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private async void OnWindowLoaded(object? sender, RoutedEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine("MainWindow: OnWindowLoaded called");
        
        try
        {
            // Get the container from XAML
            var container = this.FindControl<Grid>("WebViewContainer");
            
            if (container != null)
            {
                System.Diagnostics.Debug.WriteLine("MainWindow: Found WebViewContainer");
                
                // Create WebView from WebViewControl package
                var webView = new WebView();
                webView.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch;
                webView.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch;
                
                // Clear container and add WebView
                container.Children.Clear();
                container.Children.Add(webView);
                
                System.Diagnostics.Debug.WriteLine("MainWindow: WebView added to container");
                
                // Initialize WebViewManager with the control
                _webViewManager.Initialize(webView);
                
                System.Diagnostics.Debug.WriteLine("MainWindow: WebViewManager initialized, navigating to Google");
                
                // Navigate to a default page
                await _webViewManager.NavigateAsync("https://www.google.com");
                
                System.Diagnostics.Debug.WriteLine("MainWindow: Navigation command sent");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("MainWindow: WebViewContainer NOT found!");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"MainWindow: Error initializing WebView: {ex}");
            
            // Show error in UI
            var container = this.FindControl<Grid>("WebViewContainer");
            if (container != null)
            {
                container.Children.Clear();
                container.Children.Add(new TextBlock
                {
                    Text = $"Помилка ініціалізації WebView:\n{ex.Message}\n\nПереконайтесь що WebView2 Runtime встановлено.",
                    TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                    Foreground = Avalonia.Media.Brushes.Red,
                    FontSize = 14,
                    Margin = new Avalonia.Thickness(20)
                });
            }
        }
    }

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        _webViewManager.Dispose();
    }

    public async Task NavigateToAsync(string url)
    {
        await _webViewManager.NavigateAsync(url);
    }

    private void MinimizeWindow(object? sender, RoutedEventArgs e)
    {
        _windowManager.Minimize();
    }

    private void MaximizeWindow(object? sender, RoutedEventArgs e)
    {
        _windowManager.ToggleMaximize();
    }

    private void CloseWindow(object? sender, RoutedEventArgs e)
    {
        _windowManager.Close();
    }

    // Подвійний клік по верхній панелі -> максимізувати/відновити
    private void TopBar_DoubleTapped(object? sender, RoutedEventArgs e)
    {
        _windowManager.OnTopBarDoubleTapped(sender, e);
    }

    // Перетягування вікна при натисканні на верхню панель
    private void TopBar_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            _windowManager.TryBeginMoveDrag(e);
        }
    }
}

