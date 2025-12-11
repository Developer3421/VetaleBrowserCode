using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace VetaleBrowser.VetaleBrowser.Core.Services
{
    /// <summary>
    /// Локальний HTTP сервер для HexGL гри
    /// Обходить обмеження CEF на file:// URLs для текстур
    /// </summary>
    public class LocalGameServer : IDisposable
    {
        private HttpListener? _listener;
        private CancellationTokenSource? _cts;
        private Task? _serverTask;
        private readonly string _gameRootPath;
        private readonly int _port;
        private bool _isRunning;
        
        private static LocalGameServer? _instance;
        private static readonly object _lock = new();
        
        /// <summary>
        /// Порт сервера
        /// </summary>
        public int Port => _port;
        
        /// <summary>
        /// URL гри
        /// </summary>
        public string GameUrl => $"http://localhost:{_port}/index.html";
        
        /// <summary>
        /// URL іконки гри для вкладки браузера
        /// </summary>
        public string GameIconUrl => $"http://localhost:{_port}/icon_32.png";
        
        /// <summary>
        /// Чи запущений сервер
        /// </summary>
        public bool IsRunning => _isRunning;
        
        /// <summary>
        /// Singleton інстанс сервера
        /// </summary>
        public static LocalGameServer Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        _instance ??= new LocalGameServer();
                    }
                }
                return _instance;
            }
        }
        
        private LocalGameServer()
        {
            _port = FindAvailablePort();
            _gameRootPath = FindGamePath();
            Debug.WriteLine($"[LocalGameServer] Game path: {_gameRootPath}");
            Debug.WriteLine($"[LocalGameServer] Port: {_port}");
        }
        
        /// <summary>
        /// Знаходить доступний порт
        /// </summary>
        private static int FindAvailablePort()
        {
            // Починаємо з 8080 і шукаємо вільний порт
            for (int port = 8080; port < 9000; port++)
            {
                try
                {
                    var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, port);
                    listener.Start();
                    listener.Stop();
                    return port;
                }
                catch
                {
                    // Порт зайнятий, пробуємо наступний
                }
            }
            return 8888; // Fallback
        }
        
        /// <summary>
        /// Знаходить шлях до гри
        /// </summary>
        private static string FindGamePath()
        {
            var appDir = AppDomain.CurrentDomain.BaseDirectory;
            
            var possiblePaths = new[]
            {
                Path.Combine(appDir, "VetaleBrowserOfflineGame", "HexGL-master"),
                Path.Combine(appDir, "HexGL-master"),
                Path.Combine(appDir, "HexGL"),
            };
            
            foreach (var path in possiblePaths)
            {
                if (Directory.Exists(path) && File.Exists(Path.Combine(path, "index.html")))
                {
                    return path;
                }
            }
            
            return possiblePaths[0]; // Fallback
        }
        
        /// <summary>
        /// Запускає сервер
        /// </summary>
        public void Start()
        {
            if (_isRunning) return;
            
            try
            {
                // Перевіряємо чи існує директорія гри
                if (!Directory.Exists(_gameRootPath))
                {
                    Debug.WriteLine($"[LocalGameServer] WARNING: Game directory not found: {_gameRootPath}");
                }
                else
                {
                    Debug.WriteLine($"[LocalGameServer] Game directory found: {_gameRootPath}");
                    var indexPath = Path.Combine(_gameRootPath, "index.html");
                    Debug.WriteLine($"[LocalGameServer] index.html exists: {File.Exists(indexPath)}");
                }
                
                _listener = new HttpListener();
                _listener.Prefixes.Add($"http://localhost:{_port}/");
                _listener.Start();
                
                _cts = new CancellationTokenSource();
                _serverTask = Task.Run(() => ServerLoop(_cts.Token));
                
                _isRunning = true;
                Debug.WriteLine($"[LocalGameServer] Started on http://localhost:{_port}/");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[LocalGameServer] Failed to start: {ex.Message}");
                Debug.WriteLine($"[LocalGameServer] Exception: {ex}");
            }
        }
        
        /// <summary>
        /// Зупиняє сервер
        /// </summary>
        public void Stop()
        {
            if (!_isRunning) return;
            
            try
            {
                _cts?.Cancel();
                _listener?.Stop();
                _listener?.Close();
                _serverTask?.Wait(1000);
                
                _isRunning = false;
                Debug.WriteLine("[LocalGameServer] Stopped");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[LocalGameServer] Error stopping: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Головний цикл сервера
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
                    Debug.WriteLine($"[LocalGameServer] Error in server loop: {ex.Message}");
                }
            }
        }
        
        /// <summary>
        /// Обробляє HTTP запит
        /// </summary>
        private void HandleRequest(HttpListenerContext context)
        {
            var response = context.Response;
            try
            {
                var request = context.Request;
                
                // Отримуємо шлях до файлу
                var urlPath = request.Url?.LocalPath ?? "/";
                if (urlPath == "/") urlPath = "/index.html";
                
                // Декодуємо URL (для пробілів та спецсимволів)
                urlPath = Uri.UnescapeDataString(urlPath);
                
                // Видаляємо початковий слеш
                var relativePath = urlPath.TrimStart('/');
                var filePath = Path.Combine(_gameRootPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
                
                Debug.WriteLine($"[LocalGameServer] Request: {urlPath} -> {filePath}");
                Debug.WriteLine($"[LocalGameServer] Game root: {_gameRootPath}");
                
                // Перевіряємо чи існує директорія гри
                if (!Directory.Exists(_gameRootPath))
                {
                    Debug.WriteLine($"[LocalGameServer] ERROR: Game directory not found: {_gameRootPath}");
                    response.StatusCode = 404;
                    response.ContentType = "text/plain; charset=utf-8";
                    var notFound = Encoding.UTF8.GetBytes($"Game directory not found: {_gameRootPath}");
                    response.ContentLength64 = notFound.Length;
                    
                    using (var output = response.OutputStream)
                    {
                        output.Write(notFound, 0, notFound.Length);
                        output.Flush();
                    }
                    return;
                }
                
                if (File.Exists(filePath))
                {
                    // Визначаємо MIME тип
                    var contentType = GetContentType(filePath);
                    response.ContentType = contentType;
                    
                    // Додаємо CORS заголовки
                    response.AddHeader("Access-Control-Allow-Origin", "*");
                    response.AddHeader("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
                    response.AddHeader("Access-Control-Allow-Headers", "*");
                    
                    // Кешування для статичних файлів
                    if (IsStaticFile(filePath))
                    {
                        response.AddHeader("Cache-Control", "public, max-age=3600");
                    }
                    
                    // Читаємо та відправляємо файл
                    var content = File.ReadAllBytes(filePath);
                    response.StatusCode = 200;
                    response.ContentLength64 = content.Length;
                    
                    using (var output = response.OutputStream)
                    {
                        output.Write(content, 0, content.Length);
                        output.Flush();
                    }
                }
                else
                {
                    // 404 Not Found
                    response.StatusCode = 404;
                    response.ContentType = "text/plain; charset=utf-8";
                    var notFound = Encoding.UTF8.GetBytes($"404 - File Not Found: {relativePath}");
                    response.ContentLength64 = notFound.Length;
                    
                    using (var output = response.OutputStream)
                    {
                        output.Write(notFound, 0, notFound.Length);
                        output.Flush();
                    }
                    Debug.WriteLine($"[LocalGameServer] 404: {filePath}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[LocalGameServer] Error handling request: {ex}");
                try
                {
                    response.StatusCode = 500;
                    response.ContentType = "text/plain; charset=utf-8";
                    var errorMsg = Encoding.UTF8.GetBytes($"500 Internal Server Error: {ex.Message}");
                    response.ContentLength64 = errorMsg.Length;
                    
                    using (var output = response.OutputStream)
                    {
                        output.Write(errorMsg, 0, errorMsg.Length);
                        output.Flush();
                    }
                }
                catch { }
            }
        }
        
        /// <summary>
        /// Визначає MIME тип файлу
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
                ".glsl" or ".vert" or ".frag" => "text/plain; charset=utf-8",
                ".obj" => "text/plain; charset=utf-8",
                ".mtl" => "text/plain; charset=utf-8",
                _ => "application/octet-stream"
            };
        }
        
        /// <summary>
        /// Чи це статичний файл для кешування
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

