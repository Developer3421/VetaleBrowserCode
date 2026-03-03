using System;

namespace VetaleBrowser.VetaleBrowser.Core.Scripts.ErrorHandlers
{
    /// <summary>
    /// Browser error category for determining display style
    /// </summary>
    public enum BrowserErrorCategory
    {
        /// <summary>
        /// Network issues (user/connection problem)
        /// Color: orange (#FF9800 - same as ToolsMainPage)
        /// </summary>
        NetworkError,
        
        /// <summary>
        /// Server issues (site problem)
        /// Color: red (#F44336)
        /// </summary>
        ServerError,
        
        /// <summary>
        /// Security issues (certificates, blocking)
        /// Color: dark red (#B71C1C)
        /// </summary>
        SecurityError,
        
        /// <summary>
        /// General errors
        /// Color: gray (#607D8B)
        /// </summary>
        GeneralError
    }
    
    /// <summary>
    /// Browser error model with localized data
    /// </summary>
    public class BrowserError
    {
        /// <summary>
        /// CefGlue/Chromium error code (e.g., ERR_NAME_NOT_RESOLVED = -105)
        /// </summary>
        public int ErrorCode { get; set; }
        
        /// <summary>
        /// Chromium error name (e.g., "ERR_NAME_NOT_RESOLVED")
        /// </summary>
        public string ErrorName { get; set; } = string.Empty;
        
        /// <summary>
        /// URL that failed to load
        /// </summary>
        public string FailedUrl { get; set; } = string.Empty;
        
        /// <summary>
        /// Error category for styling
        /// </summary>
        public BrowserErrorCategory Category { get; set; }
        
        /// <summary>
        /// Localized error title
        /// </summary>
        public string Title { get; set; } = string.Empty;
        
        /// <summary>
        /// Localized error description
        /// </summary>
        public string Description { get; set; } = string.Empty;
        
        /// <summary>
        /// Localized tips for resolution
        /// </summary>
        public string[] Tips { get; set; } = Array.Empty<string>();
        
        /// <summary>
        /// Emoji/icon for display
        /// </summary>
        public string Icon { get; set; } = "❌";
        
        /// <summary>
        /// Error page background color (HEX)
        /// </summary>
        public string BackgroundColor { get; set; } = "#FF9800";
        
        /// <summary>
        /// Text color (HEX)
        /// </summary>
        public string ForegroundColor { get; set; } = "#303030";
        
        /// <summary>
        /// Time when the error occurred
        /// </summary>
        public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
        
        /// <summary>
        /// Whether the page can be retried
        /// </summary>
        public bool CanRetry { get; set; } = true;
        
        /// <summary>
        /// Whether to show search (for pages not found)
        /// </summary>
        public bool ShowSearch { get; set; }
    }
    
    /// <summary>
    /// Chromium/CefGlue error codes
    /// https://source.chromium.org/chromium/chromium/src/+/main:net/base/net_error_list.h
    /// </summary>
    public static class CefErrorCodes
    {
        // === NETWORK ERRORS ===
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
        
        // === SERVER ERRORS ===
        public const int ERR_EMPTY_RESPONSE = -324;
        public const int ERR_TOO_MANY_REDIRECTS = -310;
        public const int ERR_INVALID_RESPONSE = -320;
        public const int ERR_RESPONSE_HEADERS_TOO_BIG = -325;
        public const int ERR_CONTENT_DECODING_FAILED = -330;
        
        // === SECURITY ERRORS ===
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
        
        // === GENERAL ERRORS ===
        public const int ERR_ABORTED = -3;
        public const int ERR_FAILED = -2;
        public const int ERR_FILE_NOT_FOUND = -6;
        public const int ERR_ACCESS_DENIED = -10;
        public const int ERR_UNKNOWN_URL_SCHEME = -302;
        public const int ERR_INVALID_URL = -300;
        public const int ERR_CACHE_MISS = -400;
    }
}
