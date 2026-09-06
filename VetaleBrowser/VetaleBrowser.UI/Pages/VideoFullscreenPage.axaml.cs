using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace VetaleBrowser.VetaleBrowser.UI.Pages;

public partial class VideoFullscreenPage : UserControl
{
    private Grid? _videoContainer;
    public Grid? VideoGrid => _videoContainer;

    public VideoFullscreenPage()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
        _videoContainer = this.FindControl<Grid>("VideoContainer");
    }
}
