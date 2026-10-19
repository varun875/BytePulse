namespace BytePulse.Core.Model;

/// <summary>Which column a sibling list is ordered by.</summary>
public enum SortKey
{
    None,
    Name,
    LogicalSize,
    AllocatedSize,
    FileCount,
    DirectoryCount,
    LastWriteTime,
}

/// <summary>Direction of a sort.</summary>
public enum SortDirection
{
    Ascending,
    Descending,
}

/// <summary>
/// Describes how the children of a directory are ordered. The comparison is total and
/// deterministic: directories-first, then the primary key, then the name, then the node id.
/// </summary>
public readonly record struct SortSpecification
{
    public required SortKey Key { get; init; }

    public required SortDirection Direction { get; init; }

    /// <summary>When set, directories are listed before files regardless of the primary key.</summary>
    public bool DirectoriesFirst { get; init; }

    /// <summary>The order used when a scan starts: biggest folders first.</summary>
    public static SortSpecification Default => new()
    {
        Key = SortKey.LogicalSize,
        Direction = SortDirection.Descending,
        DirectoriesFirst = true,
    };
}
