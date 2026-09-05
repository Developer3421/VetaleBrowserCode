using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace VetaleBrowser.VetaleBrowser.UI.Windows;

/// <summary>
/// User agreement window - shown on first startup, read-only from Tools.
/// </summary>
public partial class UserAgreementWindow : Window
{
    /// <summary>True if the user accepted the agreement.</summary>
    public bool IsAccepted { get; private set; }

    public UserAgreementWindow()
        : this(readOnly: false)
    {
    }

    public UserAgreementWindow(bool readOnly)
    {
        InitializeComponent();

        if (readOnly)
        {
            var acceptButton = this.FindControl<Button>("AcceptButton");
            var declineButton = this.FindControl<Button>("DeclineButton");
            if (acceptButton != null) acceptButton.IsVisible = false;
            if (declineButton != null) declineButton.IsVisible = false;
        }
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

    private void OnAcceptClick(object? sender, RoutedEventArgs e)
    {
        IsAccepted = true;
        Close();
    }

    private void OnDeclineClick(object? sender, RoutedEventArgs e)
    {
        IsAccepted = false;
        Close();
    }
}
