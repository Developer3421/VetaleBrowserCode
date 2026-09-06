using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using CefSharp.Avalonia;

namespace VetaleBrowser.VetaleBrowser.Core.Scripts.Browser;

/// <summary>
/// <see cref="IBrowserView"/> implementation backed by CefSharp.Avalonia 1.0.6.
/// Notes vs WebViewControl:
/// - Back/forward history is emulated in-process (upstream exposes no history API).
/// - JavaScript evaluation is NOT supported upstream (no IPC message) — calls
///   return default and log; JS-dependent extras (mute-via-JS, prompt injection,
///   DOM capture) degrade gracefully.
/// </summary>
public sealed class CefSharpAdapter : IBrowserView
{
    private readonly WebView _inner;
    private readonly List<string> _history = new();
    private int _historyIndex = -1;
    private bool _suppressHistory;
    private bool _disposed;

    public event EventHandler<AvaloniaPropertyChangedEventArgs>? PropertyChanged;
    public event EventHandler<KeyEventArgs>? KeyDown;

    public CefSharpAdapter(string? initialUrl = null)
    {
        _inner = new WebView();
        try
        {
            _inner.CefSettings = CefBrowserConfig.CreateSettings();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[CefSharpAdapter] CefSettings failed: {ex.Message}");
        }
        _inner.PropertyChanged += OnInnerPropertyChanged;
        _inner.KeyDown += OnInnerKeyDown;

        if (!string.IsNullOrWhiteSpace(initialUrl))
            Address = initialUrl;
    }

    public Control View => _inner;

    public object InnerView => _inner;

    public string? Address
    {
        get => !string.IsNullOrEmpty(_inner.Url) ? _inner.Url : _inner.Address;
        set
        {
            if (string.IsNullOrWhiteSpace(value))
                return;
            PushHistory(value);
            _inner.Address = value;
        }
    }

    public string? Title => _inner.Title;

    public bool CanGoBack => _historyIndex > 0;

    public bool CanGoForward => _historyIndex >= 0 && _historyIndex < _history.Count - 1;

    public void GoBack()
    {
        if (!CanGoBack)
            return;
        _historyIndex--;
        NavigateHistoryEntry(_history[_historyIndex]);
    }

    public void GoForward()
    {
        if (!CanGoForward)
            return;
        _historyIndex++;
        NavigateHistoryEntry(_history[_historyIndex]);
    }

    public void Reload()
    {
        try
        {
            _ = _inner.ReloadAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[CefSharpAdapter] Reload failed: {ex.Message}");
        }
    }

    public Task<T> EvaluateScriptAsync<T>(string script)
    {
        // CefSharp.Avalonia 1.0.6 has no JS bridge (upstream gap).
        System.Diagnostics.Debug.WriteLine("[CefSharpAdapter] EvaluateScriptAsync not supported (no JS IPC in 1.0.6), returning default");
        return Task.FromResult<T>(default!);
    }

    private void PushHistory(string url)
    {
        if (_historyIndex >= 0 && _historyIndex < _history.Count &&
            string.Equals(_history[_historyIndex], url, StringComparison.Ordinal))
            return;

        if (_historyIndex < _history.Count - 1)
            _history.RemoveRange(_historyIndex + 1, _history.Count - _historyIndex - 1);

        _history.Add(url);
        _historyIndex = _history.Count - 1;
    }

    private void NavigateHistoryEntry(string url)
    {
        LoadUrl(url);
    }

    public void LoadUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return;
        _suppressHistory = true;
        try
        {
            if (Dispatcher.UIThread.CheckAccess())
                _ = _inner.NavigateAsync(url);
            else
                Dispatcher.UIThread.Post(() => _ = _inner.NavigateAsync(url));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[CefSharpAdapter] History navigate failed: {ex.Message}");
        }
        finally
        {
            _suppressHistory = false;
        }
    }

    private void OnInnerPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        try
        {
            // In-page navigations (link clicks) also extend our emulated history.
            if (!_suppressHistory && e.Property.Name == "Address")
                PushHistory(_inner.Address ?? string.Empty);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[CefSharpAdapter] History push failed: {ex.Message}");
        }

        PropertyChanged?.Invoke(this, e);
    }

    private void OnInnerKeyDown(object? sender, KeyEventArgs e)
    {
        KeyDown?.Invoke(sender, e);
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        try { _inner.PropertyChanged -= OnInnerPropertyChanged; } catch { }
        try { _inner.KeyDown -= OnInnerKeyDown; } catch { }
        try { _inner.Cleanup(); } catch { }
    }
}
