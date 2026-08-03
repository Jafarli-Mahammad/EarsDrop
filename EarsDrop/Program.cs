global using System;
global using System.Linq;
global using System.Threading;
global using System.Threading.Tasks;

using Avalonia;

namespace EarsDrop;

internal sealed class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        // Headless backend verification mode (useful for CI/headless environments):
        // Run with: dotnet run -- --verify-backend
        if (args != null && args.Any(a => string.Equals(a, "--verify-backend", StringComparison.OrdinalIgnoreCase)))
        {
            var ok = BackendVerification.RunVerificationAsync().GetAwaiter().GetResult();
            Environment.ExitCode = ok ? 0 : 1;
            return;
        }

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            /*.WithDeveloperTools()*/
#endif
            .WithInterFont()
            .LogToTrace();
}
