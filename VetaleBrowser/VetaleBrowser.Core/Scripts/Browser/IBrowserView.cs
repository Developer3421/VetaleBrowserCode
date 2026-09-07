using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;

namespace VetaleBrowser.VetaleBrowser.Core.Scripts.Browser;

/// <summary>
/// Engine-agnostic browser view abstraction.
/// Current implementation: <see cref="CefSharpAdapter"/> (CefSharp.Avalonia).
/// </summary>
public interface IBrowserView : IDisposable
{
    /// <summary>Underlying Avalonia control to place into visual containers.</summary>
    Control View { get; }

    /// <summary>Raw engine control (for engine-specific reflection fallbacks).</summary>
    object InnerView { get; }

    string? Address { get; set; }
    string? Title { get; }
    bool CanGoBack { get; }
    bool CanGoForward { get; }

    void GoBack();
    void GoForward();
    void Reload();

    /// <summary>
    /// Navigate without recording emulated engine history.
    /// Used for TabWorker history steps (Back/Forward) so the two
    /// histories don't diverge with duplicates.
    /// </summary>
    void LoadUrl(string url);

    Task<T> EvaluateScriptAsync<T>(string script);
    bool SetAudioMuted(bool muted);

    event EventHandler<AvaloniaPropertyChangedEventArgs> PropertyChanged;
    event EventHandler<KeyEventArgs> KeyDown;
}
