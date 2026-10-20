namespace BytePulse.Services;

/// <summary>
/// The choices the application remembers between runs.
/// </summary>
public sealed class AppSettings
{
    /// <summary>How many threads read directories at once.</summary>
    public int ThreadCount { get; set; } = Math.Min(Environment.ProcessorCount, 8);

    /// <summary>Whether to descend into junctions and symbolic links.</summary>
    public bool FollowReparsePoints { get; set; }

    /// <summary>Whether hidden and system files are included.</summary>
    public bool IncludeHiddenAndSystem { get; set; } = true;

    /// <summary>Whether to read how much space each file occupies on disk.</summary>
    public bool ComputeSizeOnDisk { get; set; }

    /// <summary>Whether to count a file with several hard links once.</summary>
    public bool DetectHardlinks { get; set; }

    /// <summary>Whether folders are listed before files.</summary>
    public bool DirectoriesFirst { get; set; } = true;

    /// <summary>The column the results are sorted by.</summary>
    public string SortColumn { get; set; } = "Size";

    /// <summary>A copy of these settings.</summary>
    public AppSettings Clone() => (AppSettings)MemberwiseClone();
}
