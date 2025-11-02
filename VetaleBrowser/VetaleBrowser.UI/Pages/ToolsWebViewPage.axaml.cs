using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using WebViewControl;
using System.Diagnostics;

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

            // Inject guards shortly after load
            _webView.PropertyChanged += WebViewOnPropertyChanged;

            System.Diagnostics.Trace.WriteLine($"[ToolsWebViewPage] Loading URL: {_currentUrl}");
            VetaleBrowser.Core.Scripts.Services.ConsoleLogger.LogInfo($"Loading URL: {_currentUrl}", "ToolsWebViewPage");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"[ToolsWebViewPage] Error initializing WebView: {ex.Message}");
            Core.Scripts.Services.ConsoleLogger.LogError($"Error initializing WebView", "ToolsWebViewPage", ex);
            ShowError($"Помилка завантаження: {ex.Message}");
        }
    }

    private void WebViewOnPropertyChanged(object? sender, Avalonia.AvaloniaPropertyChangedEventArgs e)
    {
        var name = e.Property.Name;
        if (string.IsNullOrEmpty(name) || _webView == null) return;
        if (name == "Address")
        {
            InjectNavigationGuards(_webView);
        }
    }

    private static async void InjectNavigationGuards(WebView webView)
    {
        try
        {
            var js = @"
                (function(){
                    if(window.__vetale_no_external__) return; 
                    window.__vetale_no_external__ = true;
                    try {
                        var originalOpen = window.open;
                        window.open = function(url){ try{ if(url) location.href = url; }catch(e){} return null; };
                    } catch(e) {}
                    function isExternalScheme(u){
                        try{
                            var a = document.createElement('a'); a.href = u;
                            var p = a.protocol ? a.protocol.toLowerCase() : '';
                            if(!p) return false; if(p==='http:'||p==='https:') return false; return true;
                        }catch(e){ return false; }
                    }
                    document.addEventListener('click', function(e){
                        try{
                            var el = e.target; while(el && el.tagName !== 'A'){ el = el.parentElement; }
                            if(!el) return; var href = el.getAttribute('href'); if(!href) return;
                            var target = el.getAttribute('target');
                            if((target && target.toLowerCase()==='_blank') || isExternalScheme(href)){
                                e.preventDefault(); e.stopPropagation();
                                if(!isExternalScheme(href)) { try{ location.href = href; }catch(_){} }
                            }
                        }catch(_){ }
                    }, true);
                    document.addEventListener('auxclick', function(e){
                        try{
                            if(e.button===1){
                                var el = e.target; while(el && el.tagName !== 'A'){ el = el.parentElement; }
                                if(!el) return; var href = el.getAttribute('href'); if(!href) return;
                                e.preventDefault(); e.stopPropagation();
                                if(!isExternalScheme(href)) { try{ location.href = href; }catch(_){} }
                            }
                        }catch(_){ }
                    }, true);
                    document.addEventListener('submit', function(e){
                        try{
                            var f = e.target; if(!f) return; var t = f.getAttribute('target');
                            if(t && t.toLowerCase()==='_blank'){ e.preventDefault(); try{ f.removeAttribute('target'); f.submit(); }catch(_){} }
                        }catch(_){ }
                    }, true);
                })();
            ";
            await webView.EvaluateScript<object>(js);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[ToolsWebViewPage] Failed to inject guards: {ex.Message}");
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
                _webView.PropertyChanged -= WebViewOnPropertyChanged;
                _webView.Dispose();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"[ToolsWebViewPage] Error disposing WebView: {ex.Message}");
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
                System.Diagnostics.Trace.WriteLine($"[ToolsWebViewPage] Error refreshing: {ex.Message}");
            }
        }
    }
}
