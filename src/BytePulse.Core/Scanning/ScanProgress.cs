namespace BytePulse.Core.Scanning;

/// <summary>How far a scan has got.</summary>
/// <param name="FilesScanned">The number of files seen so far.</param>
/// <param name="DirectoriesScanned">The number of directories read so far.</param>
/// <param name="BytesScanned">The total logical size of the files seen so far.</param>
/// <param name="Elapsed">How long the scan has been running.</param>
/// <param name="IsComplete">Whether the scan has finished.</param>
public readonly record struct ScanProgress(
    long FilesScanned,
    long DirectoriesScanned,
    long BytesScanned,
    TimeSpan Elapsed,
    bool IsComplete)
{
    /// <summary>The number of entries seen, files and directories together.</summary>
    public long EntriesScanned => FilesScanned + DirectoriesScanned;
}
