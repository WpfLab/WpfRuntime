using System.IO;
using System.Windows;
using System.Windows.Threading;
using WpfDemo.Diagnostics;

namespace WpfDemo;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        ContentRendered += OnContentRendered;
    }

    private void OnContentRendered(object? sender, EventArgs e)
    {
        Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, () => RunProbe(App.VerifyAndExit));
    }

    private void OnRunProbeClick(object sender, RoutedEventArgs e)
    {
        RunProbe(shouldExit: false);
    }

    private void RunProbe(bool shouldExit)
    {
        try
        {
            WpfRuntimeProbeResult result = WpfRuntimeProbe.Validate();
            string report = result.Format();
            ProbeOutputTextBox.Text = report;
            StatusTextBlock.Text = "Repository WPF validation passed";
            Console.WriteLine(report);
            if (shouldExit)
            {
                File.WriteAllText(App.VerificationReportPath, report);
                Application.Current.Shutdown(0);
            }
        }
        catch (Exception exception) when (exception is InvalidOperationException or IOException or BadImageFormatException)
        {
            ProbeOutputTextBox.Text = exception.ToString();
            StatusTextBlock.Text = "Repository WPF validation failed";
            Console.Error.WriteLine(exception);
            Environment.ExitCode = 1;
            if (shouldExit)
            {
                try
                {
                    File.WriteAllText(App.VerificationReportPath, exception.ToString());
                }
                catch (IOException reportException)
                {
                    Console.Error.WriteLine(reportException);
                }
                Application.Current.Shutdown(1);
            }
        }
    }
}