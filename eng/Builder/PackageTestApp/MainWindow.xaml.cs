using System.Globalization;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Runtime.Versioning;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace PackageTestApp;

public partial class MainWindow : Window
{
    private static readonly Version ExpectedWpfAssemblyVersion = new(42, 42, 42, 42424);

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
            Assembly directWriteForwarder = LoadAssembly("DirectWriteForwarder");
            ValidateWpfAssembly(directWriteForwarder, "DirectWriteForwarder.dll");
            ValidateDirectWriteItemizeAbi(directWriteForwarder);
            ValidateTextShaping();
            ValidateControls();

            StatusTextBlock.Text = "Validation passed";
            Console.WriteLine($"WPF XAML package probe completed on {Environment.ProcessPath}.");
            Application.Current.Shutdown(0);
        }
        catch (Exception exception) when (exception is InvalidOperationException or IOException or MissingMethodException)
        {
            Console.Error.WriteLine(exception);
            StatusTextBlock.Text = "Validation failed";
            Application.Current.Shutdown(1);
        }
    }

    private static void ValidateTextShaping()
    {
        FormattedText formattedText = new(
            "WPF package text shaping probe",
            CultureInfo.GetCultureInfo("en-US"),
            FlowDirection.LeftToRight,
            new Typeface("Segoe UI"),
            12,
            Brushes.Black,
            1);

        if (formattedText.Width <= 0)
            throw new InvalidOperationException("WPF text shaping returned an invalid width.");

        Console.WriteLine($"Validated WPF text shaping width {formattedText.Width:F2}.");
    }

    private static Assembly LoadAssembly(string assemblyName) =>
        AppDomain.CurrentDomain.GetAssemblies().SingleOrDefault(assembly =>
            string.Equals(assembly.GetName().Name, assemblyName, StringComparison.Ordinal))
        ?? Assembly.Load(assemblyName);

    private static void ValidateDirectWriteItemizeAbi(Assembly directWriteForwarder)
    {
        Type textAnalyzer = directWriteForwarder.GetType("MS.Internal.Text.TextInterface.TextAnalyzer", throwOnError: true)!;
        MethodInfo[] itemizeMethods = textAnalyzer
            .GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(method => string.Equals(method.Name, "Itemize", StringComparison.Ordinal))
            .ToArray();

        MethodInfo? itemize = itemizeMethods.SingleOrDefault(method =>
        {
            ParameterInfo[] parameters = method.GetParameters();
            return parameters.Length == 13 &&
                   parameters[0].ParameterType.IsPointer &&
                   parameters[0].ParameterType.GetElementType() == typeof(char) &&
                   parameters[1].ParameterType == typeof(uint) &&
                   parameters[2].ParameterType == typeof(CultureInfo) &&
                   parameters[4].ParameterType == typeof(bool) &&
                   parameters[5].ParameterType == typeof(CultureInfo) &&
                   parameters[6].ParameterType == typeof(bool) &&
                   parameters[7].ParameterType == typeof(uint);
        });

        if (itemize is null)
        {
            string actualSignatures = string.Join(
                Environment.NewLine,
                itemizeMethods.Select(method => $"  {FormatMethodSignature(method)}"));
            throw new MissingMethodException(
                "DirectWriteForwarder does not expose the 13-parameter TextAnalyzer.Itemize ABI required by PresentationCore." +
                Environment.NewLine + actualSignatures);
        }

        Console.WriteLine($"Validated DirectWrite ABI: {FormatMethodSignature(itemize)}");
    }

    private static string FormatMethodSignature(MethodInfo method) =>
        $"{method.ReturnType} {method.DeclaringType?.FullName}.{method.Name}(" +
        string.Join(", ", method.GetParameters().Select(parameter => parameter.ParameterType.ToString())) + ")";

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

        Version? assemblyVersion = assembly.GetName().Version;
        if (assemblyVersion != ExpectedWpfAssemblyVersion)
        {
            throw new InvalidOperationException(
                $"{assembly.GetName().Name} must have package assembly version {ExpectedWpfAssemblyVersion}, actual: {assemblyVersion?.ToString() ?? "missing"}.");
        }

        var targetFramework = assembly.GetCustomAttribute<TargetFrameworkAttribute>()?.FrameworkName;
        if (!string.Equals(targetFramework, ".NETCoreApp,Version=v8.0", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"{assembly.GetName().Name} must remain a .NET 8 assembly, actual target framework: {targetFramework ?? "missing"}.");
        }

        string sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(actualPath)));
        Console.WriteLine(
            $"Loaded {assembly.GetName().Name} {assembly.GetName().Version} ({targetFramework}) from {actualPath}; " +
            $"MVID={assembly.ManifestModule.ModuleVersionId}; SHA256={sha256}.");
    }

    private void OnActionButtonClick(object sender, RoutedEventArgs e)
    {
        StatusTextBlock.Text = "Button routed event handled";
    }
}
