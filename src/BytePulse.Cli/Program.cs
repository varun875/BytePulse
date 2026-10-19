using System.CommandLine;

namespace BytePulse.Cli;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        var root = new RootCommand("BytePulse - a fast, open source disk usage analyzer for Windows.");
        root.Subcommands.Add(ScanCommand.Create());

        return await root.Parse(args).InvokeAsync();
    }
}
