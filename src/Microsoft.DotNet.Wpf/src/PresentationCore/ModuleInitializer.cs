using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using MS.Internal.Text.TextInterface;

internal static class ModuleInitializer
{
    /// <summary>
    /// DirectWriteForwarder has a module constructor that implements
    /// the setting of the default DPI awareness for the process.
    /// We need to load DirectWriteForwarder the instant PresentationCore
    /// loads in order to ensure that this is set before any DPI sensitive
    /// operations are carried out.  To do this, we simply call LoadDwrite
    /// as the module constructor for DirectWriteForwarder would do this anyway.
    /// </summary>
#pragma warning disable CA2255
    [ModuleInitializer]
    public static void Initialize()
    {
        LoadAppLocalDirectWriteForwarder();

        IsProcessDpiAware();

        DWriteLoader.LoadDWrite();

        LoadNativeWpfDlls();
    }
#pragma warning restore CA2255

    // Keep the static DirectWriteForwarder reference out of Initialize so the JIT cannot bind it before the app-local load.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void LoadNativeWpfDlls()
    {
        MS.Internal.NativeWPFDLLLoader.LoadDwrite();
    }

    private static void LoadAppLocalDirectWriteForwarder()
    {
        string assemblyPath = Path.Combine(AppContext.BaseDirectory, "DirectWriteForwarder.dll");
        if (File.Exists(assemblyPath))
        {
            AssemblyLoadContext.Default.LoadFromAssemblyPath(assemblyPath);
        }
    }

    private static void IsProcessDpiAware()
    {
        bool disableDpiAware = false;

        // By default, Application is DPIAware.
        Assembly assemblyApp = Assembly.GetEntryAssembly();

        // Check if the Application has explicitly set DisableDpiAwareness attribute.
        if (assemblyApp != null && Attribute.IsDefined(assemblyApp, typeof(System.Windows.Media.DisableDpiAwarenessAttribute)))
        {
            disableDpiAware = true;
        }

        if (!disableDpiAware)
        {
            // DpiAware composition is enabled for this application.
            SetProcessDPIAware_Internal();
        }

        // Only when DisableDpiAwareness attribute is set in Application assembly,
        // It will ignore the SetProcessDPIAware API call.
    }

    [DllImport("user32.dll", EntryPoint = "SetProcessDPIAware")]
    private static extern void SetProcessDPIAware_Internal();
}
