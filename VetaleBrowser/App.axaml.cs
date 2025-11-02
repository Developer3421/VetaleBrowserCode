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

    public override async void OnFrameworkInitializationCompleted()
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

        // Apply UI language from settings after DB init
        try
        {
            await LocalizationService.InitializeFromSettingsAsync();
        }
        catch (System.Exception ex)
        {
            System.Diagnostics.Trace.WriteLine($"App: Failed to apply localization: {ex.Message}");
        }

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow();
            
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
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}