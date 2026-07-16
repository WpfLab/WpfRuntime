using System.Windows;
using System.Windows.Threading;

namespace PackageTestApp;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        base.OnStartup(e);
    }

    private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Console.Error.WriteLine(e.Exception);
        Environment.ExitCode = 1;
        e.Handled = true;
        Current.Shutdown(1);
    }
}
