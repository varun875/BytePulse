using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Jobs;
using BytePulse.Core.Scanning;
using BytePulse.TreeGen;

namespace BytePulse.Benchmarks;

/// <summary>
/// Compares the parallel scanner against the single threaded walks a typical disk usage
/// tool performs.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(warmupCount: 2, iterationCount: 5)]
public class ScanBenchmarks
{
    private string _root = null!;
    private int _expectedFiles;
    private int _expectedDirectories;

    /// <summary>The number of files in the tree the benchmark measures.</summary>
    [Params(20_000)]
    public int FileCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _root = Path.Combine(
            Path.GetTempPath(),
            "BytePulseBench",
            $"tree{FileCount}");

        if (!Directory.Exists(_root))
        {
            TreeGenerator.Generate(_root, TreeLayout.ForFileCount(FileCount));
        }

        (int files, int directories, _) = TreeGenerator.Measure(_root);
        _expectedFiles = files;
        _expectedDirectories = directories;
    }

    [GlobalCleanup]
    public void Cleanup()
    {
    }

    [Benchmark(Baseline = true, Description = "naive: framework enumeration, one thread")]
    public long NaiveFrameworkWalk()
    {
        (long files, _, long bytes) = NaiveWalker.UsingFrameworkEnumeration(_root);
        Check(_expectedFiles, files);
        return bytes;
    }

    [Benchmark(Description = "naive: native enumeration, one thread")]
    public long NaiveNativeWalk()
    {
        (long files, _, long bytes) = NaiveWalker.UsingNativeEnumeration(_root);
        Check(_expectedFiles, files);
        return bytes;
    }

    [Benchmark(Description = "parallel scanner, one thread")]
    public long ParallelScanOneThread() => RunScan(1);

    [Benchmark(Description = "parallel scanner, four threads")]
    public long ParallelScanFourThreads() => RunScan(4);

    [Benchmark(Description = "parallel scanner, eight threads")]
    public long ParallelScanEightThreads() => RunScan(8);

    private long RunScan(int threads)
    {
        ScanResult result = new DirectoryScanner().Scan(
            new[] { _root },
            new ScanOptions { ThreadCount = threads });

        Check(_expectedFiles, result.Tree.TotalFileCount);
        Check(_expectedDirectories, result.Tree.TotalDirectoryCount);

        return result.Tree.TotalLogicalSize;
    }

    /// <summary>
    /// Fails the benchmark if the walk found something other than what the tree holds, so
    /// a fast but wrong result cannot be mistaken for a fast result.
    /// </summary>
    private static void Check(long expected, long actual)
    {
        if (expected != actual)
        {
            throw new InvalidOperationException($"the walk found {actual} items, expected {expected}");
        }
    }
}
