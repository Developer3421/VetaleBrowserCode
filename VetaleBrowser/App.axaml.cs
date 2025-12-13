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
                // Check if user has accepted the agreement (returns false on any error)
                bool agreementAccepted = CheckUserAgreementSync();
                
                System.Diagnostics.Trace.WriteLine($"App: Agreement accepted={agreementAccepted}");
                
                if (!agreementAccepted)
                {
                    // Prevent auto-shutdown when agreement window closes
                    desktop.ShutdownMode = Avalonia.Controls.ShutdownMode.OnExplicitShutdown;
                    
                    // Show agreement window first
                    System.Diagnostics.Trace.WriteLine("App: Creating UserAgreementWindow...");
                    var agreementWindow = new UserAgreementWindow();
                    desktop.MainWindow = agreementWindow;
                    // For Avalonia, MainWindow is shown automatically, but we log for debugging
                    System.Diagnostics.Trace.WriteLine("App: UserAgreementWindow set as MainWindow");
                    
                    agreementWindow.Closed += (s, e) =>
                    {
                        System.Diagnostics.Trace.WriteLine($"App: Agreement window closed, IsAccepted={agreementWindow.IsAccepted}");
                        if (agreementWindow.IsAccepted)
                        {
                            // Now show main window on UI thread
                            Avalonia.Threading.Dispatcher.UIThread.Post(async () =>
                            {
                                // Save acceptance FIRST and wait for it
                                try
                                {
                                    await SaveUserAgreementAsync();
                                    System.Diagnostics.Trace.WriteLine("App: Agreement saved successfully before showing MainWindow");
                                }
                                catch (System.Exception ex)
                                {
                                    System.Diagnostics.Trace.WriteLine($"App: Error saving agreement: {ex.Message}");
                                }
                                
                                System.Diagnostics.Trace.WriteLine("App: Creating MainWindow after agreement...");
                                var mainWindow = new MainWindow();
                                desktop.MainWindow = mainWindow;
                                
                                // Switch back to normal shutdown mode
                                desktop.ShutdownMode = Avalonia.Controls.ShutdownMode.OnMainWindowClose;
                                
                                mainWindow.Show();
                                _ = InitializeServicesAsync(desktop, skipDatabaseInit: false);
                            });
                        }
                        else
                        {
                            // User declined - shutdown application
                            System.Diagnostics.Trace.WriteLine("App: User declined agreement, shutting down");
                            desktop.Shutdown();
                        }
                    };
                }
                else
                {
                    // Agreement already accepted - show main window directly
                    desktop.MainWindow = new MainWindow();
                    System.Diagnostics.Trace.WriteLine("App: MainWindow created (deferred initialization follows)");
                    
                    // MEMORY OPTIMIZATION: Initialize heavy services in background AFTER window is shown
                    _ = InitializeServicesAsync(desktop, skipDatabaseInit: false);
                }
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
    /// Check if user has accepted the agreement (sync version for startup)
    /// Uses simple file check for reliability
    /// </summary>
    private bool CheckUserAgreementSync()
    {
        try
        {
            // Use simple file-based check for reliability
            var agreementFilePath = GetAgreementFilePath();
            System.Diagnostics.Trace.WriteLine($"App: Checking agreement file at: {agreementFilePath}");
            
            if (System.IO.File.Exists(agreementFilePath))
            {
                var content = System.IO.File.ReadAllText(agreementFilePath);
                var result = content.Trim() == "accepted";
                System.Diagnostics.Trace.WriteLine($"App: Agreement file exists, content='{content}', result={result}");
                return result;
            }
            
            System.Diagnostics.Trace.WriteLine("App: Agreement file does not exist");
            return false;
        }
        catch (System.Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"App: Failed to check agreement: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Save user agreement acceptance
    /// Uses simple file for reliability
    /// </summary>
    private async Task SaveUserAgreementAsync()
    {
        try
        {
            var agreementFilePath = GetAgreementFilePath();
            System.Diagnostics.Trace.WriteLine($"App: Saving agreement to: {agreementFilePath}");
            
            // Ensure directory exists
            var directory = System.IO.Path.GetDirectoryName(agreementFilePath);
            System.Diagnostics.Trace.WriteLine($"App: Directory path: {directory}");
            
            if (!string.IsNullOrEmpty(directory))
            {
                if (!System.IO.Directory.Exists(directory))
                {
                    System.IO.Directory.CreateDirectory(directory);
                    System.Diagnostics.Trace.WriteLine($"App: Created directory: {directory}");
                }
            }
            
            // Write file synchronously for reliability
            System.IO.File.WriteAllText(agreementFilePath, "accepted");
            System.Diagnostics.Trace.WriteLine($"App: Agreement written to file");
            
            // Verify immediately
            if (System.IO.File.Exists(agreementFilePath))
            {
                var content = System.IO.File.ReadAllText(agreementFilePath);
                System.Diagnostics.Trace.WriteLine($"App: Verified file exists with content: '{content}'");
            }
            else
            {
                System.Diagnostics.Trace.WriteLine($"App: ERROR - File does not exist after writing!");
            }
            
            await Task.CompletedTask; // Make async happy
        }
        catch (System.Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"App: Failed to save agreement: {ex.Message}");
            System.Diagnostics.Trace.WriteLine($"App: Stack trace: {ex.StackTrace}");
            throw;
        }
    }

    /// <summary>
    /// Gets the path to the agreement status file
    /// </summary>
    private static string GetAgreementFilePath()
    {
        var appDataPath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.ApplicationData);
        return System.IO.Path.Combine(appDataPath, "VetaleBrowser", "Data", "user_agreement.txt");
    }

    /// <summary>
    /// STARTUP OPTIMIZATION: Initialize services in background to not block window display
    /// </summary>
    private async Task InitializeServicesAsync(IClassicDesktopStyleApplicationLifetime desktop, bool skipDatabaseInit = false)
    {
        await Task.Yield(); // Ensure window is shown first
        
        // Initialize logging (fast, do first)
        try
        {
            VetaleBrowser.Core.Scripts.Services.ConsoleLogger.Initialize();
            System.Diagnostics.Trace.WriteLine("App: ConsoleLogger initialized");
        }
        catch (System.Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"App: ConsoleLogger failed: {ex.Message}");
        }

        // Initialize database in background (skip if already done)
        if (!skipDatabaseInit)
        {
            await Task.Run(() =>
            {
                try
                {
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
            try { VetaleBrowser.Database.Services.DevToolsDataService.DisposeAll(); } catch { }
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