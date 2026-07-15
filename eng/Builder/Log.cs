namespace WpfReorganize.Builder;

internal static class Log
{
    public static void Step(string message) => Console.WriteLine($"\n--- {message}");

    public static void Info(string message) => Console.WriteLine($"    {message}");

    public static void Warn(string message) => Console.WriteLine($"    [WARN] {message}");

    public static void Error(string message) => Console.Error.WriteLine($"    [ERROR] {message}");
}
