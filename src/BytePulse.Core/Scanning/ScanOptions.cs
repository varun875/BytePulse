using BytePulse.Core.Model;

namespace BytePulse.Core.Scanning;

/// <summary>How a scan should behave.</summary>
public sealed record ScanOptions
{
    /// <summary>
    /// The number of threads that read directories. Defaults to the number of processors,
    /// capped at eight, which is where the speed-up over a single threaded walk comes from.
    /// </summary>
    public int ThreadCount { get; init; } = Math.Min(Environment.ProcessorCount, 8);

    /// <summary>
    /// Whether to descend into reparse points such as junctions and symbolic links.
    /// Off by default, which makes loops through reparse points impossible.
    /// </summary>
    public bool FollowReparsePoints { get; init; }

    /// <summary>The deepest level of directories to descend into.</summary>
    public int MaxDepth { get; init; } = 256;

    /// <summary>
    /// The largest number of directories that may be waiting to be read at once. This
    /// bounds the memory the walk uses on a tree with very many directories.
    /// </summary>
    public int MaxQueuedDirectories { get; init; } = 65_536;

    /// <summary>The order the results are sorted in.</summary>
    public SortSpecification Sort { get; init; } = SortSpecification.Default;

    /// <summary>Whether hidden files are included.</summary>
    public bool IncludeHiddenFiles { get; init; } = true;

    /// <summary>Whether system files are included.</summary>
    public bool IncludeSystemFiles { get; init; } = true;

    /// <summary>
    /// Whether to read how much space each file occupies on disk. This opens every file,
    /// so it is off by default and is much slower than the walk itself.
    /// </summary>
    public bool ComputeSizeOnDisk { get; init; }

    /// <summary>
    /// Whether to work out which files have more than one hard link, so a file that is
    /// reachable by several names is only counted once.
    /// </summary>
    public bool DetectHardlinks { get; init; }
}
