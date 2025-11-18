using System.Linq;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using VetaleBrowser.VetaleBrowser.Core.Scripts.GlobalManagers;
using VetaleBrowser.VetaleBrowser.UI.Services;

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
            // Initialize logging
            try
            {
                VetaleBrowser.Core.Scripts.Services.ConsoleLogger.Initialize();
                System.Diagnostics.Trace.WriteLine("App: ConsoleLogger initialized successfully");
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"App: Failed to initialize ConsoleLogger: {ex.Message}");
            }

            // Initialize database before reading settings
            try
            {
                DatabaseManager.Initialize();
                System.Diagnostics.Trace.WriteLine("App: Database initialized successfully");
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"App: Failed to initialize database: {ex.Message}");
            }

            // Apply UI language from settings after DB init (async is safe here but not awaited to avoid blocking startup)
            try
            {
                _ = LocalizationService.InitializeFromSettingsAsync();
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"App: Failed to apply localization: {ex.Message}");
            }

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                // ALWAYS create the main window - critical for startup
                desktop.MainWindow = new MainWindow();
                System.Diagnostics.Trace.WriteLine("App: MainWindow created successfully");
                
                // Закриваємо базу даних при виході з додатку
                desktop.ShutdownRequested += (sender, e) =>
                {
                    try
                    {
                        DatabaseManager.Shutdown();
                        System.Diagnostics.Trace.WriteLine("App: Database shutdown successfully");
                    }
                    catch (System.Exception ex)
                    {
                        System.Diagnostics.Trace.WriteLine($"App: Error during database shutdown: {ex.Message}");
                    }

                    try
                    {
                        VetaleBrowser.Database.Services.DevToolsDataService.DisposeAll();
                        System.Diagnostics.Trace.WriteLine("App: DevTools database shutdown successfully");
                    }
                    catch (System.Exception ex)
                    {
                        System.Diagnostics.Trace.WriteLine($"App: Error during DevTools database shutdown: {ex.Message}");
                    }
                };
            }

            // Initialize download folder watcher (monitor OS-level Downloads)
            try
            {
                VetaleBrowser.Core.Scripts.Services.DownloadFolderWatcherService.Initialize();
                var dlPath = VetaleBrowser.Core.Scripts.Services.DownloadFolderWatcherService.MonitoredPath;
                System.Diagnostics.Trace.WriteLine($"App: DownloadFolderWatcher initialized at '{dlPath}'");

                // Auto-open Downloads window on first active download
                VetaleBrowser.Core.Scripts.GlobalManagers.DownloadManager.StatusChanged += (s, item) =>
                {
                    try
                    {
                        if (item.Status == "Downloading" && ApplicationLifetime is IClassicDesktopStyleApplicationLifetime lifetime)
                        {
                            var existing = lifetime.Windows.FirstOrDefault(w => w is VetaleBrowser.UI.Windows.DownloadsWindow);
                            if (existing is VetaleBrowser.UI.Windows.DownloadsWindow dw)
                            {
                                dw.Activate();
                            }
                            else
                            {
                                var wnd = new VetaleBrowser.UI.Windows.DownloadsWindow();
                                wnd.Show();
                            }
                        }
                    }
                    catch { }
                };
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Trace.WriteLine($"App: Failed to init DownloadFolderWatcher: {ex.Message}");
            }

            base.OnFrameworkInitializationCompleted();
        }
        catch (System.Exception ex)
        {
            // Critical error during initialization - log and try to show main window anyway
            System.Diagnostics.Trace.WriteLine($"App: CRITICAL ERROR during initialization: {ex}");
            
            // Last resort - ensure window is created
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow == null)
            {
                try
                {
                    desktop.MainWindow = new MainWindow();
                }
                catch (System.Exception innerEx)
                {
                    System.Diagnostics.Trace.WriteLine($"App: FATAL - Could not create MainWindow: {innerEx}");
                    throw; // Re-throw if we can't even create the window
                }
            }
            
            base.OnFrameworkInitializationCompleted();
        }
    }
}