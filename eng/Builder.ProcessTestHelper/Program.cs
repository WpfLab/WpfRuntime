using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;

if (args.Length == 0)
{
    return 2;
}

switch (args[0])
{
    case "echo":
        foreach (var argument in args.Skip(1))
        {
            Console.WriteLine(argument);
        }
        return 0;
    case "parent" when args.Length == 2:
        Process.Start(new ProcessStartInfo
        {
            FileName = Environment.ProcessPath!,
            UseShellExecute = false,
            ArgumentList = { "child", args[1] },
        });
        Thread.Sleep(Timeout.Infinite);
        return 0;
    case "child" when args.Length == 2:
        Thread.Sleep(TimeSpan.FromSeconds(2));
        File.WriteAllText(args[1], "child survived");
        return 0;
    default:
        return 2;
}
