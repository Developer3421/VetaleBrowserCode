using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using WebViewControl;

namespace VetaleBrowser.VetaleBrowser.UI.Pages;

public partial class ToolsWebViewPage : UserControl
{
    private Grid? _webViewContainer;
    private TextBlock? _toolNameText;
    private WebView? _webView;
    private string? _currentUrl;
    
    public event EventHandler? BackRequested;

    public ToolsWebViewPage()
    {
        InitializeComponent();
        _webViewContainer = this.FindControl<Grid>("WebViewContainer");
        _toolNameText = this.FindControl<TextBlock>("ToolNameText");
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public void LoadTool(string toolName, string url)
    {
        if (_toolNameText != null)
        {
            _toolNameText.Text = toolName;
        }

        _currentUrl = url;
        InitializeWebView();
    }

    private void InitializeWebView()
    {
        if (_webViewContainer == null || string.IsNullOrEmpty(_currentUrl)) return;

        try
        {
            // Clear existing content
            _webViewContainer.Children.Clear();

            // Create new WebView
            _webView = new WebView();
            _webViewContainer.Children.Add(_webView);

            // Navigate to URL
            _webView.Address = _currentUrl;

            System.Diagnostics.Debug.WriteLine($"[ToolsWebViewPage] Loading URL: {_currentUrl}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ToolsWebViewPage] Error initializing WebView: {ex.Message}");
            ShowError($"Помилка завантаження: {ex.Message}");
        }
    }

    private void ShowError(string message)
    {
        if (_webViewContainer == null) return;

        _webViewContainer.Children.Clear();
        _webViewContainer.Children.Add(new TextBlock
        {
            Text = message,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            Foreground = Avalonia.Media.Brushes.Red,
            FontSize = 14
        });
    }

    private void OnBackClick(object? sender, RoutedEventArgs e)
    {
        // Dispose WebView before going back
        if (_webView != null)
        {
            try
            {
                _webView.Dispose();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ToolsWebViewPage] Error disposing WebView: {ex.Message}");
            }
            _webView = null;
        }

        BackRequested?.Invoke(this, EventArgs.Empty);
    }

    private void OnRefreshClick(object? sender, RoutedEventArgs e)
    {
        if (_webView != null && !string.IsNullOrEmpty(_currentUrl))
        {
            try
            {
                _webView.Reload();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ToolsWebViewPage] Error refreshing: {ex.Message}");
            }
        }
    }
}

