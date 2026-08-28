using System.IO;
using System.Reflection;
using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace PackageTestApp;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        ContentRendered += OnContentRendered;
    }

    private void OnContentRendered(object? sender, EventArgs e)
    {
        Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, ValidateAndClose);
    }

    private void ValidateAndClose()
    {
        try
        {
            ValidateRuntimeVersion();
            ValidateWpfAssembly(typeof(DependencyObject).Assembly, "WindowsBase.dll");
            ValidateWpfAssembly(typeof(Visual).Assembly, "PresentationCore.dll");
            ValidateWpfAssembly(typeof(Application).Assembly, "PresentationFramework.dll");
            ValidateControls();

            StatusTextBlock.Text = "Validation passed";
            Console.WriteLine($"WPF XAML package probe completed on {Environment.ProcessPath}.");
            Application.Current.Shutdown(0);
        }
        catch (Exception exception) when (exception is InvalidOperationException or IOException)
        {
            Console.Error.WriteLine(exception);
            StatusTextBlock.Text = "Validation failed";
            Application.Current.Shutdown(1);
        }
    }

    private void ValidateControls()
    {
        RequireControl(ContentTabs, nameof(ContentTabs));
        RequireControl(NameTextBox, nameof(NameTextBox));
        RequireControl(ThemeComboBox, nameof(ThemeComboBox));
        RequireControl(AnimationsCheckBox, nameof(AnimationsCheckBox));
        RequireControl(StandardModeRadioButton, nameof(StandardModeRadioButton));
        RequireControl(ValidationProgressBar, nameof(ValidationProgressBar));
        RequireControl(ActionButton, nameof(ActionButton));
        RequireControl(AvalonEditor, nameof(AvalonEditor));
        RequireControl(StatusTextBlock, nameof(StatusTextBlock));

        if (Application.Current.Resources["AccentBrush"] is not SolidColorBrush)
            throw new InvalidOperationException("Application XAML resource 'AccentBrush' was not loaded.");

        ActionButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        if (!string.Equals(StatusTextBlock.Text, "Button routed event handled", StringComparison.Ordinal))
            throw new InvalidOperationException("Button routed event was not handled.");

        Console.WriteLine(
            $"Validated XAML controls: {ContentTabs.Items.Count} tabs, AvalonEdit text length {AvalonEditor.Text.Length}, " +
            $"progress {ValidationProgressBar.Value}.");
    }

    private static void RequireControl(FrameworkElement? control, string name)
    {
        if (control is null)
            throw new InvalidOperationException($"XAML control '{name}' was not created.");
    }

    private static void ValidateRuntimeVersion()
    {
#if NET9_0
        const int expectedMajorVersion = 9;
#else
        const int expectedMajorVersion = 8;
#endif
        if (Environment.Version.Major != expectedMajorVersion)
        {
            throw new InvalidOperationException(
                $"Expected .NET {expectedMajorVersion} runtime, actual: {Environment.Version}.");
        }

        Console.WriteLine($"Running on .NET {Environment.Version}.");
    }

    private static void ValidateWpfAssembly(Assembly assembly, string expectedFileName)
    {
        var expectedPath = Path.GetFullPath(Path.Join(AppContext.BaseDirectory, expectedFileName));
        var actualPath = Path.GetFullPath(assembly.Location);
        if (!string.Equals(actualPath, expectedPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"{assembly.GetName().Name} was loaded from '{actualPath}' instead of package output '{expectedPath}'.");
        }

        var targetFramework = assembly.GetCustomAttribute<TargetFrameworkAttribute>()?.FrameworkName;
        if (!string.Equals(targetFramework, ".NETCoreApp,Version=v8.0", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"{assembly.GetName().Name} must remain a .NET 8 assembly, actual target framework: {targetFramework ?? "missing"}.");
        }

        Console.WriteLine(
            $"Loaded {assembly.GetName().Name} {assembly.GetName().Version} ({targetFramework}) from {actualPath}.");
    }

    private void OnActionButtonClick(object sender, RoutedEventArgs e)
    {
        StatusTextBlock.Text = "Button routed event handled";
    }
}
