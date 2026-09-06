using System;
using System.IO;

namespace VetaleBrowser.VetaleBrowser.Database.Services;

/// <summary>
/// One-time migration of settings/data from Vetale Browser Lite.
/// Lite uses %AppData%\VetaleBrowserLite\Data (+ LocalAppData cef dirs),
/// full version uses %AppData%\VetaleBrowser\Data.
/// Copies files only if the target is missing, so user data is never overwritten.
/// Call BEFORE DatabaseServicesFactory/DatabaseManager initialization.
/// </summary>
public static class LiteSettingsMigrator
{
    private static readonly string[] DataFilesToMigrate =
    {
        "browser.db",
        "appearance_settings.db",
        "api_keys.db",
        "user_agreement.txt"
    };

    public static void MigrateIfNeeded()
    {
        try
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var liteDir = Path.Combine(appData, "VetaleBrowserLite", "Data");
            var fullDir = Path.Combine(appData, "VetaleBrowser", "Data");

            if (!Directory.Exists(liteDir))
                return;

            Directory.CreateDirectory(fullDir);

            foreach (var file in DataFilesToMigrate)
            {
                try
                {
                    var src = Path.Combine(liteDir, file);
                    var dst = Path.Combine(fullDir, file);
                    // Copy only when target is missing — never overwrite full-version settings.
                    // This also preserves Google/search-engine choice from Lite
                    // in case the full version was reset to defaults at startup.
                    if (File.Exists(src) && !File.Exists(dst))
                    {
                        File.Copy(src, dst);
                        System.Diagnostics.Debug.WriteLine($"[LiteMigrator] Copied {file} from Lite.");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[LiteMigrator] File copy failed: {ex.Message}");
                }
            }

            // Also copy user agreement flag if stored one level up (legacy layouts)
            TryCopyLegacyAgreement(appData, fullDir);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LiteMigrator] Migration failed: {ex.Message}");
        }
    }

    private static void TryCopyLegacyAgreement(string appData, string fullDir)
    {
        try
        {
            var liteLegacy = Path.Combine(appData, "VetaleBrowserLite", "user_agreement.txt");
            var fullTarget = Path.Combine(fullDir, "user_agreement.txt");
            if (File.Exists(liteLegacy) && !File.Exists(fullTarget))
                File.Copy(liteLegacy, fullTarget);
        }
        catch { }
    }

    /// <summary>
    /// Removes stale language prefs (intl.accept_languages / selected_languages,
    /// spellcheck dictionaries) from the persisted Chromium profile.
    /// They were once written from a hardcoded value and, via
    /// PersistUserPreferences, override everything on every launch.
    /// After removal Chromium falls back to the OS language.
    /// Call BEFORE any WebView is created (profile must not be locked).
    /// </summary>
    public static void ResetPersistedChromiumLanguage()
    {
        try
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var prefsPath = Path.Combine(localAppData, "VetaleBrowser", "cef_data", "Default", "Preferences");
            if (!File.Exists(prefsPath))
                return;

            string json;
            try
            {
                // Fail if another instance holds the profile (do not corrupt it).
                using var fs = new FileStream(prefsPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                using var sr = new StreamReader(fs);
                json = sr.ReadToEnd();
            }
            catch
            {
                System.Diagnostics.Debug.WriteLine("[LiteMigrator] Profile is locked, language reset skipped.");
                return;
            }

            System.Text.Json.Nodes.JsonNode? root;
            try
            {
                root = System.Text.Json.Nodes.JsonNode.Parse(json);
            }
            catch
            {
                return;
            }
            if (root == null)
                return;

            bool changed = false;

            var intl = root["intl"] as System.Text.Json.Nodes.JsonObject;
            if (intl != null)
            {
                changed |= intl.Remove("accept_languages");
                changed |= intl.Remove("selected_languages");
            }

            var spell = root["spellcheck"] as System.Text.Json.Nodes.JsonObject;
            if (spell != null)
            {
                changed |= spell.Remove("dictionaries");
                changed |= spell.Remove("dictionary");
            }

            if (!changed)
                return;

            // Backup before rewrite; CEF recreates the file if it ever becomes invalid.
            try { File.Copy(prefsPath, prefsPath + ".bak", overwrite: true); } catch { }
            File.WriteAllText(prefsPath, root.ToJsonString());
            System.Diagnostics.Debug.WriteLine("[LiteMigrator] Stale Chromium language prefs removed.");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LiteMigrator] Language reset failed: {ex.Message}");
        }
    }
}
