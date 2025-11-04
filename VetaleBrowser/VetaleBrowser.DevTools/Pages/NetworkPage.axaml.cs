using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace VetaleBrowser.VetaleBrowser.DevTools.Pages
{
    public partial class NetworkPage : UserControl
    {
        public NetworkPage()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}

