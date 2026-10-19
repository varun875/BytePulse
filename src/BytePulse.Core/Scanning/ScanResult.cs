using BytePulse.Core.Model;

namespace BytePulse.Core.Scanning;

/// <summary>What a scan found.</summary>
public sealed class ScanResult
{
    /// <summary>Creates a result.</summary>
    public ScanResult(
        ScanTree tree,
        IReadOnlyList<ScanError> errors,
        bool isComplete,
        TimeSpan elapsed,
        long filesScanned,
        long directoriesScanned,
        long bytesScanned)
    {
        Tree = tree;
        Errors = errors;
        IsComplete = isComplete;
        Elapsed = elapsed;
        FilesScanned = filesScanned;
        DirectoriesScanned = directoriesScanned;
        BytesScanned = bytesScanned;
    }

    /// <summary>The tree that was built.</summary>
    public ScanTree Tree { get; }

    /// <summary>The directories that could not be read, and why.</summary>
    public IReadOnlyList<ScanError> Errors { get; }

    /// <summary>Whether the scan ran to the end. A cancelled scan is not complete.</summary>
    public bool IsComplete { get; }

    /// <summary>How long the scan took.</summary>
    public TimeSpan Elapsed { get; }

    /// <summary>The number of files seen.</summary>
    public long FilesScanned { get; }

    /// <summary>The number of directories read.</summary>
    public long DirectoriesScanned { get; }

    /// <summary>The total logical size of the files seen.</summary>
    public long BytesScanned { get; }

    /// <summary>The number of entries seen per second.</summary>
    public double EntriesPerSecond =>
        Elapsed.TotalSeconds > 0 ? (FilesScanned + DirectoriesScanned) / Elapsed.TotalSeconds : 0;

    /// <summary>The number of bytes seen per second.</summary>
    public double BytesPerSecond =>
        Elapsed.TotalSeconds > 0 ? BytesScanned / Elapsed.TotalSeconds : 0;
}
