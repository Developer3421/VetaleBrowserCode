using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using CefSharp.Avalonia;

namespace VetaleBrowser.VetaleBrowser.Core.Scripts.Browser;

public class CefSharpAdapter : IBrowserView
{
    private readonly WebView _inner;
    private readonly List<string> _history = new();
    private int _historyIndex = -1;
    private bool _disposed;
    private bool _suppressHistoryUpdate;

    public event EventHandler<AvaloniaPropertyChangedEventArgs>? PropertyChanged;
    public event EventHandler<KeyEventArgs>? KeyDown;

    public CefSharpAdapter(string? initialUrl = null)
    {
        CefBrowserConfig.EnsureInitialized();
        _inner = new WebView
        {
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch,
            CefSettings = CefBrowserConfig.CreateSettings()
        };
        _inner.PropertyChanged += OnInnerPropertyChanged;
        _inner.KeyDown += OnInnerKeyDown;

        if (!string.IsNullOrWhiteSpace(initialUrl))
            LoadUrl(initialUrl);
    }

    public Control View => _inner;
    public object InnerView => _inner;
    public string? Address
    {
        get => _inner.Address;
        set
        {
            if (string.IsNullOrWhiteSpace(value))
                return;

            var normalized = value.Trim();
            if (_suppressHistoryUpdate)
            {
                _inner.Address = normalized;
                return;
            }

            if (_history.Count == 0 || !string.Equals(_history[_historyIndex], normalized, StringComparison.OrdinalIgnoreCase))
            {
                if (_historyIndex >= 0 && _historyIndex < _history.Count - 1)
                    _history.RemoveRange(_historyIndex + 1, _history.Count - _historyIndex - 1);

                if (_history.Count == 0 || !string.Equals(_history[^1], normalized, StringComparison.OrdinalIgnoreCase))
                {
                    _history.Add(normalized);
                    _historyIndex = _history.Count - 1;
                }
                else
                {
                    _historyIndex = _history.Count - 1;
                }
            }

            if (!string.Equals(_inner.Address, normalized, StringComparison.OrdinalIgnoreCase))
                _inner.Address = normalized;
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
        var target = _history[_historyIndex];

        _suppressHistoryUpdate = true;
        try
        {
            if (!string.Equals(_inner.Address, target, StringComparison.OrdinalIgnoreCase))
                _inner.Address = target;
        }
        finally
        {
            _suppressHistoryUpdate = false;
        }
    }

    public void GoForward()
    {
        if (!CanGoForward)
            return;

        _historyIndex++;
        var target = _history[_historyIndex];

        _suppressHistoryUpdate = true;
        try
        {
            if (!string.Equals(_inner.Address, target, StringComparison.OrdinalIgnoreCase))
                _inner.Address = target;
        }
        finally
        {
            _suppressHistoryUpdate = false;
        }
    }

    public void Reload() => _ = _inner.ReloadAsync();
    public void LoadUrl(string url)
    {
        if (!string.IsNullOrWhiteSpace(url))
            Address = url;
    }

    public Task<T> EvaluateScriptAsync<T>(string script) =>
        Task.FromResult(default(T)!);

    public bool SetAudioMuted(bool muted) => false;

    private void OnInnerPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e) =>
        PropertyChanged?.Invoke(this, e);

    private void OnInnerKeyDown(object? sender, KeyEventArgs e) =>
        KeyDown?.Invoke(this, e);

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _inner.PropertyChanged -= OnInnerPropertyChanged;
        _inner.KeyDown -= OnInnerKeyDown;
        _inner.Cleanup();
    }
}

/// <summary>
/// Compatibility alias for existing callers.
/// </summary>
public sealed class CefGlueAdapter : CefSharpAdapter
{
    public CefGlueAdapter(string? initialUrl = null) : base(initialUrl)
    {
    }
}
