using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using VetaleBrowser.VetaleBrowser.Core.Scripts.GlobalManagers;

namespace VetaleBrowser;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // Ініціалізуємо базу даних при запуску додатку
        try
        {
            DatabaseManager.Initialize();
            System.Diagnostics.Debug.WriteLine("App: Database initialized successfully");
        }
        catch (System.Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"App: Failed to initialize database: {ex.Message}");
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
                    System.Diagnostics.Debug.WriteLine("App: Database shutdown successfully");
                }
                catch (System.Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"App: Error during database shutdown: {ex.Message}");
                }
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}