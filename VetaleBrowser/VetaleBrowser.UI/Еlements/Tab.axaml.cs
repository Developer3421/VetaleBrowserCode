using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace VetaleBrowser.VetaleBrowser.UI.Еlements;

public class Tab : TemplatedControl
{
    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<Tab, string>(nameof(Title), "New Tab");

    public static readonly StyledProperty<string> IconPathProperty =
        AvaloniaProperty.Register<Tab, string>(nameof(IconPath), "");

    public static readonly StyledProperty<bool> IsActiveProperty =
        AvaloniaProperty.Register<Tab, bool>(nameof(IsActive));

    public static readonly StyledProperty<bool> IsCloseButtonVisibleProperty =
        AvaloniaProperty.Register<Tab, bool>(nameof(IsCloseButtonVisible), true);

    static Tab()
    {
        IsActiveProperty.Changed.AddClassHandler<Tab>((tab, _) =>
        {
            tab.PseudoClasses.Set(":active", tab.IsActive);
        });
    }

    public string Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string IconPath
    {
        get => GetValue(IconPathProperty);
        set => SetValue(IconPathProperty, value);
    }

    public bool IsActive
    {
        get => GetValue(IsActiveProperty);
        set => SetValue(IsActiveProperty, value);
    }

    public bool IsCloseButtonVisible
    {
        get => GetValue(IsCloseButtonVisibleProperty);
        set => SetValue(IsCloseButtonVisibleProperty, value);
    }
}