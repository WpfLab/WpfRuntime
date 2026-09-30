using System;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Interop;
using Forms = System.Windows.Forms;

internal static class Program
{
    [STAThread]
    private static int Main()
    {
        var desktopDirectory = Path.GetDirectoryName(typeof(Forms.Form).Assembly.Location)!;
        var frameworkPrimitives = Assembly.Load("System.Windows.Primitives");
        var frameworkCore = Assembly.Load("System.Private.Windows.Core");
        Verify(Path.GetDirectoryName(frameworkPrimitives.Location) == desktopDirectory, "WinForms primitives was replaced.");
        Verify(Path.GetDirectoryName(frameworkCore.Location) == desktopDirectory, "WinForms core was replaced.");
        Verify(frameworkPrimitives.GetName().Version!.Major == 10, "Unexpected WinForms primitives version.");
        Verify(frameworkCore.GetName().Version!.Major == 10, "Unexpected WinForms core version.");
        Verify(!File.Exists(Path.Join(AppContext.BaseDirectory, "System.Windows.Primitives.dll")), "Framework primitives was copied locally.");
        Verify(!File.Exists(Path.Join(AppContext.BaseDirectory, "System.Private.Windows.Core.dll")), "Framework core was copied locally.");

        VerifyLocal(typeof(Window).Assembly);
        VerifyLocal(typeof(SplashScreen).Assembly);
        VerifyLocal(Assembly.Load("WpfRuntime.Windows.Primitives"));

        using var form = new Forms.Form();
        using var button = new Forms.Button();
        var clicked = false;
        button.Click += (_, _) => clicked = true;
        button.PerformClick();
        Verify(clicked && form.Handle != IntPtr.Zero, "WinForms behavior failed.");

        var window = new Window();
        Verify(new WindowInteropHelper(window).EnsureHandle() != IntPtr.Zero, "WPF window creation failed.");
        window.Close();

        var splash = new SplashScreen("splash.png");
        splash.Show(false);
        splash.Close(TimeSpan.Zero);

        Console.WriteLine("WPF_WINFORMS_NET10_PASS");
        return 0;
    }

    private static void VerifyLocal(Assembly assembly)
    {
        Console.WriteLine($"{assembly.FullName}: {assembly.Location}");
        Verify(string.Equals(Path.GetDirectoryName(assembly.Location), Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory), StringComparison.OrdinalIgnoreCase), "Repository assembly was not loaded locally.");
    }

    private static void Verify(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
