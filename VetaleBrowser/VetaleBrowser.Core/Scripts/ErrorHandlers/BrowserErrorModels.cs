using System;

namespace VetaleBrowser.VetaleBrowser.Core.Scripts.ErrorHandlers
{
    /// <summary>
    /// Категорія помилки браузера для визначення стилю відображення
    /// </summary>
    public enum BrowserErrorCategory
    {
        /// <summary>
        /// Мережеві проблеми (проблема користувача/з'єднання)
        /// Колір: помаранчевий (#FF9800 - як у ToolsMainPage)
        /// </summary>
        NetworkError,
        
        /// <summary>
        /// Проблеми сервера (проблема сайту) 
        /// Колір: червоний (#F44336)
        /// </summary>
        ServerError,
        
        /// <summary>
        /// Проблеми безпеки (сертифікати, блокування)
        /// Колір: темно-червоний (#B71C1C)
        /// </summary>
        SecurityError,
        
        /// <summary>
        /// Загальні помилки
        /// Колір: сірий (#607D8B)
        /// </summary>
        GeneralError
    }
    
    /// <summary>
    /// Модель помилки браузера з локалізованими даними
    /// </summary>
    public class BrowserError
    {
        /// <summary>
        /// Код помилки CefGlue/Chromium (наприклад, ERR_NAME_NOT_RESOLVED = -105)
        /// </summary>
        public int ErrorCode { get; set; }
        
        /// <summary>
        /// Назва помилки Chromium (наприклад, "ERR_NAME_NOT_RESOLVED")
        /// </summary>
        public string ErrorName { get; set; } = string.Empty;
        
        /// <summary>
        /// URL, на який не вдалося перейти
        /// </summary>
        public string FailedUrl { get; set; } = string.Empty;
        
        /// <summary>
        /// Категорія помилки для стилізації
        /// </summary>
        public BrowserErrorCategory Category { get; set; }
        
        /// <summary>
        /// Локалізований заголовок помилки
        /// </summary>
        public string Title { get; set; } = string.Empty;
        
        /// <summary>
        /// Локалізований опис помилки
        /// </summary>
        public string Description { get; set; } = string.Empty;
        
        /// <summary>
        /// Локалізовані поради для вирішення
        /// </summary>
        public string[] Tips { get; set; } = Array.Empty<string>();
        
        /// <summary>
        /// Emoji/іконка для відображення
        /// </summary>
        public string Icon { get; set; } = "❌";
        
        /// <summary>
        /// Колір фону сторінки помилки (HEX)
        /// </summary>
        public string BackgroundColor { get; set; } = "#FF9800";
        
        /// <summary>
        /// Колір тексту (HEX)
        /// </summary>
        public string ForegroundColor { get; set; } = "#303030";
        
        /// <summary>
        /// Час виникнення помилки
        /// </summary>
        public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
        
        /// <summary>
        /// Чи можна спробувати перезавантажити сторінку
        /// </summary>
        public bool CanRetry { get; set; } = true;
        
        /// <summary>
        /// Чи показувати пошук (для сторінок не знайдено)
        /// </summary>
        public bool ShowSearch { get; set; }
    }
    
    /// <summary>
    /// Коди помилок Chromium/CefGlue
    /// https://source.chromium.org/chromium/chromium/src/+/main:net/base/net_error_list.h
    /// </summary>
    public static class CefErrorCodes
    {
        // === МЕРЕЖЕВІ ПОМИЛКИ (Network Errors) ===
        public const int ERR_NAME_NOT_RESOLVED = -105;
        public const int ERR_INTERNET_DISCONNECTED = -106;
        public const int ERR_CONNECTION_REFUSED = -102;
        public const int ERR_CONNECTION_RESET = -101;
        public const int ERR_CONNECTION_CLOSED = -100;
        public const int ERR_CONNECTION_TIMED_OUT = -118;
        public const int ERR_ADDRESS_UNREACHABLE = -109;
        public const int ERR_NETWORK_CHANGED = -21;
        public const int ERR_DNS_TIMED_OUT = -803;
        public const int ERR_DNS_SERVER_FAILED = -802;
        public const int ERR_DNS_MALFORMED_RESPONSE = -801;
        public const int ERR_PROXY_CONNECTION_FAILED = -130;
        public const int ERR_NO_SUPPORTED_PROXIES = -336;
        public const int ERR_TUNNEL_CONNECTION_FAILED = -111;
        
        // === ПОМИЛКИ СЕРВЕРА (Server Errors) ===
        public const int ERR_EMPTY_RESPONSE = -324;
        public const int ERR_TOO_MANY_REDIRECTS = -310;
        public const int ERR_INVALID_RESPONSE = -320;
        public const int ERR_RESPONSE_HEADERS_TOO_BIG = -325;
        public const int ERR_CONTENT_DECODING_FAILED = -330;
        
        // === ПОМИЛКИ БЕЗПЕКИ (Security Errors) ===
        public const int ERR_SSL_PROTOCOL_ERROR = -107;
        public const int ERR_CERT_DATE_INVALID = -201;
        public const int ERR_CERT_AUTHORITY_INVALID = -202;
        public const int ERR_CERT_COMMON_NAME_INVALID = -200;
        public const int ERR_CERT_REVOKED = -206;
        public const int ERR_CERT_INVALID = -207;
        public const int ERR_SSL_HANDSHAKE_NOT_COMPLETED = -148;
        public const int ERR_INSECURE_RESPONSE = -501;
        public const int ERR_BLOCKED_BY_CLIENT = -20;
        public const int ERR_BLOCKED_BY_RESPONSE = -27;
        
        // === ЗАГАЛЬНІ ПОМИЛКИ (General Errors) ===
        public const int ERR_ABORTED = -3;
        public const int ERR_FAILED = -2;
        public const int ERR_FILE_NOT_FOUND = -6;
        public const int ERR_ACCESS_DENIED = -10;
        public const int ERR_UNKNOWN_URL_SCHEME = -302;
        public const int ERR_INVALID_URL = -300;
        public const int ERR_CACHE_MISS = -400;
    }
}

