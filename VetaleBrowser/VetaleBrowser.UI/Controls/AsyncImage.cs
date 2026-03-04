using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using VetaleBrowser.VetaleBrowser.UI.Helpers;

namespace VetaleBrowser.VetaleBrowser.UI.Controls;

/// <summary>
/// Control for asynchronous image loading with memory optimization
/// </summary>
public class AsyncImage : Control
{
    private Bitmap? _loadedBitmap;
    private CancellationTokenSource? _loadCts;
    private bool _isLoading;

    public static readonly StyledProperty<string?> SourceProperty =
        AvaloniaProperty.Register<AsyncImage, string?>(nameof(Source));

    public static readonly StyledProperty<Stretch> StretchProperty =
        AvaloniaProperty.Register<AsyncImage, Stretch>(nameof(Stretch), Stretch.UniformToFill);

    public static readonly StyledProperty<IBrush?> PlaceholderBrushProperty =
        AvaloniaProperty.Register<AsyncImage, IBrush?>(nameof(PlaceholderBrush), new SolidColorBrush(Color.Parse("#F0F0F0")));

    public string? Source
    {
        get => GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    public Stretch Stretch
    {
        get => GetValue(StretchProperty);
        set => SetValue(StretchProperty, value);
    }

    public IBrush? PlaceholderBrush
    {
        get => GetValue(PlaceholderBrushProperty);
        set => SetValue(PlaceholderBrushProperty, value);
    }

    static AsyncImage()
    {
        AffectsRender<AsyncImage>(SourceProperty, StretchProperty, PlaceholderBrushProperty);
        AffectsMeasure<AsyncImage>(SourceProperty);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == SourceProperty)
        {
            LoadImageAsync();
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        LoadImageAsync();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        CancelLoading();
        
        // Do not release the bitmap here - it may be in the cache and used by others
        _loadedBitmap = null;
    }

    private void CancelLoading()
    {
        _loadCts?.Cancel();
        _loadCts?.Dispose();
        _loadCts = null;
        _isLoading = false;
    }

    private async void LoadImageAsync()
    {
        var source = Source;
        
        if (string.IsNullOrWhiteSpace(source))
        {
            _loadedBitmap = null;
            InvalidateVisual();
            return;
        }

        // Cancel previous load
        CancelLoading();

        _isLoading = true;
        _loadCts = new CancellationTokenSource();
        var ct = _loadCts.Token;

        try
        {
            var bitmap = await OptimizedImageLoader.LoadImageAsync(source, ct);

            if (ct.IsCancellationRequested)
                return;

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (!ct.IsCancellationRequested)
                {
                    _loadedBitmap = bitmap;
                    _isLoading = false;
                    InvalidateVisual();
                }
            });
        }
        catch (OperationCanceledException)
        {
            // Normal cancellation
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AsyncImage] Error loading: {ex.Message}");
            _isLoading = false;
            InvalidateVisual();
        }
    }

    public override void Render(DrawingContext context)
    {
        var bounds = new Rect(Bounds.Size);

        if (_loadedBitmap != null)
        {
            // Draw the image
            var sourceSize = new Size(_loadedBitmap.PixelSize.Width, _loadedBitmap.PixelSize.Height);
            var destRect = CalculateDestRect(bounds, sourceSize, Stretch);
            
            context.DrawImage(_loadedBitmap, destRect);
        }
        else
        {
            // Draw placeholder
            if (PlaceholderBrush != null)
            {
                context.FillRectangle(PlaceholderBrush, bounds);
            }

            // Show loading indicator
            if (_isLoading)
            {
                var centerX = bounds.Width / 2;
                var centerY = bounds.Height / 2;
                
                // Simple indicator - circle
                var indicatorBrush = new SolidColorBrush(Color.Parse("#CCCCCC"));
                context.DrawEllipse(indicatorBrush, null, new Point(centerX, centerY), 8, 8);
            }
        }
    }

    private static Rect CalculateDestRect(Rect bounds, Size sourceSize, Stretch stretch)
    {
        if (sourceSize.Width == 0 || sourceSize.Height == 0)
            return bounds;

        switch (stretch)
        {
            case Stretch.None:
                return new Rect(0, 0, sourceSize.Width, sourceSize.Height);

            case Stretch.Fill:
                return bounds;

            case Stretch.Uniform:
            {
                var ratioX = bounds.Width / sourceSize.Width;
                var ratioY = bounds.Height / sourceSize.Height;
                var ratio = Math.Min(ratioX, ratioY);
                var newWidth = sourceSize.Width * ratio;
                var newHeight = sourceSize.Height * ratio;
                var x = (bounds.Width - newWidth) / 2;
                var y = (bounds.Height - newHeight) / 2;
                return new Rect(x, y, newWidth, newHeight);
            }

            case Stretch.UniformToFill:
            {
                var ratioX = bounds.Width / sourceSize.Width;
                var ratioY = bounds.Height / sourceSize.Height;
                var ratio = Math.Max(ratioX, ratioY);
                var newWidth = sourceSize.Width * ratio;
                var newHeight = sourceSize.Height * ratio;
                var x = (bounds.Width - newWidth) / 2;
                var y = (bounds.Height - newHeight) / 2;
                return new Rect(x, y, newWidth, newHeight);
            }

            default:
                return bounds;
        }
    }
}

