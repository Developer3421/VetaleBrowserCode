using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace VetaleBrowser.VetaleBrowser.Core.Services
{
    /// <summary>
    /// Supervisor for the microStudio game engine (built via npm).
    /// Primary mode: launches the real node server (server/app.js) as a child process
    /// on port 8081 and opens it in a browser tab via index page.
    /// Fallback (node not installed): serves static files via built-in HttpListener hub.
    /// </summary>
    public class MicroStudioServer : IDisposable
    {
        private const int PreferredPort = 8081;

        private HttpListener? _listener;
        private CancellationTokenSource? _cts;
        private Task? _serverTask;
        private Process? _nodeProcess;
        private readonly string _rootPath;
        private readonly int _port;
        private int _nodePort;
        private bool _isRunning;
        private bool _nodeRunning;

        private static MicroStudioServer? _instance;
        private static readonly object _lock = new();

        /// <summary>
        /// Server port (static fallback listener)
        /// </summary>
        public int Port => _port;

        /// <summary>
        /// Engine URL: real node IDE when running, otherwise static hub
        /// </summary>
        public string ServerUrl => _nodeRunning
            ? $"http://localhost:{_nodePort}/"
            : $"http://localhost:{_port}/";

        /// <summary>
        /// Whether the server is running (node or fallback)
        /// </summary>
        public bool IsRunning => _isRunning;

        /// <summary>
        /// Whether the real node engine is running
        /// </summary>
        public bool NodeRunning => _nodeRunning;

        /// <summary>
        /// Root folder of microStudio
        /// </summary>
        public string RootPath => _rootPath;

        /// <summary>
        /// Whether the microStudio folder exists
        /// </summary>
        public bool EngineExists => Directory.Exists(_rootPath);

        /// <summary>
        /// Singleton server instance
        /// </summary>
        public static MicroStudioServer Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        _instance ??= new MicroStudioServer();
                    }
                }
                return _instance;
            }
        }

        private MicroStudioServer()
        {
            _port = FindAvailablePort(9100, 9300, 9191);
            _rootPath = FindEnginePath();
            _nodePort = PreferredPort;
            Debug.WriteLine($"[MicroStudioServer] Engine path: {_rootPath}");
            Debug.WriteLine($"[MicroStudioServer] Fallback port: {_port}");
        }

        /// <summary>
        /// Finds an available port, preferred first
        /// </summary>
        private static int FindAvailablePort(int from, int to, int fallback)
        {
            // Preferred port first (8081 for node), then scan upward
            for (int port = from == 9100 ? 9100 : from; port < to; port++)
            {
                if (IsPortFree(port)) return port;
            }
            return fallback;
        }

        private static bool IsPortFree(int port)
        {
            try
            {
                var listener = new TcpListener(IPAddress.Loopback, port);
                listener.Start();
                listener.Stop();
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Finds the microStudio folder
        /// </summary>
        private static string FindEnginePath()
        {
            var appDir = AppDomain.CurrentDomain.BaseDirectory;

            var possiblePaths = new[]
            {
                Path.Combine(appDir, "MicroStudio"),
            };

            foreach (var path in possiblePaths)
            {
                if (Directory.Exists(path))
                {
                    return path;
                }
            }

            return possiblePaths[0]; // Fallback
        }

        private string ServerDir => Path.Combine(_rootPath, "server");

        /// <summary>
        /// Locates node.exe (PATH or default install location)
        /// </summary>
        private static string? FindNodeExe()
        {
            try
            {
                // 1. PATH via where
                var where = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "where",
                        Arguments = "node",
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                    }
                };
                where.Start();
                var output = where.StandardOutput.ReadToEnd();
                where.WaitForExit(5000);
                if (where.ExitCode == 0)
                {
                    foreach (var line in output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        var candidate = line.Trim().Trim('"');
                        if (File.Exists(candidate)) return candidate;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MicroStudioServer] where node failed: {ex.Message}");
            }

            // 2. Default install locations
            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            var candidates = new[]
            {
                Path.Combine(programFiles, "nodejs", "node.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "nodejs", "node.exe"),
            };
            foreach (var c in candidates)
            {
                if (File.Exists(c)) return c;
            }

            return null;
        }

        /// <summary>
        /// Writes the chosen port into MicroStudio/config.json so node binds it
        /// </summary>
        private void EnsurePortInConfig(int port)
        {
            try
            {
                var configPath = Path.Combine(_rootPath, "config.json");
                if (!File.Exists(configPath)) return;

                var json = File.ReadAllText(configPath);
                // Minimal JSON patch without extra dependencies
                json = System.Text.RegularExpressions.Regex.Replace(json, @"""port""\s*:\s*\d+", $"\"port\": {port}");
                json = System.Text.RegularExpressions.Regex.Replace(json, @"http://localhost:\d+", $"http://localhost:{port}");
                File.WriteAllText(configPath, json);
                Debug.WriteLine($"[MicroStudioServer] config.json port set to {port}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MicroStudioServer] Failed to patch config.json: {ex.Message}");
            }
        }

        private static bool WaitForPort(int port, int timeoutMs = 30000)
        {
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                try
                {
                    using var client = new TcpClient();
                    var task = client.ConnectAsync(IPAddress.Loopback, port);
                    if (task.Wait(1000))
                    {
                        return true;
                    }
                }
                catch { }
                Thread.Sleep(500);
            }
            return false;
        }

        /// <summary>
        /// Starts the server: real node engine first, static fallback otherwise
        /// </summary>
        public void Start()
        {
            if (_isRunning) return;

            // Try real node engine: prefer 8081, then first free port above it
            var nodeExe = FindNodeExe();
            var appJs = Path.Combine(ServerDir, "app.js");
            if (nodeExe != null && Directory.Exists(ServerDir) && File.Exists(appJs))
            {
                var port = PreferredPort;
                if (!IsPortFree(port))
                {
                    for (int p = PreferredPort + 1; p < PreferredPort + 20; p++)
                    {
                        if (IsPortFree(p)) { port = p; break; }
                    }
                }

                try
                {
                    EnsurePortInConfig(port);

                    _nodeProcess = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = nodeExe,
                            Arguments = "app.js",
                            WorkingDirectory = ServerDir,
                            UseShellExecute = false,
                            CreateNoWindow = true,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                        },
                        EnableRaisingEvents = true,
                    };
                    _nodeProcess.OutputDataReceived += (_, e) =>
                    {
                        if (!string.IsNullOrEmpty(e.Data)) Debug.WriteLine($"[microStudio-node] {e.Data}");
                    };
                    _nodeProcess.ErrorDataReceived += (_, e) =>
                    {
                        if (!string.IsNullOrEmpty(e.Data)) Debug.WriteLine($"[microStudio-node:err] {e.Data}");
                    };
                    _nodeProcess.Start();
                    _nodeProcess.BeginOutputReadLine();
                    _nodeProcess.BeginErrorReadLine();

                    if (WaitForPort(port))
                    {
                        _nodePort = port;
                        _nodeRunning = true;
                        _isRunning = true;
                        Debug.WriteLine($"[MicroStudioServer] Node engine running on http://localhost:{port}/");
                        return;
                    }

                    Debug.WriteLine("[MicroStudioServer] Node engine did not answer in time, killing it");
                    try { _nodeProcess.Kill(); } catch { }
                    _nodeProcess = null;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[MicroStudioServer] Failed to start node engine: {ex.Message}");
                    try { _nodeProcess?.Kill(); } catch { }
                    _nodeProcess = null;
                }
            }
            else
            {
                Debug.WriteLine($"[MicroStudioServer] Node not available (nodeExe={nodeExe}, serverDir={Directory.Exists(ServerDir)}), using static fallback");
            }

            StartStaticFallback();
        }

        /// <summary>
        /// Static file fallback (no node installed)
        /// </summary>
        private void StartStaticFallback()
        {
            try
            {
                if (!Directory.Exists(_rootPath))
                {
                    Debug.WriteLine($"[MicroStudioServer] WARNING: Engine directory not found: {_rootPath}");
                }

                _listener = new HttpListener();
                _listener.Prefixes.Add($"http://localhost:{_port}/");
                _listener.Start();

                _cts = new CancellationTokenSource();
                _serverTask = Task.Run(() => ServerLoop(_cts.Token));

                _isRunning = true;
                Debug.WriteLine($"[MicroStudioServer] Static fallback started on http://localhost:{_port}/");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MicroStudioServer] Failed to start fallback: {ex.Message}");
            }
        }

        /// <summary>
        /// Stops the server (node child + fallback listener)
        /// </summary>
        public void Stop()
        {
            if (!_isRunning && _nodeProcess == null && _listener == null) return;

            try
            {
                if (_nodeProcess != null)
                {
                    try
                    {
                        if (!_nodeProcess.HasExited) _nodeProcess.Kill();
                        _nodeProcess.WaitForExit(3000);
                    }
                    catch { }
                    _nodeProcess.Dispose();
                    _nodeProcess = null;
                }
                _nodeRunning = false;

                _cts?.Cancel();
                if (_listener != null)
                {
                    try { _listener.Stop(); } catch { }
                    try { _listener.Close(); } catch { }
                    _listener = null;
                }
                try { _serverTask?.Wait(1000); } catch { }

                _isRunning = false;
                Debug.WriteLine("[MicroStudioServer] Stopped");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MicroStudioServer] Error stopping: {ex.Message}");
            }
        }

        /// <summary>
        /// Main server loop (static fallback)
        /// </summary>
        private async Task ServerLoop(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested && _listener != null && _listener.IsListening)
            {
                try
                {
                    var context = await _listener.GetContextAsync();
                    _ = Task.Run(() => HandleRequest(context), ct);
                }
                catch (HttpListenerException) when (ct.IsCancellationRequested)
                {
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[MicroStudioServer] Error in server loop: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Handles an HTTP request (static fallback)
        /// </summary>
        private void HandleRequest(HttpListenerContext context)
        {
            var response = context.Response;
            try
            {
                var request = context.Request;

                var urlPath = request.Url?.LocalPath ?? "/";
                urlPath = Uri.UnescapeDataString(urlPath);

                if (urlPath == "/" || string.IsNullOrEmpty(urlPath))
                {
                    var hubPath = Path.Combine(_rootPath, "index.html");
                    if (File.Exists(hubPath))
                    {
                        SendFile(response, hubPath);
                    }
                    else
                    {
                        SendHubPage(response);
                    }
                    return;
                }

                var relativePath = urlPath.TrimStart('/');
                var lower = relativePath.Replace('\\', '/').ToLowerInvariant();
                if (lower == ".git" || lower.StartsWith(".git/") || lower.StartsWith("server/db/") || lower.StartsWith("server/filestorage/"))
                {
                    SendText(response, 403, "Forbidden");
                    return;
                }

                var filePath = Path.Combine(_rootPath, relativePath.Replace('/', Path.DirectorySeparatorChar));

                if (!Directory.Exists(_rootPath))
                {
                    SendText(response, 404, $"Engine directory not found: {_rootPath}");
                    return;
                }

                if (File.Exists(filePath))
                {
                    SendFile(response, filePath);
                }
                else
                {
                    SendText(response, 404, $"404 - File Not Found: {relativePath}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MicroStudioServer] Error handling request: {ex}");
                try
                {
                    SendText(response, 500, $"500 Internal Server Error: {ex.Message}");
                }
                catch { }
            }
        }

        private static void SendText(HttpListenerResponse response, int status, string text)
        {
            var bytes = Encoding.UTF8.GetBytes(text);
            response.StatusCode = status;
            response.ContentType = "text/plain; charset=utf-8";
            response.ContentLength64 = bytes.Length;
            using var output = response.OutputStream;
            output.Write(bytes, 0, bytes.Length);
            output.Flush();
        }

        private static void SendFile(HttpListenerResponse response, string filePath)
        {
            var contentType = GetContentType(filePath);
            response.ContentType = contentType;
            response.AddHeader("Access-Control-Allow-Origin", "*");

            if (IsStaticFile(filePath))
            {
                response.AddHeader("Cache-Control", "public, max-age=3600");
            }

            var content = File.ReadAllBytes(filePath);
            response.StatusCode = 200;
            response.ContentLength64 = content.Length;
            using var output = response.OutputStream;
            output.Write(content, 0, content.Length);
            output.Flush();
        }

        /// <summary>
        /// Local hub page (static fallback only)
        /// </summary>
        private void SendHubPage(HttpListenerResponse response)
        {
            var html = $@"<!DOCTYPE html>
<html lang=""en"">
<head><meta charset=""utf-8""><title>microStudio — Vetale Browser (offline)</title>
<style>
body{{font-family:Segoe UI,Arial,sans-serif;background:#1b1030;color:#efe9ff;margin:0;padding:40px}}
.card{{max-width:640px;margin:auto;background:#2a1b4d;border-radius:12px;padding:28px}}
h1{{color:#c4a5ff;margin-top:0}}a{{color:#ffb14e}}.row{{margin:10px 0}}
code{{background:#170d28;padding:2px 6px;border-radius:4px}}
</style></head>
<body><div class=""card"">
<h1>microStudio — offline engine</h1>
<div class=""row"">Node.js was not found. Install Node.js LTS to run the full microStudio IDE.</div>
<div class=""row"">Engine folder: <code>{_rootPath}</code></div>
<div class=""row""><a href=""/static/"">Browse static files</a> &nbsp;|&nbsp; <a href=""/static/doc/"">Documentation</a> &nbsp;|&nbsp; <a href=""/static/tutorials/"">Tutorials</a></div>
</div></body></html>";
            var bytes = Encoding.UTF8.GetBytes(html);
            response.StatusCode = 200;
            response.ContentType = "text/html; charset=utf-8";
            response.ContentLength64 = bytes.Length;
            using var output = response.OutputStream;
            output.Write(bytes, 0, bytes.Length);
            output.Flush();
        }

        /// <summary>
        /// Determines the MIME type of a file
        /// </summary>
        private static string GetContentType(string filePath)
        {
            var ext = Path.GetExtension(filePath).ToLowerInvariant();
            return ext switch
            {
                ".html" or ".htm" => "text/html; charset=utf-8",
                ".css" => "text/css; charset=utf-8",
                ".js" => "application/javascript; charset=utf-8",
                ".json" => "application/json; charset=utf-8",
                ".pug" => "text/plain; charset=utf-8",
                ".coffee" => "text/plain; charset=utf-8",
                ".md" => "text/markdown; charset=utf-8",
                ".png" => "image/png",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".gif" => "image/gif",
                ".svg" => "image/svg+xml",
                ".ico" => "image/x-icon",
                ".woff" => "font/woff",
                ".woff2" => "font/woff2",
                ".ttf" => "font/ttf",
                ".eot" => "application/vnd.ms-fontobject",
                ".mp3" => "audio/mpeg",
                ".ogg" => "audio/ogg",
                ".wav" => "audio/wav",
                ".mp4" => "video/mp4",
                ".webm" => "video/webm",
                _ => "application/octet-stream"
            };
        }

        /// <summary>
        /// Whether this is a static file for caching
        /// </summary>
        private static bool IsStaticFile(string filePath)
        {
            var ext = Path.GetExtension(filePath).ToLowerInvariant();
            return ext is ".png" or ".jpg" or ".jpeg" or ".gif" or ".svg" or ".ico"
                or ".woff" or ".woff2" or ".ttf" or ".eot"
                or ".mp3" or ".ogg" or ".wav";
        }

        public void Dispose()
        {
            Stop();
            _instance = null;
        }
    }
}
