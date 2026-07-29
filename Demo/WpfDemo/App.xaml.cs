using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace WpfDemo;

public partial class App : Application
{
    internal static bool VerifyAndExit { get; private set; }
    internal static string VerificationReportPath => Path.Join(AppContext.BaseDirectory, "WpfDemo.repo-wpf-validation.txt");

    protected override void OnStartup(StartupEventArgs e)
    {
        VerifyAndExit = e.Args.Contains("--verify-repo-wpf", StringComparer.OrdinalIgnoreCase);
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        if (VerifyAndExit)
        {
            File.Delete(VerificationReportPath);
        }
        base.OnStartup(e);
    }

    private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Console.Error.WriteLine(e.Exception);
        Environment.ExitCode = 1;
        e.Handled = true;
        Current.Shutdown(1);
    }

    private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        Console.Error.WriteLine(e.ExceptionObject);
        Environment.ExitCode = 1;
    }
}

