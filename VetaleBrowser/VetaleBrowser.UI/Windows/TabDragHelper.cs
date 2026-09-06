using Avalonia.Input;

namespace VetaleBrowser.VetaleBrowser.UI.Windows;

/// <summary>
/// Shared drag-transfer plumbing for tab drag-and-drop (Avalonia 12 DataTransfer API).
/// </summary>
public static class TabDragHelper
{
    public static readonly DataFormat<TabDragData> Format =
        DataFormat.CreateInProcessFormat<TabDragData>("TabDragData");

    public static DataTransfer CreateTransfer(TabDragData data)
    {
        var transfer = new DataTransfer();
        transfer.Add(DataTransferItem.Create(Format, data));
        return transfer;
    }
}
