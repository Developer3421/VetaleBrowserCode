using System;
using System.Threading.Tasks;

namespace VetaleBrowser.VetaleBrowser.Database.Services;

/// <summary>
/// Interface for working with browser settings
/// </summary>
public interface ISettingsService
{
    /// <summary>
    /// Gets the search engine URL
    /// </summary>
    Task<string> GetSearchEngineUrlAsync();
    
    /// <summary>
    /// Sets the search engine URL
    /// </summary>
    Task SetSearchEngineUrlAsync(string url);
    
    /// <summary>
    /// Gets the search engine name
    /// </summary>
    Task<string> GetSearchEngineNameAsync();
    
    /// <summary>
    /// Sets the search engine name
    /// </summary>
    Task SetSearchEngineNameAsync(string name);
    
    /// <summary>
    /// Sets the search engine (name + URL)
    /// </summary>
    Task SetSearchEngineAsync(string name, string url);

    /// <summary>
    /// Gets the UI language code (e.g. "en", "uk", "de", "ru")
    /// </summary>
    Task<string> GetLanguageAsync();

    /// <summary>
    /// Sets the UI language code
    /// </summary>
    Task SetLanguageAsync(string code);

    /// <summary>
    /// Checks whether the user has accepted the agreement (GDPR/Privacy)
    /// </summary>
    Task<bool> IsUserAgreementAcceptedAsync();

    /// <summary>
    /// Saves the agreement acceptance status
    /// </summary>
    Task SetUserAgreementAcceptedAsync(bool accepted);

    /// <summary>
    /// Gets the date when the agreement was accepted
    /// </summary>
    Task<DateTime?> GetUserAgreementDateAsync();
}
