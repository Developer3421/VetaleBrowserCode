using System;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace VetaleBrowser.VetaleBrowser.UI.Windows;

/// <summary>
/// Separate fullscreen window for HTML5 video (YouTube "F").
/// Hosts only the active tab WebView (no browser chrome).
/// Styled like other app windows (borderless, centered, dark).
/// </summary>
public partial class VideoFullscreenWindow : Window
{
    public Grid? VideoHostGrid => VideoHost;

    /// <summary>Whether the main window should be maximized while video is fullscreen.</summary>
    public bool MaximizeMainWindow => MaximizeMainCheckBox?.IsChecked == true;

    public event EventHandler<bool>? MaximizeMainChanged;

    public VideoFullscreenWindow()
    {
        InitializeComponent();
        if (MaximizeMainCheckBox != null)
        {
            MaximizeMainCheckBox.IsCheckedChanged += (_, _) =>
            {
                try { MaximizeMainChanged?.Invoke(this, MaximizeMainWindow); } catch { }
            };
        }
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
