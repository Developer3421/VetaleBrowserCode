using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace VetaleBrowser.VetaleBrowser.UI.Windows;

/// <summary>
/// User agreement window - read-only view, opened from Tools.
/// No blocking prompt is shown on startup.
/// </summary>
public partial class UserAgreementWindow : Window
{
    public UserAgreementWindow()
        : this(readOnly: true)
    {
    }

    public UserAgreementWindow(bool readOnly)
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void TopBar_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginMoveDrag(e);
        }
    }

    private void CloseWindow(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
