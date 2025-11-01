using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using WebViewControl;

namespace VetaleBrowser.VetaleBrowser.Core.Scripts.Models
{
    /// <summary>
    /// Represents a browser worker that owns its own WebView (CefGlue-based via WebViewControl) and navigation manager.
    /// </summary>
    public sealed class TabWorker : IDisposable, INotifyPropertyChanged
    {
        public Guid Id { get; } = Guid.NewGuid();

        public WebView WebView { get; }
        public GlobalManagers.WebViewManager Manager { get; }

        private string? _title;
        private string? _address;
        private bool _isActive;

        public string? Title
        {
            get => _title;
            private set { if (_title != value) { _title = value; OnPropertyChanged(); TitleChanged?.Invoke(this, value); } }
        }

        public string? Address
        {
            get => _address;
            private set { if (_address != value) { _address = value; OnPropertyChanged(); AddressChanged?.Invoke(this, value); } }
        }

        public bool IsActive
        {
            get => _isActive;
            set { if (_isActive != value) { _isActive = value; OnPropertyChanged(); } }
        }

        public event EventHandler<string?>? TitleChanged;
        public event EventHandler<string?>? AddressChanged;

        public TabWorker()
        {
            WebView = new WebView
            {
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch,
            };

            Manager = new GlobalManagers.WebViewManager();
            Manager.Initialize(WebView);

            // Forward manager-initiated navigations to Address property
            Manager.Navigated += (_, url) => Address = url;

            // Observe WebView property changes to keep state up-to-date
            WebView.PropertyChanged += WebViewOnPropertyChanged;
        }

        private void WebViewOnPropertyChanged(object? sender, Avalonia.AvaloniaPropertyChangedEventArgs e)
        {
            var name = e.Property.Name;
            if (string.IsNullOrEmpty(name)) return;

            if (name == "Address")
            {
                Address = WebView.Address;
            }
            else if (name == "Title")
            {
                try
                {
                    var t = WebView.GetType().GetProperty("Title")?.GetValue(WebView) as string;
                    if (!string.IsNullOrWhiteSpace(t)) Title = t;
                }
                catch
                {
                    // ignore
                }
            }
            else if (name == "CanGoBack" || name == "CanGoForward")
            {
                // No-op here; consumers can read from WebView
            }
        }

        public async void Navigate(string url)
        {
            try
            {
                await Manager.NavigateAsync(url);
            }
            catch
            {
                // ignore navigation errors here
            }
        }

        public void Dispose()
        {
            try
            {
                WebView.PropertyChanged -= WebViewOnPropertyChanged;
                Manager.Dispose();
                WebView.Dispose();
            }
            catch
            {
                // best-effort
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
