using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace VetaleBrowser.VetaleBrowser.UI.Pages;

public partial class FullscreenModePage : UserControl
{
    private Grid? _fullscreenContainer;
    public Grid? FullscreenGrid => _fullscreenContainer;

    public FullscreenModePage()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
        _fullscreenContainer = this.FindControl<Grid>("FullscreenContainer");
    }
}
