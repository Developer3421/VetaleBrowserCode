using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using System;

namespace VetaleBrowser.VetaleBrowser.UI.Pages;

public partial class FullscreenModePage : UserControl
{
    private Grid? _fullscreenContainer;
    public Grid? FullscreenGrid => _fullscreenContainer;

    public FullscreenModePage() {
        try
        {
            System.Diagnostics.Debug.WriteLine("[FullscreenModePage] Constructor called");
            InitializeComponent();
            System.Diagnostics.Debug.WriteLine("[FullscreenModePage] Constructor completed");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[FullscreenModePage] ERROR in constructor: {ex}");
            throw;
        }
        InitializeComponent();
    }

    private void InitializeComponent() {
        try
        {
            System.Diagnostics.Debug.WriteLine("[FullscreenModePage] Loading XAML...");
            AvaloniaXamlLoader.Load(this);
            System.Diagnostics.Debug.WriteLine("[FullscreenModePage] XAML loaded");
            
            _fullscreenContainer = this.FindControl<Grid>("FullscreenContainer");
            System.Diagnostics.Debug.WriteLine($"[FullscreenModePage] FullscreenContainer: {(_fullscreenContainer != null ? "Found" : "NULL")}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[FullscreenModePage] ERROR: {ex}");
            throw;
        }
        _fullscreenContainer = this.FindControl<Grid>("FullscreenContainer");
    }
}
