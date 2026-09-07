using System;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace VetaleBrowser.VetaleBrowser.Core.Scripts.Browser;

/// <summary>
/// Reads page state via the CEF remote-debugging port (DevTools protocol).
/// Bypasses the missing JS bridge in CefSharp.Avalonia (EvaluateScriptAsync is a stub).
/// Currently used for HTML5 fullscreen detection (document.fullscreenElement).
/// Returns null when DevTools is unreachable or the target is ambiguous.
/// </summary>
public static class CefDevToolsClient
{
    private static readonly HttpClient _http = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };

    public static async Task<bool?> IsDocumentFullscreenAsync(string? urlHint, int port = 9223)
    {
        return await EvaluateBooleanAsync(urlHint, "!!(document.fullscreenElement||document.webkitFullscreenElement)", port);
    }

    /// <summary>
    /// Runs an arbitrary JS boolean expression in the page (actuator side).
    /// </summary>
    public static async Task<bool?> EvaluateBooleanAsync(string? urlHint, string expression, int port = 9223)
    {
        try
        {
            string listJson;
            try
            {
                listJson = await _http.GetStringAsync($"http://127.0.0.1:{port}/json/list");
            }
            catch
            {
                return null; // DevTools port closed
            }

            string? wsUrl = PickTarget(listJson, urlHint);
            if (wsUrl == null)
                return null;

            return await EvaluateAsync(wsUrl, expression);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Runs an arbitrary JS string expression in the page, returns string value or null.
    /// </summary>
    public static async Task<string?> EvaluateStringAsync(string? urlHint, string expression, int port = 9223)
    {
        try
        {
            string listJson;
            try { listJson = await _http.GetStringAsync($"http://127.0.0.1:{port}/json/list"); }
            catch { return null; }
            string? wsUrl = PickTarget(listJson, urlHint);
            if (wsUrl == null) return null;
            return await EvaluateStringRawAsync(wsUrl, expression);
        }
        catch { return null; }
    }

    private static async Task<string?> EvaluateStringRawAsync(string wsUrl, string expression)
    {
        using var ws = new ClientWebSocket();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        try
        {
            await ws.ConnectAsync(new Uri(wsUrl), cts.Token);
            int id = new Random().Next(1, 1000000);
            var msg = JsonSerializer.Serialize(new { id, method = "Runtime.evaluate", @params = new { expression, returnByValue = true } });
            var bytes = Encoding.UTF8.GetBytes(msg);
            await ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, cts.Token);
            var buffer = new byte[65536];
            var sb = new StringBuilder();
            while (true)
            {
                var res = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), cts.Token);
                if (res.MessageType == WebSocketMessageType.Close) return null;
                sb.Append(Encoding.UTF8.GetString(buffer, 0, res.Count));
                if (!res.EndOfMessage) continue;
                try
                {
                    using var doc = JsonDocument.Parse(sb.ToString());
                    if (doc.RootElement.TryGetProperty("id", out var rid) && rid.GetInt32() == id &&
                        doc.RootElement.TryGetProperty("result", out var result) &&
                        result.TryGetProperty("result", out var inner) &&
                        inner.TryGetProperty("value", out var val) &&
                        val.ValueKind == JsonValueKind.String)
                        return val.GetString();
                    return null;
                }
                catch { return null; }
            }
        }
        catch { return null; }
    }

    private static string? PickTarget(string listJson, string? urlHint)
    {
        try
        {
            using var doc = JsonDocument.Parse(listJson);
            string? fallback = null;
            string? hintHost = TryGetHost(urlHint);

            foreach (var el in doc.RootElement.EnumerateArray())
            {
                if (!el.TryGetProperty("type", out var t) || t.GetString() != "page")
                    continue;
                if (!el.TryGetProperty("webSocketDebuggerUrl", out var w))
                    continue;
                var ws = w.GetString();
                if (string.IsNullOrWhiteSpace(ws))
                    continue;

                fallback ??= ws;

                if (hintHost != null && el.TryGetProperty("url", out var u))
                {
                    var uh = TryGetHost(u.GetString());
                    if (uh != null && uh.Equals(hintHost, StringComparison.OrdinalIgnoreCase))
                        return ws;
                }
            }

            // Single page target with no hint -> use it; multiple without match -> ambiguous.
            if (hintHost == null)
                return fallback;
            return null;
        }
        catch
        {
            return null;
        }
    }

    private static string? TryGetHost(string? url)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(url)) return null;
            return new Uri(url).Host;
        }
        catch { return null; }
    }

    private static async Task<bool?> EvaluateAsync(string wsUrl, string expression)
    {
        using var ws = new ClientWebSocket();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        try
        {
            await ws.ConnectAsync(new Uri(wsUrl), cts.Token);

            int id = new Random().Next(1, 1000000);
            var msg = JsonSerializer.Serialize(new
            {
                id,
                method = "Runtime.evaluate",
                @params = new { expression, returnByValue = true }
            });
            var bytes = Encoding.UTF8.GetBytes(msg);
            await ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, cts.Token);

            var buffer = new byte[65536];
            while (true)
            {
                var res = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), cts.Token);
                if (res.MessageType == WebSocketMessageType.Close)
                    return null;
                var text = Encoding.UTF8.GetString(buffer, 0, res.Count);
                try
                {
                    using var doc = JsonDocument.Parse(text);
                    if (doc.RootElement.TryGetProperty("id", out var rid) &&
                        rid.GetInt32() == id &&
                        doc.RootElement.TryGetProperty("result", out var result) &&
                        result.TryGetProperty("result", out var inner) &&
                        inner.TryGetProperty("value", out var val) &&
                        (val.ValueKind == JsonValueKind.True || val.ValueKind == JsonValueKind.False))
                    {
                        return val.GetBoolean();
                    }
                }
                catch { }
                if (res.EndOfMessage && res.Count == 0)
                    return null;
            }
        }
        catch
        {
            return null;
        }
    }
}
