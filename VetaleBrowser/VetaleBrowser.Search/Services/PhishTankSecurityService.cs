using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using VetaleBrowser.VetaleBrowser.Search.Models;

namespace VetaleBrowser.VetaleBrowser.Search.Services;

/// <summary>
/// URL check service without API key (public database + local check)
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
            // Some known phishing domains (examples)
            "paypal-secure-login.com",
            "apple-account-verify.com",
            "microsoft-account-security.com",
            "bank-secure-login.com",
            "amazon-account-verify.com"
        };
        _virusTotal = new VirusTotalSecurityService();
    }
    
    /// <summary>
    /// Check URL for phishing
    /// </summary>
    public async Task<SecurityCheckResult> CheckUrlAsync(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return new SecurityCheckResult
            {
                Url = url,
                Status = SecurityStatus.Unknown,
                Description = "Empty URL"
            };
        }
        
        // URL normalization
        var normalizedUrl = NormalizeUrl(url);
        
        // Cache check
        if (_cache.TryGetValue(normalizedUrl, out var cachedResult))
        {
            var cacheAge = DateTime.Now - cachedResult.CheckedAt;
            if (cacheAge < _cacheExpiration)
            {
                return cachedResult;
            }
            _cache.Remove(normalizedUrl);
        }
        
        // Perform check
        var result = await PerformSecurityCheckAsync(normalizedUrl);
        
        // Save to cache
        _cache[normalizedUrl] = result;
        
        return result;
    }
    
    /// <summary>
    /// Check URL via public PhishTank API without a key
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
    /// Perform security check (multi-level)
    /// </summary>
    private async Task<SecurityCheckResult> PerformSecurityCheckAsync(string url)
    {
        try
        {
            // Level 1: local URLs
            if (IsLocalUrl(url))
            {
                return new SecurityCheckResult
                {
                    Url = url,
                    Status = SecurityStatus.Safe,
                    Source = SecuritySource.LocalList,
                    Description = "Local resource",
                    IsPhishing = false
                };
            }

            // Level 2: local database
            var localCheckResult = CheckLocalDatabase(url);
            if (localCheckResult != null)
            {
                localCheckResult.Source = SecuritySource.LocalList;
                return localCheckResult;
            }

            // Level 3: heuristics
            var heuristicResult = PerformHeuristicCheck(url);
            if (heuristicResult != null)
            {
                heuristicResult.Source = SecuritySource.Heuristic;
                return heuristicResult;
            }

            // Level 4: PhishTank public API without key
            var isPhishing = await IsPhishingAsync(url);
            if (isPhishing)
            {
                return new SecurityCheckResult
                {
                    Url = url,
                    Status = SecurityStatus.Dangerous,
                    Source = SecuritySource.LocalList,
                    Description = "⚠️ Domain found in PhishTank database (public API)",
                    IsPhishing = true
                };
            }

            // Level 5: VirusTotal (if key is available)
            var vtDetails = await _virusTotal.CheckUrlAsync(url);
            if (vtDetails != null)
            {
                // If VT detects a threat
                if (vtDetails.Malicious > 0 || vtDetails.Suspicious > 0)
                {
                    return new SecurityCheckResult
                    {
                        Url = url,
                        Status = SecurityStatus.Dangerous,
                        Source = SecuritySource.VirusTotal,
                        Description = vtDetails.RawLabel ?? "⚠️ Malicious activity detected according to VirusTotal",
                        IsPhishing = true,
                        VirusTotalDetails = vtDetails
                    };
                }

                // VT says everything is ok or unknown
                return new SecurityCheckResult
                {
                    Url = url,
                    Status = SecurityStatus.Safe,
                    Source = SecuritySource.VirusTotal,
                    Description = vtDetails.RawLabel ?? "Safe according to VirusTotal",
                    IsPhishing = false,
                    VirusTotalDetails = vtDetails
                };
            }

            // If all checks passed - consider safe
            return new SecurityCheckResult
            {
                Url = url,
                Status = SecurityStatus.Safe,
                Source = SecuritySource.Heuristic,
                Description = "Checked locally and via PhishTank: no suspicious signs found",
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
                Description = "Request timed out",
                IsPhishing = false
            };
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Security] Check error: {ex.Message}");

            return new SecurityCheckResult
            {
                Url = url,
                Status = SecurityStatus.Safe,
                Source = SecuritySource.Unknown,
                Description = "Check error (safety not confirmed)",
                IsPhishing = false
            };
        }
    }
    
    /// <summary>
    /// Check against local database of known phishing domains
    /// </summary>
    private SecurityCheckResult? CheckLocalDatabase(string url)
    {
        try
        {
            var uri = new Uri(url);
            var domain = uri.Host.ToLower();
            
            // Check in local database
            if (_knownPhishingDomains.Contains(domain))
            {
                return new SecurityCheckResult
                {
                    Url = url,
                    Status = SecurityStatus.Dangerous,
                    Description = "⚠️ WARNING! Known phishing domain",
                    IsPhishing = true
                };
            }
            
            // Check suspicious domain spoofing
            if (IsSuspiciousDomain(domain))
            {
                return new SecurityCheckResult
                {
                    Url = url,
                    Status = SecurityStatus.Dangerous,
                    Description = "⚠️ WARNING! Suspicious imitation of a known domain",
                    IsPhishing = true
                };
            }
        }
        catch
        {
            // Cannot parse URL
        }
        
        return null;
    }
    
    /// <summary>
    /// Heuristic check (analysis of suspicious signs)
    /// </summary>
    private SecurityCheckResult? PerformHeuristicCheck(string url)
    {
        try
        {
            var uri = new Uri(url);
            var domain = uri.Host.ToLower();
            var suspicionScore = 0;
            var reasons = new List<string>();
            
            // 1. Excessively long domain
            if (domain.Length > 50)
            {
                suspicionScore += 2;
                reasons.Add("Excessively long domain");
            }
            
            // 2. Many hyphens
            if (domain.Count(c => c == '-') > 3)
            {
                suspicionScore += 2;
                reasons.Add("Many hyphens in domain");
            }
            
            // 3. Using IP address instead of domain
            if (System.Net.IPAddress.TryParse(domain, out _))
            {
                suspicionScore += 3;
                reasons.Add("IP address instead of domain");
            }
            
            // 4. Suspicious keywords
            string[] suspiciousKeywords = { "verify", "secure", "account", "login", "update", "confirm", "banking" };
            int keywordCount = suspiciousKeywords.Count(kw => domain.Contains(kw));
            if (keywordCount >= 2)
            {
                suspicionScore += keywordCount;
                reasons.Add($"Suspicious keywords ({keywordCount})");
            }
            
            // 5. Non-standard ports
            if (uri.Port != 80 && uri.Port != 443 && !uri.IsDefaultPort)
            {
                suspicionScore += 1;
                reasons.Add("Non-standard port");
            }
            
            // 6. HTTP instead of HTTPS for "secure" services
            if (uri.Scheme == "http" && (domain.Contains("bank") || domain.Contains("pay") || domain.Contains("secure")))
            {
                suspicionScore += 3;
                reasons.Add("HTTP for financial service");
            }
            
            // Evaluate suspicion level
            if (suspicionScore >= 5)
            {
                return new SecurityCheckResult
                {
                    Url = url,
                    Status = SecurityStatus.Dangerous,
                    Description = $"⚠️ Suspicious site! Signs: {string.Join(", ", reasons)}",
                    IsPhishing = true
                };
            }
            else if (suspicionScore >= 3)
            {
                return new SecurityCheckResult
                {
                    Url = url,
                    Status = SecurityStatus.Error,
                    Description = $"⚠ Be careful! Possible phishing signs: {string.Join(", ", reasons)}",
                    IsPhishing = false
                };
            }
        }
        catch
        {
            // Analysis error
        }
        
        return null;
    }
    

    /// <summary>
    /// Check whether domain is a suspicious imitation
    /// </summary>
    private bool IsSuspiciousDomain(string domain)
    {
        // Known brands for spoofing check
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
            // If domain contains brand name but is not the official domain
            if (domain.Contains(brand.Key) && !brand.Value.Any(official => domain == official || domain.EndsWith("." + official)))
            {
                return true;
            }
        }
        
        // Check for homograph attacks (similar characters)
        if (ContainsHomographCharacters(domain))
        {
            return true;
        }
        
        return false;
    }
    
    /// <summary>
    /// Check for presence of homograph characters (substitution of similar letters)
    /// </summary>
    private bool ContainsHomographCharacters(string domain)
    {
        // Cyrillic letters similar to Latin ones
        char[] cyrillicLookalikes = { 'а', 'е', 'і', 'о', 'р', 'с', 'х', 'у' }; // a, e, i, o, p, c, x, y
        
        return domain.Any(c => cyrillicLookalikes.Contains(c));
    }
    
    /// <summary>
    /// Normalize URL for check
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
    /// Check if URL is local
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
    /// Clear cache
    /// </summary>
    public void ClearCache()
    {
        _cache.Clear();
    }
    
    /// <summary>
    /// Add domain to local phishing sites database
    /// </summary>
    public void AddPhishingDomain(string domain)
    {
        _knownPhishingDomains.Add(domain.ToLower());
    }
    
    /// <summary>
    /// Remove domain from local database
    /// </summary>
    public void RemovePhishingDomain(string domain)
    {
        _knownPhishingDomains.Remove(domain.ToLower());
    }
}
