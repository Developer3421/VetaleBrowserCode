using System;
using System.Collections.Generic;
using Avalonia.Input;

namespace VetaleBrowser.VetaleBrowser.UI.Windows;

/// <summary>
/// Shared drag-transfer plumbing for tab drag-and-drop (Avalonia 12 DataTransfer API).
/// </summary>
public static class TabDragHelper
{
    private static readonly object SyncRoot = new();
    private static readonly Dictionary<string, TabDragData> PendingTransfers = new();
    public static readonly DataFormat<string> Format = DataFormat.Text;

    public static DataTransfer CreateTransfer(TabDragData data)
    {
        var token = Guid.NewGuid().ToString("N");
        lock (SyncRoot)
            PendingTransfers[token] = data;

        var transfer = new DataTransfer();
        transfer.Add(DataTransferItem.Create(Format, token));
        return transfer;
    }

    public static TabDragData? TryGetData(IDataTransfer transfer)
    {
        var token = DataTransferExtensions.TryGetValue(transfer, Format);
        if (string.IsNullOrEmpty(token))
            return null;

        lock (SyncRoot)
        {
            PendingTransfers.Remove(token, out var data);
            return data;
        }
    }
}
