using BytePulse.Core.Formatting;
using BytePulse.Core.Model;
using BytePulse.Core.Scanning;

namespace BytePulse.Cli.Output;

/// <summary>Walks a tree and hands every node to a callback.</summary>
internal static class TreeWalker
{
    public static void Walk(ScanTree tree, Action<int, int> visit)
    {
        foreach (int root in tree.RootNodeIds)
        {
            visit(root, 0);
            WalkChildren(tree, root, 1, visit);
        }
    }

    private static void WalkChildren(ScanTree tree, int nodeId, int depth, Action<int, int> visit)
    {
        foreach (int child in tree.GetSortedChildren(nodeId))
        {
            visit(child, depth);
            WalkChildren(tree, child, depth + 1, visit);
        }
    }
}

/// <summary>The summary listing, in the style of a disk usage report.</summary>
internal static class SummaryReport
{
    public static void Write(ScanResult result, int top)
    {
        ScanTree tree = result.Tree;
        string root = tree.GetName(tree.RootNodeIds[0]);

        Console.WriteLine();
        Console.WriteLine(root);
        Console.WriteLine(
            $"  {ByteFormatter.Format(tree.TotalLogicalSize)} in " +
            $"{ByteFormatter.FormatCount(tree.TotalFileCount, "file", "files")} and " +
            $"{ByteFormatter.FormatCount(tree.TotalDirectoryCount, "folder", "folders")}");
        Console.WriteLine(
            $"  scanned in {result.Elapsed.TotalSeconds:F2}s, " +
            $"{ByteFormatter.FormatCount((long)result.EntriesPerSecond, "entry", "entries")} per second");
        Console.WriteLine();

        var rows = new List<(string Path, long Size, long OnDisk, int Files)>();

        bool showOnDisk = tree.TotalAllocatedSize > 0;

        foreach (int rootId in tree.RootNodeIds)
        {
            Collect(tree, rootId, string.Empty, rows);
        }

        rows.Sort((a, b) => b.Size.CompareTo(a.Size));

        int count = Math.Min(top, rows.Count);

        const int pathWidth = 60;

        if (showOnDisk)
        {
            Console.WriteLine($"  {"Size",12}  {"On disk",12}  {"Files",10}  {"Path",-pathWidth}");
            Console.WriteLine($"  {new string('-', 12)}  {new string('-', 12)}  {new string('-', 10)}  {new string('-', pathWidth)}");
        }
        else
        {
            Console.WriteLine($"  {"Size",12}  {"Files",10}  {"Path",-pathWidth}");
            Console.WriteLine($"  {new string('-', 12)}  {new string('-', 10)}  {new string('-', pathWidth)}");
        }

        for (int i = 0; i < count; i++)
        {
            (string path, long size, long onDisk, int files) = rows[i];

            if (showOnDisk)
            {
                Console.WriteLine(
                    $"  {ByteFormatter.Format(size),12}  {ByteFormatter.Format(onDisk),12}  {files,10:N0}  {Trim(path, pathWidth)}");
            }
            else
            {
                Console.WriteLine($"  {ByteFormatter.Format(size),12}  {files,10:N0}  {Trim(path, pathWidth)}");
            }
        }

        if (rows.Count > count)
        {
            Console.WriteLine($"  ... and {rows.Count - count:N0} more");
        }

        WriteErrors(result);
    }

    private static void Collect(ScanTree tree, int nodeId, string prefix, List<(string Path, long Size, long OnDisk, int Files)> rows)
    {
        foreach (int child in tree.GetSortedChildren(nodeId))
        {
            string name = tree.GetName(child);
            string path = prefix.Length == 0 ? name : prefix + "\\" + name;

            ScanNode node = tree.GetNode(child);
            rows.Add((path, node.TotalLogicalSize, node.TotalAllocatedSize, node.TotalFileCount));

            Collect(tree, child, path, rows);
        }
    }

    private static void WriteErrors(ScanResult result)
    {
        if (result.Errors.Count == 0)
        {
            return;
        }

        int denied = result.Errors.Count(error => error.Kind == ScanErrorKind.AccessDenied);

        Console.WriteLine();
        Console.WriteLine($"  {result.Errors.Count:N0} directories could not be read:");

        foreach (ScanError error in result.Errors.Take(10))
        {
            Console.WriteLine($"    {error.Message}: {error.Path}");
        }

        if (result.Errors.Count > 10)
        {
            Console.WriteLine($"    ... and {result.Errors.Count - 10:N0} more");
        }

        if (denied > 0)
        {
            Console.WriteLine();
            Console.WriteLine(
                $"  {denied:N0} of them were access denied, so the total is lower than the real figure.");
            Console.WriteLine(
                Core.Hosting.Elevation.IsElevated
                    ? "  These are reserved for the system itself and stay refused even as an administrator."
                    : "  Run BytePulse as administrator to read most of them.");
        }
    }

    internal static string Trim(string value, int width) =>
        value.Length <= width ? value : "..." + value[^Math.Max(0, width - 3)..];
}

/// <summary>The whole tree, indented.</summary>
internal static class TreeReport
{
    public static void Write(ScanResult result)
    {
        ScanTree tree = result.Tree;

        Console.WriteLine();
        TreeWalker.Walk(tree, (nodeId, depth) =>
        {
            ScanNode node = tree.GetNode(nodeId);
            string indent = new(' ', depth * 2);
            string size = ByteFormatter.Format(node.TotalLogicalSize).PadLeft(12);

            Console.WriteLine($"{indent}{size}  {node.Name}");
        });

        WriteErrors(result);
    }

    private static void WriteErrors(ScanResult result)
    {
        foreach (ScanError error in result.Errors)
        {
            Console.Error.WriteLine($"error: {error.Message}: {error.Path}");
        }
    }
}

/// <summary>The results as comma separated values.</summary>
internal static class CsvReport
{
    public static void Write(ScanResult result)
    {
        ScanTree tree = result.Tree;

        Console.WriteLine("Path,Name,Size,SizeOnDisk,Files,Directories,Modified,IsDirectory");

        TreeWalker.Walk(tree, (nodeId, _) =>
        {
            ScanNode node = tree.GetNode(nodeId);

            Console.WriteLine(string.Join(',',
                Quote(tree.GetPath(nodeId)),
                Quote(node.Name),
                node.TotalLogicalSize,
                node.TotalAllocatedSize,
                node.TotalFileCount,
                node.TotalDirectoryCount,
                node.LastWriteTimeUtc.ToString("O"),
                node.IsDirectory ? "true" : "false"));
        });
    }

    private static string Quote(string value) =>
        value.Contains(',') || value.Contains('"')
            ? "\"" + value.Replace("\"", "\"\"") + "\""
            : value;
}
