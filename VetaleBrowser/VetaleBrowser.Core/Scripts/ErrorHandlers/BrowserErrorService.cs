using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;

namespace VetaleBrowser.VetaleBrowser.Core.Scripts.ErrorHandlers
{
    /// <summary>
    /// Service for localizing and handling CefGlue/Chromium errors.
    /// Converts technical error codes into user-friendly pages.
    /// </summary>
    public static class BrowserErrorService
    {
        /// <summary>
        /// Gets localized error information by CefGlue error code
        /// </summary>
        /// <param name="errorCode">CefGlue error code (negative number)</param>
        /// <param name="failedUrl">URL that failed to load</param>
        /// <param name="errorText">Original error text from CefGlue</param>
        /// <returns>Localized error model</returns>
        public static BrowserError GetLocalizedError(int errorCode, string failedUrl, string? errorText = null)
        {
            var error = new BrowserError
            {
                ErrorCode = errorCode,
                ErrorName = GetErrorName(errorCode),
                FailedUrl = failedUrl,
                Category = GetErrorCategory(errorCode),
                OccurredAt = DateTime.UtcNow
            };
            
            // Determine colors by category
            SetCategoryStyles(error);
            
            // Get localized texts
            SetLocalizedContent(error);
            
            return error;
        }
        
        /// <summary>
        /// Gets HTTP server error information (4xx, 5xx)
        /// </summary>
        /// <param name="httpStatusCode">HTTP status code</param>
        /// <param name="failedUrl">Page URL</param>
        /// <returns>Localized error model</returns>
        public static BrowserError GetHttpError(int httpStatusCode, string failedUrl)
        {
            var error = new BrowserError
            {
                ErrorCode = httpStatusCode,
                ErrorName = $"HTTP_{httpStatusCode}",
                FailedUrl = failedUrl,
                Category = httpStatusCode >= 500 ? BrowserErrorCategory.ServerError : BrowserErrorCategory.GeneralError,
                OccurredAt = DateTime.UtcNow
            };
            
            SetCategoryStyles(error);
            SetHttpErrorContent(error, httpStatusCode);
            
            return error;
        }
        
        /// <summary>
        /// Gets the error name by code
        /// </summary>
        private static string GetErrorName(int errorCode)
        {
            return errorCode switch
            {
                CefErrorCodes.ERR_NAME_NOT_RESOLVED => "ERR_NAME_NOT_RESOLVED",
                CefErrorCodes.ERR_INTERNET_DISCONNECTED => "ERR_INTERNET_DISCONNECTED",
                CefErrorCodes.ERR_CONNECTION_REFUSED => "ERR_CONNECTION_REFUSED",
                CefErrorCodes.ERR_CONNECTION_RESET => "ERR_CONNECTION_RESET",
                CefErrorCodes.ERR_CONNECTION_CLOSED => "ERR_CONNECTION_CLOSED",
                CefErrorCodes.ERR_CONNECTION_TIMED_OUT => "ERR_CONNECTION_TIMED_OUT",
                CefErrorCodes.ERR_ADDRESS_UNREACHABLE => "ERR_ADDRESS_UNREACHABLE",
                CefErrorCodes.ERR_NETWORK_CHANGED => "ERR_NETWORK_CHANGED",
                CefErrorCodes.ERR_DNS_TIMED_OUT => "ERR_DNS_TIMED_OUT",
                CefErrorCodes.ERR_DNS_SERVER_FAILED => "ERR_DNS_SERVER_FAILED",
                CefErrorCodes.ERR_PROXY_CONNECTION_FAILED => "ERR_PROXY_CONNECTION_FAILED",
                CefErrorCodes.ERR_EMPTY_RESPONSE => "ERR_EMPTY_RESPONSE",
                CefErrorCodes.ERR_TOO_MANY_REDIRECTS => "ERR_TOO_MANY_REDIRECTS",
                CefErrorCodes.ERR_INVALID_RESPONSE => "ERR_INVALID_RESPONSE",
                CefErrorCodes.ERR_SSL_PROTOCOL_ERROR => "ERR_SSL_PROTOCOL_ERROR",
                CefErrorCodes.ERR_CERT_DATE_INVALID => "ERR_CERT_DATE_INVALID",
                CefErrorCodes.ERR_CERT_AUTHORITY_INVALID => "ERR_CERT_AUTHORITY_INVALID",
                CefErrorCodes.ERR_CERT_COMMON_NAME_INVALID => "ERR_CERT_COMMON_NAME_INVALID",
                CefErrorCodes.ERR_CERT_REVOKED => "ERR_CERT_REVOKED",
                CefErrorCodes.ERR_CERT_INVALID => "ERR_CERT_INVALID",
                CefErrorCodes.ERR_BLOCKED_BY_CLIENT => "ERR_BLOCKED_BY_CLIENT",
                CefErrorCodes.ERR_ABORTED => "ERR_ABORTED",
                CefErrorCodes.ERR_FAILED => "ERR_FAILED",
                CefErrorCodes.ERR_FILE_NOT_FOUND => "ERR_FILE_NOT_FOUND",
                CefErrorCodes.ERR_ACCESS_DENIED => "ERR_ACCESS_DENIED",
                CefErrorCodes.ERR_UNKNOWN_URL_SCHEME => "ERR_UNKNOWN_URL_SCHEME",
                CefErrorCodes.ERR_INVALID_URL => "ERR_INVALID_URL",
                CefErrorCodes.ERR_CACHE_MISS => "ERR_CACHE_MISS",
                _ => $"ERR_UNKNOWN_{Math.Abs(errorCode)}"
            };
        }
        
        /// <summary>
        /// Determines the error category
        /// </summary>
        private static BrowserErrorCategory GetErrorCategory(int errorCode)
        {
            return errorCode switch
            {
                // Network errors
                CefErrorCodes.ERR_NAME_NOT_RESOLVED or
                CefErrorCodes.ERR_INTERNET_DISCONNECTED or
                CefErrorCodes.ERR_CONNECTION_REFUSED or
                CefErrorCodes.ERR_CONNECTION_RESET or
                CefErrorCodes.ERR_CONNECTION_CLOSED or
                CefErrorCodes.ERR_CONNECTION_TIMED_OUT or
                CefErrorCodes.ERR_ADDRESS_UNREACHABLE or
                CefErrorCodes.ERR_NETWORK_CHANGED or
                CefErrorCodes.ERR_DNS_TIMED_OUT or
                CefErrorCodes.ERR_DNS_SERVER_FAILED or
                CefErrorCodes.ERR_DNS_MALFORMED_RESPONSE or
                CefErrorCodes.ERR_PROXY_CONNECTION_FAILED or
                CefErrorCodes.ERR_NO_SUPPORTED_PROXIES or
                CefErrorCodes.ERR_TUNNEL_CONNECTION_FAILED
                    => BrowserErrorCategory.NetworkError,
                
                // Server errors
                CefErrorCodes.ERR_EMPTY_RESPONSE or
                CefErrorCodes.ERR_TOO_MANY_REDIRECTS or
                CefErrorCodes.ERR_INVALID_RESPONSE or
                CefErrorCodes.ERR_RESPONSE_HEADERS_TOO_BIG or
                CefErrorCodes.ERR_CONTENT_DECODING_FAILED
                    => BrowserErrorCategory.ServerError,
                
                // Security errors
                CefErrorCodes.ERR_SSL_PROTOCOL_ERROR or
                CefErrorCodes.ERR_CERT_DATE_INVALID or
                CefErrorCodes.ERR_CERT_AUTHORITY_INVALID or
                CefErrorCodes.ERR_CERT_COMMON_NAME_INVALID or
                CefErrorCodes.ERR_CERT_REVOKED or
                CefErrorCodes.ERR_CERT_INVALID or
                CefErrorCodes.ERR_SSL_HANDSHAKE_NOT_COMPLETED or
                CefErrorCodes.ERR_INSECURE_RESPONSE or
                CefErrorCodes.ERR_BLOCKED_BY_CLIENT or
                CefErrorCodes.ERR_BLOCKED_BY_RESPONSE
                    => BrowserErrorCategory.SecurityError,
                
                // General errors
                _ => BrowserErrorCategory.GeneralError
            };
        }
        
        /// <summary>
        /// Sets display styles by category
        /// </summary>
        private static void SetCategoryStyles(BrowserError error)
        {
            switch (error.Category)
            {
                case BrowserErrorCategory.NetworkError:
                    error.BackgroundColor = "#FF9800"; // Orange (same as ToolsMainPage)
                    error.ForegroundColor = "#303030";
                    error.Icon = "🌐";
                    break;
                    
                case BrowserErrorCategory.ServerError:
                    error.BackgroundColor = "#F44336"; // Red
                    error.ForegroundColor = "#FFFFFF";
                    error.Icon = "🖥️";
                    break;
                    
                case BrowserErrorCategory.SecurityError:
                    error.BackgroundColor = "#B71C1C"; // Dark red
                    error.ForegroundColor = "#FFFFFF";
                    error.Icon = "🔒";
                    break;
                    
                case BrowserErrorCategory.GeneralError:
                default:
                    error.BackgroundColor = "#607D8B"; // Gray
                    error.ForegroundColor = "#FFFFFF";
                    error.Icon = "⚠️";
                    break;
            }
        }
        
        /// <summary>
        /// Sets localized content for CefGlue error
        /// </summary>
        private static void SetLocalizedContent(BrowserError error)
        {
            // Get localized strings via Avalonia resources
            var (title, description, tips) = GetLocalizedStrings(error.ErrorCode);
            
            error.Title = title;
            error.Description = description;
            error.Tips = tips;
            error.ShowSearch = error.ErrorCode == CefErrorCodes.ERR_NAME_NOT_RESOLVED ||
                               error.ErrorCode == CefErrorCodes.ERR_FILE_NOT_FOUND;
        }
        
        /// <summary>
        /// Gets localized strings for the error
        /// </summary>
        private static (string title, string description, string[] tips) GetLocalizedStrings(int errorCode)
        {
            // Try to get from Avalonia resources
            var app = Application.Current;
            
            string GetResource(string key, string defaultValue)
            {
                try
                {
                    if (app?.TryFindResource(key, out var resource) == true && resource is string str)
                        return str;
                }
                catch { }
                return defaultValue;
            }
            
            return errorCode switch
            {
                CefErrorCodes.ERR_NAME_NOT_RESOLVED => (
                    GetResource("BrowserError.NameNotResolved.Title", "Сайт не знайдено"),
                    GetResource("BrowserError.NameNotResolved.Description", 
                        "Не вдалося знайти IP-адресу сервера для цього доменного імені. Можливо, сайт не існує або написано з помилкою."),
                    new[] {
                        GetResource("BrowserError.NameNotResolved.Tip1", "Перевірте правильність написання адреси"),
                        GetResource("BrowserError.NameNotResolved.Tip2", "Переконайтеся, що ви підключені до Інтернету"),
                        GetResource("BrowserError.NameNotResolved.Tip3", "Спробуйте очистити кеш DNS: ipconfig /flushdns")
                    }),
                
                CefErrorCodes.ERR_INTERNET_DISCONNECTED => (
                    GetResource("BrowserError.InternetDisconnected.Title", "Немає підключення до Інтернету"),
                    GetResource("BrowserError.InternetDisconnected.Description",
                        "Ваш пристрій не підключено до Інтернету. Перевірте з'єднання та спробуйте знову."),
                    new[] {
                        GetResource("BrowserError.InternetDisconnected.Tip1", "Перевірте, чи увімкнено Wi-Fi або під'єднано кабель"),
                        GetResource("BrowserError.InternetDisconnected.Tip2", "Перезавантажте роутер або модем"),
                        GetResource("BrowserError.InternetDisconnected.Tip3", "Зверніться до вашого інтернет-провайдера")
                    }),
                
                CefErrorCodes.ERR_CONNECTION_REFUSED => (
                    GetResource("BrowserError.ConnectionRefused.Title", "З'єднання відхилено"),
                    GetResource("BrowserError.ConnectionRefused.Description",
                        "Сервер активно відхилив спробу підключення. Можливо, сервер не запущено або порт закритий."),
                    new[] {
                        GetResource("BrowserError.ConnectionRefused.Tip1", "Переконайтеся, що сервер запущено"),
                        GetResource("BrowserError.ConnectionRefused.Tip2", "Перевірте налаштування брандмауера"),
                        GetResource("BrowserError.ConnectionRefused.Tip3", "Спробуйте пізніше")
                    }),
                
                CefErrorCodes.ERR_CONNECTION_TIMED_OUT => (
                    GetResource("BrowserError.ConnectionTimedOut.Title", "Час очікування вичерпано"),
                    GetResource("BrowserError.ConnectionTimedOut.Description",
                        "Сервер занадто довго не відповідає. Це може бути через повільне з'єднання або перевантаження сервера."),
                    new[] {
                        GetResource("BrowserError.ConnectionTimedOut.Tip1", "Перевірте швидкість вашого Інтернету"),
                        GetResource("BrowserError.ConnectionTimedOut.Tip2", "Спробуйте оновити сторінку"),
                        GetResource("BrowserError.ConnectionTimedOut.Tip3", "Спробуйте пізніше, коли сервер буде менш завантажений")
                    }),
                
                CefErrorCodes.ERR_CONNECTION_RESET => (
                    GetResource("BrowserError.ConnectionReset.Title", "З'єднання перервано"),
                    GetResource("BrowserError.ConnectionReset.Description",
                        "З'єднання з сервером було несподівано перервано. Це може бути тимчасовою проблемою."),
                    new[] {
                        GetResource("BrowserError.ConnectionReset.Tip1", "Оновіть сторінку"),
                        GetResource("BrowserError.ConnectionReset.Tip2", "Перевірте з'єднання з Інтернетом"),
                        GetResource("BrowserError.ConnectionReset.Tip3", "Вимкніть VPN або проксі, якщо використовуєте")
                    }),
                
                CefErrorCodes.ERR_SSL_PROTOCOL_ERROR or
                CefErrorCodes.ERR_CERT_DATE_INVALID or
                CefErrorCodes.ERR_CERT_AUTHORITY_INVALID or
                CefErrorCodes.ERR_CERT_COMMON_NAME_INVALID or
                CefErrorCodes.ERR_CERT_REVOKED or
                CefErrorCodes.ERR_CERT_INVALID => (
                    GetResource("BrowserError.SSLError.Title", "Небезпечне з'єднання"),
                    GetResource("BrowserError.SSLError.Description",
                        "Сертифікат безпеки цього сайту недійсний або прострочений. Ваші дані можуть бути під загрозою."),
                    new[] {
                        GetResource("BrowserError.SSLError.Tip1", "Не вводьте особисті дані на цьому сайті"),
                        GetResource("BrowserError.SSLError.Tip2", "Перевірте дату та час на вашому пристрої"),
                        GetResource("BrowserError.SSLError.Tip3", "Зверніться до адміністратора сайту")
                    }),
                
                CefErrorCodes.ERR_EMPTY_RESPONSE => (
                    GetResource("BrowserError.EmptyResponse.Title", "Порожня відповідь"),
                    GetResource("BrowserError.EmptyResponse.Description",
                        "Сервер не надіслав жодних даних. Можливо, сервер перевантажений або несправний."),
                    new[] {
                        GetResource("BrowserError.EmptyResponse.Tip1", "Спробуйте оновити сторінку"),
                        GetResource("BrowserError.EmptyResponse.Tip2", "Спробуйте пізніше"),
                        GetResource("BrowserError.EmptyResponse.Tip3", "Перевірте статус сервера")
                    }),
                
                CefErrorCodes.ERR_TOO_MANY_REDIRECTS => (
                    GetResource("BrowserError.TooManyRedirects.Title", "Забагато перенаправлень"),
                    GetResource("BrowserError.TooManyRedirects.Description",
                        "Сторінка створила цикл перенаправлень. Сервер неправильно налаштований."),
                    new[] {
                        GetResource("BrowserError.TooManyRedirects.Tip1", "Очистіть cookies для цього сайту"),
                        GetResource("BrowserError.TooManyRedirects.Tip2", "Спробуйте інший браузер"),
                        GetResource("BrowserError.TooManyRedirects.Tip3", "Зверніться до підтримки сайту")
                    }),
                
                CefErrorCodes.ERR_BLOCKED_BY_CLIENT => (
                    GetResource("BrowserError.BlockedByClient.Title", "Заблоковано"),
                    GetResource("BrowserError.BlockedByClient.Description",
                        "Завантаження цього ресурсу заблоковано браузером або розширенням."),
                    new[] {
                        GetResource("BrowserError.BlockedByClient.Tip1", "Перевірте налаштування блокувальника реклами"),
                        GetResource("BrowserError.BlockedByClient.Tip2", "Вимкніть розширення безпеки тимчасово"),
                        GetResource("BrowserError.BlockedByClient.Tip3", "Додайте сайт до списку винятків")
                    }),
                
                CefErrorCodes.ERR_INVALID_URL => (
                    GetResource("BrowserError.InvalidUrl.Title", "Недійсна адреса"),
                    GetResource("BrowserError.InvalidUrl.Description",
                        "Введена адреса має неправильний формат і не може бути оброблена."),
                    new[] {
                        GetResource("BrowserError.InvalidUrl.Tip1", "Перевірте правильність написання URL"),
                        GetResource("BrowserError.InvalidUrl.Tip2", "Переконайтеся, що URL починається з http:// або https://"),
                        GetResource("BrowserError.InvalidUrl.Tip3", "Спробуйте знайти сайт через пошук")
                    }),
                
                CefErrorCodes.ERR_FILE_NOT_FOUND => (
                    GetResource("BrowserError.FileNotFound.Title", "Файл не знайдено"),
                    GetResource("BrowserError.FileNotFound.Description",
                        "Запитаний файл не існує за вказаною адресою."),
                    new[] {
                        GetResource("BrowserError.FileNotFound.Tip1", "Перевірте правильність шляху до файлу"),
                        GetResource("BrowserError.FileNotFound.Tip2", "Переконайтеся, що файл існує"),
                        GetResource("BrowserError.FileNotFound.Tip3", "Спробуйте перейти на головну сторінку сайту")
                    }),
                
                CefErrorCodes.ERR_ACCESS_DENIED => (
                    GetResource("BrowserError.AccessDenied.Title", "Доступ заборонено"),
                    GetResource("BrowserError.AccessDenied.Description",
                        "У вас немає дозволу на перегляд цього ресурсу."),
                    new[] {
                        GetResource("BrowserError.AccessDenied.Tip1", "Увійдіть до свого облікового запису"),
                        GetResource("BrowserError.AccessDenied.Tip2", "Зверніться до адміністратора для отримання доступу"),
                        GetResource("BrowserError.AccessDenied.Tip3", "Перевірте налаштування дозволів")
                    }),
                
                _ => (
                    GetResource("BrowserError.Unknown.Title", "Помилка завантаження"),
                    GetResource("BrowserError.Unknown.Description",
                        $"Не вдалося завантажити сторінку. Код помилки: {errorCode}"),
                    new[] {
                        GetResource("BrowserError.Unknown.Tip1", "Спробуйте оновити сторінку"),
                        GetResource("BrowserError.Unknown.Tip2", "Перевірте з'єднання з Інтернетом"),
                        GetResource("BrowserError.Unknown.Tip3", "Спробуйте пізніше")
                    })
            };
        }
        
        /// <summary>
        /// Sets localized content for HTTP error
        /// </summary>
        private static void SetHttpErrorContent(BrowserError error, int httpStatusCode)
        {
            var app = Application.Current;
            
            string GetResource(string key, string defaultValue)
            {
                try
                {
                    if (app?.TryFindResource(key, out var resource) == true && resource is string str)
                        return str;
                }
                catch { }
                return defaultValue;
            }
            
            var (title, description, tips) = httpStatusCode switch
            {
                400 => (
                    GetResource("BrowserError.Http400.Title", "Поганий запит"),
                    GetResource("BrowserError.Http400.Description", "Сервер не може обробити ваш запит через синтаксичну помилку."),
                    new[] { "Перевірте URL", "Очистіть кеш браузера", "Спробуйте знову" }
                ),
                
                401 => (
                    GetResource("BrowserError.Http401.Title", "Потрібна авторизація"),
                    GetResource("BrowserError.Http401.Description", "Для доступу до цієї сторінки потрібно увійти до облікового запису."),
                    new[] { "Увійдіть до свого облікового запису", "Перевірте правильність логіна та пароля", "Зверніться до підтримки" }
                ),
                
                403 => (
                    GetResource("BrowserError.Http403.Title", "Доступ заборонено"),
                    GetResource("BrowserError.Http403.Description", "У вас немає дозволу на перегляд цієї сторінки."),
                    new[] { "Перевірте свої права доступу", "Зверніться до адміністратора", "Спробуйте інший обліковий запис" }
                ),
                
                404 => (
                    GetResource("BrowserError.Http404.Title", "Сторінку не знайдено"),
                    GetResource("BrowserError.Http404.Description", "Запитана сторінка не існує на цьому сервері. Можливо, її видалено або переміщено."),
                    new[] { "Перевірте правильність URL", "Спробуйте пошук на сайті", "Поверніться на головну сторінку" }
                ),
                
                408 => (
                    GetResource("BrowserError.Http408.Title", "Час запиту вичерпано"),
                    GetResource("BrowserError.Http408.Description", "Сервер закрив з'єднання через тривале очікування."),
                    new[] { "Оновіть сторінку", "Перевірте з'єднання", "Спробуйте пізніше" }
                ),
                
                429 => (
                    GetResource("BrowserError.Http429.Title", "Забагато запитів"),
                    GetResource("BrowserError.Http429.Description", "Ви надіслали занадто багато запитів. Зачекайте та спробуйте знову."),
                    new[] { "Зачекайте кілька хвилин", "Зменшіть частоту запитів", "Зверніться до підтримки" }
                ),
                
                500 => (
                    GetResource("BrowserError.Http500.Title", "Внутрішня помилка сервера"),
                    GetResource("BrowserError.Http500.Description", "На сервері сталася неочікувана помилка. Це проблема сайту, а не вашого з'єднання."),
                    new[] { "Оновіть сторінку", "Спробуйте пізніше", "Зверніться до підтримки сайту" }
                ),
                
                502 => (
                    GetResource("BrowserError.Http502.Title", "Поганий шлюз"),
                    GetResource("BrowserError.Http502.Description", "Проксі-сервер отримав недійсну відповідь від іншого сервера."),
                    new[] { "Оновіть сторінку", "Очистіть кеш", "Спробуйте пізніше" }
                ),
                
                503 => (
                    GetResource("BrowserError.Http503.Title", "Сервіс недоступний"),
                    GetResource("BrowserError.Http503.Description", "Сервер тимчасово недоступний через технічне обслуговування або перевантаження."),
                    new[] { "Спробуйте через кілька хвилин", "Перевірте статус сервісу", "Зверніться до підтримки" }
                ),
                
                504 => (
                    GetResource("BrowserError.Http504.Title", "Час очікування шлюзу"),
                    GetResource("BrowserError.Http504.Description", "Проксі-сервер не отримав відповідь вчасно від іншого сервера."),
                    new[] { "Оновіть сторінку", "Спробуйте пізніше", "Перевірте з'єднання" }
                ),
                
                _ => (
                    GetResource("BrowserError.HttpUnknown.Title", $"Помилка {httpStatusCode}"),
                    GetResource("BrowserError.HttpUnknown.Description", $"Сервер повернув помилку з кодом {httpStatusCode}."),
                    new[] { "Оновіть сторінку", "Спробуйте пізніше", "Зверніться до підтримки" }
                )
            };
            
            error.Title = title;
            error.Description = description;
            error.Tips = tips;
            error.ShowSearch = httpStatusCode == 404;
            
            // HTTP 4xx - client problem (general error)
            // HTTP 5xx - server problem
            if (httpStatusCode >= 500)
            {
                error.Icon = "🖥️";
            }
            else if (httpStatusCode == 404)
            {
                error.Icon = "🔍";
            }
            else
            {
                error.Icon = "⚠️";
            }
        }
        
        /// <summary>
        /// Checks whether to show the error page (some errors are ignored)
        /// </summary>
        public static bool ShouldShowErrorPage(int errorCode)
        {
            // Ignore code 0 - this means successful load, no error
            if (errorCode == 0)
                return false;
            
            // Ignore ERR_ABORTED - this happens when user cancels navigation
            if (errorCode == CefErrorCodes.ERR_ABORTED)
                return false;
            
            // Ignore ERR_CACHE_MISS - this is normal on first load
            if (errorCode == CefErrorCodes.ERR_CACHE_MISS)
                return false;
            
            return true;
        }
    }
}

