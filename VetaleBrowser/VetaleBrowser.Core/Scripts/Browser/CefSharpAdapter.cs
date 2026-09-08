using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using CefSharp;
using VetaleBrowser.VetaleBrowser.UI.Controls;

namespace VetaleBrowser.VetaleBrowser.Core.Scripts.Browser;

/// <summary>
/// Таб-система на новому контролі: CefSharp.Wpf ChromiumWebBrowser в Avalonia через <see cref="CefSharpWpfHost"/>.
/// Пробрасыває реальні події браузера (Address/Title/CanGoBack/CanGoForward/FrameLoadEnd) як
/// Avalonia PropertyChanged, які вже слухають TabWorker і MainWindow. Іконка вкладки:
/// TabWorker.RefreshFaviconAsync (DevTools :9223) -> FaviconChanged -> MainWindow качає і малює.
/// Навігація: Load/Navigate + справжні browser.Back()/Forward()/Reload().
/// </summary>
public class CefSharpAdapter : AvaloniaObject, IBrowserView
{
    public static readonly StyledProperty<string?> AddressProperty =
        AvaloniaProperty.Register<CefSharpAdapter, string?>(nameof(Address));
    public static readonly StyledProperty<string?> TitleProperty =
        AvaloniaProperty.Register<CefSharpAdapter, string?>(nameof(Title));
    public static readonly StyledProperty<bool> CanGoBackProperty =
        AvaloniaProperty.Register<CefSharpAdapter, bool>(nameof(CanGoBack));
    public static readonly StyledProperty<bool> CanGoForwardProperty =
        AvaloniaProperty.Register<CefSharpAdapter, bool>(nameof(CanGoForward));

    private readonly CefSharpWpfHost _host;
    private readonly List<string> _history = new();
    private int _historyIndex = -1;
    private bool _disposed;
    private bool _suppressHistoryUpdate;

    // PropertyChanged успадковано від AvaloniaObject і задовольняє IBrowserView.
    public event EventHandler<KeyEventArgs>? KeyDown;
    /// <summary>Головний фрейм довантажився — тригер для іконки/титулу вкладки.</summary>
    public event EventHandler<string?>? FrameLoadEnd;
    public event EventHandler<IList<string>>? FaviconUrlsChanged;

    /// <summary>Останні URL іконок від CEF.</summary>
    public IList<string>? LatestFaviconUrls { get; private set; }

    public CefSharpAdapter(string? initialUrl = null)
    {
        CefWpfBootstrapper.EnsureInitialized();
        _host = new CefSharpWpfHost
        {
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch,
            StartUrl = string.IsNullOrWhiteSpace(initialUrl) ? "about:blank" : initialUrl.Trim(),
        };
        _host.BrowserAddressChanged += (_, url) => OnEngineAddress(url);
        _host.BrowserTitleChanged += (_, title) => OnEngineTitle(title);
        _host.BrowserLoadingStateChanged += (_, s) => OnEngineLoadingState(s.CanGoBack, s.CanGoForward);
        _host.BrowserFrameLoadEnd += (_, url) => FrameLoadEnd?.Invoke(this, url);
        _host.BrowserFaviconUrlsChanged += (_, urls) =>
        {
            LatestFaviconUrls = urls;
            FaviconUrlsChanged?.Invoke(this, urls);
        };
        if (!string.IsNullOrWhiteSpace(initialUrl))
            LoadUrl(initialUrl);
    }

    public Control View => _host;
    public object InnerView => _host.Browser ?? (object)_host;
    public CefSharpWpfHost Host => _host;

    public string? Address
    {
        get => GetValue(AddressProperty) ?? _host.Browser?.Address ?? _host.StartUrl;
        set
        {
            if (string.IsNullOrWhiteSpace(value))
                return;
            var normalized = value.Trim();
            if (!_suppressHistoryUpdate)
                PushHistory(normalized);
            SetOnUi(() => SetValue(AddressProperty, normalized));
            _host.Navigate(normalized);
        }
    }

    public string? Title => GetValue(TitleProperty) ?? _host.Browser?.Title;
    public bool CanGoBack => GetValue(CanGoBackProperty) || _historyIndex > 0;
    public bool CanGoForward => GetValue(CanGoForwardProperty) || (_historyIndex >= 0 && _historyIndex < _history.Count - 1);

    public void GoBack()
    {
        var browser = _host.Browser;
        // Справжня навігація рушієм (історія CEF) — синхронізація таб-історії прийде через AddressChanged.
        if (browser != null && browser.CanGoBack)
        {
            try { browser.Back(); return; } catch { }
        }
        if (_historyIndex <= 0) return;
        _historyIndex--;
        _suppressHistoryUpdate = true;
        try { _host.Navigate(_history[_historyIndex]); }
        finally { _suppressHistoryUpdate = false; }
    }

    public void GoForward()
    {
        var browser = _host.Browser;
        if (browser != null && browser.CanGoForward)
        {
            try { browser.Forward(); return; } catch { }
        }
        if (_historyIndex < 0 || _historyIndex >= _history.Count - 1) return;
        _historyIndex++;
        _suppressHistoryUpdate = true;
        try { _host.Navigate(_history[_historyIndex]); }
        finally { _suppressHistoryUpdate = false; }
    }

    public void Reload() => _host.Browser?.Reload();

    public void LoadUrl(string url)
    {
        if (!string.IsNullOrWhiteSpace(url))
            Address = url;
    }

    public async Task<T> EvaluateScriptAsync<T>(string script)
    {
        var browser = _host.Browser;
        if (browser == null) return default!;
        try
        {
            var resp = await browser.EvaluateScriptAsync(script);
            if (resp.Success && resp.Result is T t) return t;
            // М'яка конверсія примітивів (напр. object->string).
            if (resp.Success && resp.Result != null)
            {
                try { return (T)Convert.ChangeType(resp.Result, typeof(T)); } catch { }
            }
            return default!;
        }
        catch { return default!; }
    }

    public bool SetAudioMuted(bool muted)
    {
        try
        {
            var host = _host.Browser?.GetBrowser()?.GetHost();
            if (host == null) return false;
            host.SetAudioMuted(muted);
            return true;
        }
        catch { return false; }
    }

    // ---- рушій -> таб-система ----

    private void OnEngineAddress(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return;
        var trimmed = url.Trim();
        if (!_suppressHistoryUpdate)
            PushHistory(trimmed);
        // Події рушія йдуть з WPF-диспетчера — маршалимо на Avalonia UI-потік,
        // інакше SetValue/RaisePropertyChanged впаде з cross-thread.
        SetOnUi(() => SetValue(AddressProperty, trimmed));
    }

    private void OnEngineTitle(string? title)
    {
        if (string.IsNullOrWhiteSpace(title)) return;
        var trimmed = title.Trim();
        SetOnUi(() => SetValue(TitleProperty, trimmed));
    }

    private void OnEngineLoadingState(bool canGoBack, bool canGoForward)
    {
        SetOnUi(() =>
        {
            SetValue(CanGoBackProperty, canGoBack);
            SetValue(CanGoForwardProperty, canGoForward);
        });
    }

    private void PushHistory(string normalized)
    {
        if (_history.Count == 0 || !string.Equals(_history[_historyIndex], normalized, StringComparison.OrdinalIgnoreCase))
        {
            if (_historyIndex >= 0 && _historyIndex < _history.Count - 1)
                _history.RemoveRange(_historyIndex + 1, _history.Count - _historyIndex - 1);
            if (_history.Count == 0 || !string.Equals(_history[^1], normalized, StringComparison.OrdinalIgnoreCase))
            {
                _history.Add(normalized);
                _historyIndex = _history.Count - 1;
            }
            else _historyIndex = _history.Count - 1;
        }
    }

    private void RaisePropertyChanged(AvaloniaProperty property, object? newValue, string? forceName = null)
    {
        // Залишено для сумісності; штатний шлях — SetValue() вище,
        // який сам рейзить PropertyChanged через AvaloniaObject.
    }

    private static void SetOnUi(Action action)
    {
        try
        {
            var dispatcher = Avalonia.Threading.Dispatcher.UIThread;
            if (dispatcher.CheckAccess())
                action();
            else
                dispatcher.Post(action);
        }
        catch { }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        // Закриття вкладки: остаточно вбити браузер (перемикання вкладок його не чіпає).
        try { _host.DestroyBrowser(); } catch { }
    }
}

/// <summary>Compatibility alias for existing callers.</summary>
public sealed class CefGlueAdapter : CefSharpAdapter
{
    public CefGlueAdapter(string? initialUrl = null) : base(initialUrl) { }
}
