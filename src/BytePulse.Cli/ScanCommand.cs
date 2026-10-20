using System.CommandLine;
using BytePulse.Cli.Output;
using BytePulse.Core.Formatting;
using BytePulse.Core.Model;
using BytePulse.Core.Scanning;

namespace BytePulse.Cli;

/// <summary>The <c>scan</c> command.</summary>
internal static class ScanCommand
{
    public static Command Create()
    {
        var command = new Command("scan", "Scan a directory and report what is using the space.");

        var pathArgument = new Argument<string>("path")
        {
            Description = "The directory to scan.",
        };

        var jsonOption = new Option<bool>("--json") { Description = "Write the results as JSON." };
        var csvOption = new Option<bool>("--csv") { Description = "Write the results as CSV." };
        var treeOption = new Option<bool>("--tree") { Description = "Write the whole tree, indented." };
        var threadsOption = new Option<int>("--threads") { Description = "How many threads read directories at once." };
        var sortOption = new Option<string>("--sort") { Description = "Sort by name, size, sizeondisk, files or modified." };
        var topOption = new Option<int>("--top") { Description = "How many entries the summary lists." };
        var followOption = new Option<bool>("--follow-reparse") { Description = "Follow junctions and symbolic links." };
        var hiddenOption = new Option<bool>("--no-hidden") { Description = "Leave hidden and system files out." };
        var sizeOnDiskOption = new Option<bool>("--size-on-disk") { Description = "Read how much space each file occupies on disk." };
        var hardlinkOption = new Option<bool>("--hardlinks") { Description = "Count a file with several hard links once." };
        var versionOption = new Option<bool>("--version") { Description = "Print the version and exit." };

        command.Arguments.Add(pathArgument);
        command.Options.Add(jsonOption);
        command.Options.Add(csvOption);
        command.Options.Add(treeOption);
        command.Options.Add(threadsOption);
        command.Options.Add(sortOption);
        command.Options.Add(topOption);
        command.Options.Add(followOption);
        command.Options.Add(hiddenOption);
        command.Options.Add(sizeOnDiskOption);
        command.Options.Add(hardlinkOption);
        command.Options.Add(versionOption);

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            if (parseResult.GetValue(versionOption))
            {
                Console.WriteLine(VersionInformation.Text);
                return 0;
            }

            string? path = parseResult.GetValue(pathArgument);
            if (string.IsNullOrWhiteSpace(path))
            {
                Console.Error.WriteLine("error: a path is required");
                return 1;
            }
            var options = new ScanOptions
            {
                ThreadCount = parseResult.GetValue(threadsOption) > 0
                    ? parseResult.GetValue(threadsOption)
                    : new ScanOptions().ThreadCount,
                Sort = ParseSort(parseResult.GetValue(sortOption)),
                FollowReparsePoints = parseResult.GetValue(followOption),
                IncludeHiddenFiles = !parseResult.GetValue(hiddenOption),
                IncludeSystemFiles = !parseResult.GetValue(hiddenOption),
                ComputeSizeOnDisk = parseResult.GetValue(sizeOnDiskOption),
                DetectHardlinks = parseResult.GetValue(hardlinkOption),
            };

            int top = parseResult.GetValue(topOption) > 0 ? parseResult.GetValue(topOption) : 25;

            var scanner = new DirectoryScanner();
            var progress = new Progress<ScanProgress>(p => WriteProgress(p));

            ScanResult result = await scanner.ScanAsync(
                new[] { path },
                options,
                progress,
                cancellationToken);
            Console.Error.WriteLine();

            if (parseResult.GetValue(jsonOption))
            {
                JsonReport.Write(result);
            }
            else if (parseResult.GetValue(csvOption))
            {
                CsvReport.Write(result);
            }
            else if (parseResult.GetValue(treeOption))
            {
                TreeReport.Write(result);
            }
            else
            {
                SummaryReport.Write(result, top);
            }

            return result.IsComplete ? 0 : 2;
        });

        return command;
    }

    private static SortSpecification ParseSort(string? key) => key?.ToLowerInvariant() switch
    {
        "name" => new SortSpecification { Key = SortKey.Name, Direction = SortDirection.Ascending, DirectoriesFirst = true },
        "size" or "sizeondisk" or "size on disk" => SortSpecification.Default,
        "files" or "filecount" => new SortSpecification { Key = SortKey.FileCount, Direction = SortDirection.Descending, DirectoriesFirst = true },
        "modified" or "lastwritetime" => new SortSpecification { Key = SortKey.LastWriteTime, Direction = SortDirection.Descending, DirectoriesFirst = true },
        _ => SortSpecification.Default,
    };

    private static void WriteProgress(ScanProgress progress)
    {
        string text =
            $"  {ByteFormatter.FormatCount(progress.EntriesScanned, "entry", "entries")} scanned, " +
            $"{ByteFormatter.Format(progress.BytesScanned)}";

        // Pad the line so a shorter message erases the one before it.
        Console.Error.Write('\r');
        Console.Error.Write(text.PadRight(78));
    }
}
