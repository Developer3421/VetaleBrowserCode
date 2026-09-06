using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using VetaleBrowser.VetaleBrowser.Core.Scripts.GlobalManagers;
using VetaleBrowser.VetaleBrowser.UI.Services;
using VetaleBrowser.VetaleBrowser.UI.Windows;
using VetaleBrowser.VetaleBrowser.Database.Services;

namespace VetaleBrowser;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        try
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                // User agreement is available for review from Tools -> User Agreement.
                // No blocking agreement window is shown on startup.
                desktop.MainWindow = new MainWindow();
                System.Diagnostics.Trace.WriteLine("App: MainWindow created (deferred initialization follows)");
                
                // MEMORY OPTIMIZATION: Initialize heavy services in background AFTER window is shown
                _ = InitializeServicesAsync(desktop, skipDatabaseInit: false);
            }

            base.OnFrameworkInitializationCompleted();
        }
        catch (System.Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"App: CRITICAL ERROR during initialization: {ex}");
            
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow == null)
            {
                try
                {
                    // Fallback - just show main window without agreement
                    desktop.MainWindow = new MainWindow();
                }
                catch (System.Exception innerEx)
                {
                    System.Diagnostics.Trace.WriteLine($"App: FATAL - Could not create MainWindow: {innerEx}");
                    throw;
                }
            }
            
            base.OnFrameworkInitializationCompleted();
        }
    }

    /// <summary>
    /// STARTUP OPTIMIZATION: Initialize services in background to not block window display
    /// </summary>
    private async Task InitializeServicesAsync(IClassicDesktopStyleApplicationLifetime desktop, bool skipDatabaseInit = false)
    {
        await Task.Yield(); // Ensure window is shown first

        // Initialize database in background (skip if already done)
        if (!skipDatabaseInit)
        {
            await Task.Run(() =>
            {
                try
                {
                    // Copy settings from Vetale Browser Lite first (missing files only)
                    VetaleBrowser.Database.Services.LiteSettingsMigrator.MigrateIfNeeded();
                    DatabaseManager.Initialize();
                    System.Diagnostics.Trace.WriteLine("App: Database initialized");
                }
                catch (System.Exception ex)
                {
                    System.Diagnostics.Trace.WriteLine($"App: Database init failed: {ex.Message}");
                }
            });
        }

        // Apply localization after DB - MUST await to ensure it completes
        try
        {
            await LocalizationService.InitializeFromSettingsAsync();
            System.Diagnostics.Trace.WriteLine("App: Localization initialized");
        }
        catch (System.Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"App: Localization failed: {ex.Message}");
        }
        
        // Setup shutdown handler
        desktop.ShutdownRequested += (_, _) =>
        {
            try { DatabaseManager.Shutdown(); } catch { }
            try { VetaleBrowser.Database.Services.DatabaseServiceManager.Shutdown(); } catch { }
        };

        // Initialize download watcher (can be deferred)
        await Task.Delay(500); // Wait a bit before starting background services
        try
        {
            VetaleBrowser.Core.Scripts.Services.DownloadFolderWatcherService.Initialize();
            System.Diagnostics.Trace.WriteLine("App: DownloadFolderWatcher initialized");

            VetaleBrowser.Core.Scripts.GlobalManagers.DownloadManager.StatusChanged += (s, item) =>
            {
                try
                {
                    if (item.Status == "Downloading" && ApplicationLifetime is IClassicDesktopStyleApplicationLifetime lifetime)
                    {
                        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                        {
                            var existing = lifetime.Windows.FirstOrDefault(w => w is VetaleBrowser.UI.Windows.DownloadsWindow);
                            if (existing is VetaleBrowser.UI.Windows.DownloadsWindow dw)
                                dw.Activate();
                            else
                                new VetaleBrowser.UI.Windows.DownloadsWindow().Show();
                        });
                    }
                }
                catch { }
            };
        }
        catch (System.Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"App: DownloadFolderWatcher failed: {ex.Message}");
        }
    }
}