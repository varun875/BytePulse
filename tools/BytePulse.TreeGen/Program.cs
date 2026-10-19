using System.CommandLine;
using BytePulse.TreeGen;

namespace BytePulse.TreeGen;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        var root = new RootCommand("BytePulse tree generator - creates a synthetic directory tree for benchmarks and tests.");

        var pathArgument = new Argument<string>("path") { Description = "The directory to create the tree in." };
        var filesOption = new Option<int>("--files") { Description = "How many files to create." };
        var sizeOption = new Option<int>("--size") { Description = "The size of each file in bytes." };
        var perDirectoryOption = new Option<int>("--per-directory") { Description = "How many files each directory holds." };
        var seedOption = new Option<int>("--seed") { Description = "The seed for the random layout." };
        var measureOption = new Option<bool>("--measure") { Description = "Only report what is already under the path." };

        root.Arguments.Add(pathArgument);
        root.Options.Add(filesOption);
        root.Options.Add(sizeOption);
        root.Options.Add(perDirectoryOption);
        root.Options.Add(seedOption);
        root.Options.Add(measureOption);

        root.SetAction(parseResult =>
        {
            string path = parseResult.GetValue(pathArgument) ?? ".";

            if (parseResult.GetValue(measureOption))
            {
                (int files, int directories, long bytes) = TreeGenerator.Measure(path);
                Console.WriteLine($"{files} files, {directories} directories, {bytes} bytes");
                return 0;
            }

            int fileCount = parseResult.GetValue(filesOption);
            int perDirectory = parseResult.GetValue(perDirectoryOption);
            int size = parseResult.GetValue(sizeOption);
            int seed = parseResult.GetValue(seedOption);

            var layout = perDirectory > 0
                ? new TreeLayout(Math.Max(1, fileCount / perDirectory), perDirectory, size, seed)
                : TreeLayout.ForFileCount(fileCount, size, seed);

            Console.WriteLine($"generating {layout.FileCount} files of {layout.FileSize} bytes under {path}...");

            TreeGenerationResult result = TreeGenerator.Generate(path, layout);

            Console.WriteLine(
                $"done in {result.Elapsed.TotalSeconds:F1}s: {result.FileCount} files, " +
                $"{result.DirectoryCount} directories, {result.TotalBytes} bytes");

            return 0;
        });

        return await root.Parse(args).InvokeAsync();
    }
}
