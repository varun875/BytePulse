using System.Diagnostics;
using BytePulse.Core.Model;
using BytePulse.Core.Scanning;
using Xunit;

namespace BytePulse.Core.Tests.Scanning;

public sealed class DirectoryScannerTests : IDisposable
{
    private readonly string _root;

    public DirectoryScannerTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "BytePulseTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        TryDelete(_root);
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void ScansAnEmptyDirectory()
    {
        ScanResult result = new DirectoryScanner().Scan(new[] { _root });

        Assert.True(result.IsComplete);
        Assert.Empty(result.Errors);
        Assert.Equal(0, result.Tree.TotalFileCount);
        Assert.Equal(0, result.Tree.TotalLogicalSize);
        Assert.Equal(1, result.Tree.NodeCount);
    }

    [Fact]
    public void FindsFilesAndDirectories()
    {
        CreateFile("alpha.txt", 100);
        CreateFile("beta.bin", 2000);
        Directory.CreateDirectory(Path.Combine(_root, "sub"));
        CreateFile("sub/gamma.txt", 3000);

        ScanResult result = new DirectoryScanner().Scan(new[] { _root });

        Assert.Equal(3, result.Tree.TotalFileCount);
        Assert.Equal(1, result.Tree.TotalDirectoryCount);
        Assert.Equal(5100, result.Tree.TotalLogicalSize);
    }

    [Fact]
    public void MatchesAReferenceWalkExactly()
    {
        BuildSampleTree();

        ScanResult result = new DirectoryScanner().Scan(new[] { _root });
        var totals = new ReferenceTotals();
        WalkReference(_root, totals);

        Assert.True(result.IsComplete);
        Assert.Equal(totals.FileCount, result.Tree.TotalFileCount);
        Assert.Equal(totals.DirectoryCount, result.Tree.TotalDirectoryCount);
        Assert.Equal(totals.Size, result.Tree.TotalLogicalSize);
    }

    [Fact]
    public void FindsTheSameFilesAsAReferenceWalk()
    {
        BuildSampleTree();

        ScanResult result = new DirectoryScanner().Scan(new[] { _root });

        var found = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        CollectPaths(result.Tree, result.Tree.RootNodeIds[0], string.Empty, found);

        var expected = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        CollectReferencePaths(_root, string.Empty, expected);

        Assert.Equal(expected, found);
    }

    [Fact]
    public void ScansSeveralRoots()
    {
        string second = Path.Combine(_root, "second");
        Directory.CreateDirectory(second);
        CreateFile("a.txt", 10);
        CreateFile("second/b.txt", 20);

        ScanResult result = new DirectoryScanner().Scan(new[] { _root });

        Assert.Equal(2, result.Tree.TotalFileCount);
        Assert.Equal(30, result.Tree.TotalLogicalSize);
    }

    [Fact]
    public void RecordsAnUnreadableRootAndStillFinishes()
    {
        string missing = Path.Combine(_root, "does-not-exist");

        ScanResult result = new DirectoryScanner().Scan(new[] { missing });

        Assert.NotEmpty(result.Errors);
        Assert.Equal(ScanErrorKind.NotFound, result.Errors[0].Kind);
    }

    [Fact]
    public void ReportsProgress()
    {
        BuildSampleTree();

        var reports = new List<ScanProgress>();
        var progress = new Progress<ScanProgress>(reports.Add);

        new DirectoryScanner().Scan(new[] { _root }, progress: progress);

        // The final report is always sent, even if the scan was quick.
        Assert.NotEmpty(reports);
        Assert.True(reports[^1].IsComplete);
        Assert.Equal(4, reports[^1].FilesScanned);
    }

    [Fact]
    public void ReturnsAPartialTreeWhenCancelledBeforeItStarts()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        ScanResult result = new DirectoryScanner().Scan(new[] { _root }, cancellationToken: cancellation.Token);

        Assert.False(result.IsComplete);
    }

    [Fact]
    public void StopsPromptlyWhenCancelledMidScan()
    {
        // A tree with enough files in it that the scan cannot finish instantly.
        for (int d = 0; d < 40; d++)
        {
            string directory = Path.Combine(_root, $"dir{d}");
            Directory.CreateDirectory(directory);
            for (int f = 0; f < 100; f++)
            {
                File.WriteAllBytes(Path.Combine(directory, $"file{f}.dat"), new byte[64]);
            }
        }

        using var cancellation = new CancellationTokenSource();
        cancellation.CancelAfter(TimeSpan.FromMilliseconds(20));

        var stopwatch = Stopwatch.StartNew();
        ScanResult result = new DirectoryScanner().Scan(new[] { _root }, cancellationToken: cancellation.Token);
        stopwatch.Stop();

        Assert.False(result.IsComplete);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(5), $"the scan took {stopwatch.Elapsed}");
    }

    [Fact]
    public void DoesNotFollowReparsePointsByDefault()
    {
        string link = Path.Combine(_root, "link");
        if (!TryCreateJunction(link, _root))
        {
            return;
        }

        BuildSampleTree();

        ScanResult result = new DirectoryScanner().Scan(new[] { _root });

        // The link is present as a node but is not descended into, so the files inside
        // the target are not counted twice.
        Assert.Equal(4, result.Tree.TotalFileCount);
    }

    [Fact]
    public void FollowsReparsePointsWhenAsked()
    {
        string link = Path.Combine(_root, "link");
        if (!TryCreateJunction(link, _root))
        {
            return;
        }

        BuildSampleTree();

        ScanResult result = new DirectoryScanner().Scan(
            new[] { _root },
            new ScanOptions { FollowReparsePoints = true });

        // Following the link re-reads the tree, so the files are counted twice.
        Assert.Equal(8, result.Tree.TotalFileCount);
    }

    [Fact]
    public void SkipsHiddenFilesWhenAsked()
    {
        string hidden = Path.Combine(_root, "hidden.txt");
        File.WriteAllText(hidden, "x");
        File.SetAttributes(hidden, FileAttributes.Hidden);

        try
        {
            ScanResult result = new DirectoryScanner().Scan(
                new[] { _root },
                new ScanOptions { IncludeHiddenFiles = false });

            Assert.Equal(0, result.Tree.TotalFileCount);
        }
        finally
        {
            File.SetAttributes(hidden, FileAttributes.Normal);
        }
    }

    [Fact]
    public void HonoursTheMaximumDepth()
    {
        Directory.CreateDirectory(Path.Combine(_root, "one"));
        Directory.CreateDirectory(Path.Combine(_root, "one", "two"));
        CreateFile("one/two/three.txt", 10);

        ScanResult result = new DirectoryScanner().Scan(
            new[] { _root },
            new ScanOptions { MaxDepth = 1 });

        Assert.Equal(0, result.Tree.TotalFileCount);
    }

    private void BuildSampleTree()
    {
        CreateFile("alpha.txt", 100);
        CreateFile("beta.bin", 2000);

        Directory.CreateDirectory(Path.Combine(_root, "sub"));
        CreateFile("sub/gamma.txt", 3000);
        CreateFile("sub/delta.dat", 4000);
    }

    private void CreateFile(string relativePath, int size)
    {
        string path = Path.Combine(_root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, new byte[size]);
    }

    private static void CollectPaths(ScanTree tree, int nodeId, string prefix, HashSet<string> destination)
    {
        foreach (int childId in tree.GetSortedChildren(nodeId))
        {
            string path = prefix.Length == 0 ? tree.GetName(childId) : prefix + "\\" + tree.GetName(childId);
            destination.Add(path);
            CollectPaths(tree, childId, path, destination);
        }
    }

    private static void CollectReferencePaths(string directory, string prefix, HashSet<string> destination)
    {
        foreach (string entry in Directory.GetFileSystemEntries(directory))
        {
            string name = Path.GetFileName(entry);
            string path = prefix.Length == 0 ? name : prefix + "\\" + name;
            destination.Add(path);

            if (Directory.Exists(entry))
            {
                CollectReferencePaths(entry, path, destination);
            }
        }
    }

    private static void WalkReference(string directory, ReferenceTotals totals)
    {
        foreach (string entry in Directory.GetFileSystemEntries(directory))
        {
            if (Directory.Exists(entry))
            {
                // Directories report no size of their own, which is what the enumeration
                // returns as well.
                totals.DirectoryCount++;
                WalkReference(entry, totals);
            }
            else
            {
                var fileInfo = new FileInfo(entry);
                totals.Size += fileInfo.Length;
                totals.FileCount++;
            }
        }
    }

    private sealed class ReferenceTotals
    {
        public long FileCount { get; set; }

        public long DirectoryCount { get; set; }

        public long Size { get; set; }
    }

    private static bool TryCreateJunction(string linkPath, string targetPath)
    {
        try
        {
            var process = Process.Start(new ProcessStartInfo("cmd.exe")
            {
                ArgumentList = { "/c", "mklink", "/J", linkPath, targetPath },
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            });

            if (process is null)
            {
                return false;
            }

            process.WaitForExit(5000);
            return process.ExitCode == 0 && Directory.Exists(linkPath);
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            foreach (string entry in Directory.GetFileSystemEntries(path))
            {
                if (Directory.Exists(entry))
                {
                    TryDelete(entry);
                }
                else
                {
                    File.Delete(entry);
                }
            }

            Directory.Delete(path);
        }
        catch (Exception)
        {
        }
    }
}
