using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using VetaleBrowser.VetaleBrowser.Search.Models;

namespace VetaleBrowser.VetaleBrowser.Search.Services;

/// <summary>
/// Сервіс перевірки URL без API ключа (публічна база + локальна перевірка)
/// </summary>
public class PhishTankSecurityService : ISecurityCheckService
{
    private readonly HttpClient _httpClient;
    private readonly Dictionary<string, SecurityCheckResult> _cache;
    private readonly HashSet<string> _knownPhishingDomains;
    private readonly TimeSpan _cacheExpiration = TimeSpan.FromMinutes(30);
    private readonly VirusTotalSecurityService _virusTotal;

    public PhishTankSecurityService()
    {
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(10)
        };
        _cache = new Dictionary<string, SecurityCheckResult>();
        _knownPhishingDomains = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            // Деякі відомі фішингові домени (приклади)
            "paypal-secure-login.com",
            "apple-account-verify.com",
            "microsoft-account-security.com",
            "bank-secure-login.com",
            "amazon-account-verify.com"
        };
        _virusTotal = new VirusTotalSecurityService();
    }
    
    /// <summary>
    /// Перевірити URL на фішинг
    /// </summary>
    public async Task<SecurityCheckResult> CheckUrlAsync(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return new SecurityCheckResult
            {
                Url = url,
                Status = SecurityStatus.Unknown,
                Description = "Порожній URL"
            };
        }
        
        // Нормалізація URL
        var normalizedUrl = NormalizeUrl(url);
        
        // Перевірка кешу
        if (_cache.TryGetValue(normalizedUrl, out var cachedResult))
        {
            var cacheAge = DateTime.Now - cachedResult.CheckedAt;
            if (cacheAge < _cacheExpiration)
            {
                return cachedResult;
            }
            _cache.Remove(normalizedUrl);
        }
        
        // Виконання перевірки
        var result = await PerformSecurityCheckAsync(normalizedUrl);
        
        // Збереження в кеш
        _cache[normalizedUrl] = result;
        
        return result;
    }
    
    /// <summary>
    /// Перевірка URL через публічний PhishTank API без ключа
    /// </summary>
    private async Task<bool> IsPhishingAsync(string url)
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };

            using var form = new MultipartFormDataContent
            {
                { new StringContent(url), "url" },
                { new StringContent("json"), "format" }
            };

            using var response = await http.PostAsync("https://checkurl.phishtank.com/checkurl/", form);
            if (!response.IsSuccessStatusCode)
                return false;

            var json = await response.Content.ReadFromJsonAsync<PhishTankApiResponse>();

            return json?.Results?.InDatabase == true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PhishTank] Error calling public API: {ex.Message}");
            return false;
        }
    }

    private class PhishTankApiResponse
    {
        public PhishTankApiResult? Results { get; set; }
    }

    private class PhishTankApiResult
    {
        public bool InDatabase { get; set; }
    }

    /// <summary>
    /// Виконати перевірку безпеки (багаторівнева)
    /// </summary>
    private async Task<SecurityCheckResult> PerformSecurityCheckAsync(string url)
    {
        try
        {
            // Рівень 1: локальні URL
            if (IsLocalUrl(url))
            {
                return new SecurityCheckResult
                {
                    Url = url,
                    Status = SecurityStatus.Safe,
                    Source = SecuritySource.LocalList,
                    Description = "Локальний ресурс",
                    IsPhishing = false
                };
            }

            // Рівень 2: локальна база
            var localCheckResult = CheckLocalDatabase(url);
            if (localCheckResult != null)
            {
                localCheckResult.Source = SecuritySource.LocalList;
                return localCheckResult;
            }

            // Рівень 3: евристика
            var heuristicResult = PerformHeuristicCheck(url);
            if (heuristicResult != null)
            {
                heuristicResult.Source = SecuritySource.Heuristic;
                return heuristicResult;
            }

            // Рівень 4: PhishTank public API без ключа
            var isPhishing = await IsPhishingAsync(url);
            if (isPhishing)
            {
                return new SecurityCheckResult
                {
                    Url = url,
                    Status = SecurityStatus.Dangerous,
                    Source = SecuritySource.LocalList,
                    Description = "⚠️ Домен знайдено в базі PhishTank (public API)",
                    IsPhishing = true
                };
            }

            // Рівень 5: VirusTotal (якщо є ключ)
            var vtDetails = await _virusTotal.CheckUrlAsync(url);
            if (vtDetails != null)
            {
                // Якщо VT виявляє загрозу
                if (vtDetails.Malicious > 0 || vtDetails.Suspicious > 0)
                {
                    return new SecurityCheckResult
                    {
                        Url = url,
                        Status = SecurityStatus.Dangerous,
                        Source = SecuritySource.VirusTotal,
                        Description = vtDetails.RawLabel ?? "⚠️ Виявлено шкідливу активність за даними VirusTotal",
                        IsPhishing = true,
                        VirusTotalDetails = vtDetails
                    };
                }

                // VT каже, що все ок або невідомо
                return new SecurityCheckResult
                {
                    Url = url,
                    Status = SecurityStatus.Safe,
                    Source = SecuritySource.VirusTotal,
                    Description = vtDetails.RawLabel ?? "Безпечний за даними VirusTotal",
                    IsPhishing = false,
                    VirusTotalDetails = vtDetails
                };
            }

            // Якщо всі перевірки пройшли - вважаємо безпечним
            return new SecurityCheckResult
            {
                Url = url,
                Status = SecurityStatus.Safe,
                Source = SecuritySource.Heuristic,
                Description = "Перевірено локально та через PhishTank: підозрілих ознак не виявлено",
                IsPhishing = false
            };
        }
        catch (TaskCanceledException)
        {
            return new SecurityCheckResult
            {
                Url = url,
                Status = SecurityStatus.Error,
                Source = SecuritySource.Unknown,
                Description = "Час очікування вичерпано",
                IsPhishing = false
            };
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Security] Помилка перевірки: {ex.Message}");

            return new SecurityCheckResult
            {
                Url = url,
                Status = SecurityStatus.Safe,
                Source = SecuritySource.Unknown,
                Description = "Помилка перевірки (безпечність не підтверджена)",
                IsPhishing = false
            };
        }
    }
    
    /// <summary>
    /// Перевірка по локальній базі відомих фішингових доменів
    /// </summary>
    private SecurityCheckResult? CheckLocalDatabase(string url)
    {
        try
        {
            var uri = new Uri(url);
            var domain = uri.Host.ToLower();
            
            // Перевірка в локальній базі
            if (_knownPhishingDomains.Contains(domain))
            {
                return new SecurityCheckResult
                {
                    Url = url,
                    Status = SecurityStatus.Dangerous,
                    Description = "⚠️ УВАГА! Відомий фішинговий домен",
                    IsPhishing = true
                };
            }
            
            // Перевірка підозрілих підменів доменів
            if (IsSuspiciousDomain(domain))
            {
                return new SecurityCheckResult
                {
                    Url = url,
                    Status = SecurityStatus.Dangerous,
                    Description = "⚠️ УВАГА! Підозріла імітація відомого домену",
                    IsPhishing = true
                };
            }
        }
        catch
        {
            // Неможливо розпарсити URL
        }
        
        return null;
    }
    
    /// <summary>
    /// Евристична перевірка (аналіз підозрілих ознак)
    /// </summary>
    private SecurityCheckResult? PerformHeuristicCheck(string url)
    {
        try
        {
            var uri = new Uri(url);
            var domain = uri.Host.ToLower();
            var suspicionScore = 0;
            var reasons = new List<string>();
            
            // 1. Надмірно довгий домен
            if (domain.Length > 50)
            {
                suspicionScore += 2;
                reasons.Add("Надмірно довгий домен");
            }
            
            // 2. Багато дефісів
            if (domain.Count(c => c == '-') > 3)
            {
                suspicionScore += 2;
                reasons.Add("Багато дефісів в домені");
            }
            
            // 3. Використання IP адреси замість домену
            if (System.Net.IPAddress.TryParse(domain, out _))
            {
                suspicionScore += 3;
                reasons.Add("IP адреса замість домену");
            }
            
            // 4. Підозрілі ключові слова
            string[] suspiciousKeywords = { "verify", "secure", "account", "login", "update", "confirm", "banking" };
            int keywordCount = suspiciousKeywords.Count(kw => domain.Contains(kw));
            if (keywordCount >= 2)
            {
                suspicionScore += keywordCount;
                reasons.Add($"Підозрілі ключові слова ({keywordCount})");
            }
            
            // 5. Нестандартні порти
            if (uri.Port != 80 && uri.Port != 443 && !uri.IsDefaultPort)
            {
                suspicionScore += 1;
                reasons.Add("Нестандартний порт");
            }
            
            // 6. HTTP замість HTTPS для "безпечних" сервісів
            if (uri.Scheme == "http" && (domain.Contains("bank") || domain.Contains("pay") || domain.Contains("secure")))
            {
                suspicionScore += 3;
                reasons.Add("HTTP для фінансового сервісу");
            }
            
            // Оцінка рівня підозрілості
            if (suspicionScore >= 5)
            {
                return new SecurityCheckResult
                {
                    Url = url,
                    Status = SecurityStatus.Dangerous,
                    Description = $"⚠️ Підозрілий сайт! Ознаки: {string.Join(", ", reasons)}",
                    IsPhishing = true
                };
            }
            else if (suspicionScore >= 3)
            {
                return new SecurityCheckResult
                {
                    Url = url,
                    Status = SecurityStatus.Error,
                    Description = $"⚠ Будьте обережні! Можливі ознаки фішингу: {string.Join(", ", reasons)}",
                    IsPhishing = false
                };
            }
        }
        catch
        {
            // Помилка аналізу
        }
        
        return null;
    }
    

    /// <summary>
    /// Перевірка чи є домен підозрілою імітацією
    /// </summary>
    private bool IsSuspiciousDomain(string domain)
    {
        // Відомі бренди для перевірки підміни
        var trustedBrands = new Dictionary<string, string[]>
        {
            { "google", new[] { "google.com", "google.ua" } },
            { "facebook", new[] { "facebook.com", "fb.com" } },
            { "paypal", new[] { "paypal.com" } },
            { "amazon", new[] { "amazon.com" } },
            { "apple", new[] { "apple.com", "icloud.com" } },
            { "microsoft", new[] { "microsoft.com", "live.com", "outlook.com" } },
            { "netflix", new[] { "netflix.com" } },
            { "instagram", new[] { "instagram.com" } },
            { "twitter", new[] { "twitter.com", "x.com" } }
        };
        
        foreach (var brand in trustedBrands)
        {
            // Якщо домен містить назву бренду, але це не офіційний домен
            if (domain.Contains(brand.Key) && !brand.Value.Any(official => domain == official || domain.EndsWith("." + official)))
            {
                return true;
            }
        }
        
        // Перевірка homograph атак (схожі символи)
        if (ContainsHomographCharacters(domain))
        {
            return true;
        }
        
        return false;
    }
    
    /// <summary>
    /// Перевірка наявності homograph символів (підміна схожих літер)
    /// </summary>
    private bool ContainsHomographCharacters(string domain)
    {
        // Кирилічні літери, схожі на латинські
        char[] cyrillicLookalikes = { 'а', 'е', 'і', 'о', 'р', 'с', 'х', 'у' }; // a, e, i, o, p, c, x, y
        
        return domain.Any(c => cyrillicLookalikes.Contains(c));
    }
    
    /// <summary>
    /// Нормалізувати URL для перевірки
    /// </summary>
    private string NormalizeUrl(string url)
    {
        try
        {
            if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && 
                !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                url = "https://" + url;
            }
            
            var uri = new Uri(url);
            return uri.GetLeftPart(UriPartial.Authority) + uri.AbsolutePath;
        }
        catch
        {
            return url;
        }
    }
    
    /// <summary>
    /// Перевірити чи є URL локальним
    /// </summary>
    private bool IsLocalUrl(string url)
    {
        return url.StartsWith("file://", StringComparison.OrdinalIgnoreCase) ||
               url.StartsWith("about:", StringComparison.OrdinalIgnoreCase) ||
               url.StartsWith("vetale://", StringComparison.OrdinalIgnoreCase) ||
               url.Contains("localhost") ||
               url.Contains("127.0.0.1") ||
               url.Contains("::1");
    }
    
    /// <summary>
    /// Очистити кеш
    /// </summary>
    public void ClearCache()
    {
        _cache.Clear();
    }
    
    /// <summary>
    /// Додати домен до локальної бази фішингових сайтів
    /// </summary>
    public void AddPhishingDomain(string domain)
    {
        _knownPhishingDomains.Add(domain.ToLower());
    }
    
    /// <summary>
    /// Видалити домен з локальної бази
    /// </summary>
    public void RemovePhishingDomain(string domain)
    {
        _knownPhishingDomains.Remove(domain.ToLower());
    }
}
