namespace BytePulse.Core.Model;

/// <summary>
/// A materialized snapshot of a single node, for display and inspection.
/// </summary>
/// <remarks>
/// The scan itself is stored in a columnar form; this type is the convenience view the UI
/// binds to. It is a value type, so reading one never allocates.
/// </remarks>
public readonly record struct ScanNode
{
    /// <summary>The node's stable id. It survives re-sorting.</summary>
    public required int Id { get; init; }

    /// <summary>The node's parent id, or -1 for a scan root.</summary>
    public required int ParentId { get; init; }

    /// <summary>The node's depth below the scan root.</summary>
    public required int Depth { get; init; }

    /// <summary>The node's flags.</summary>
    public required NodeFlags Flags { get; init; }

    /// <summary>The node's name.</summary>
    public required string Name { get; init; }

    /// <summary>The node's own logical size in bytes.</summary>
    public required long LogicalSize { get; init; }

    /// <summary>The node's own allocated size in bytes.</summary>
    public required long AllocatedSize { get; init; }

    /// <summary>The total logical size of the node and everything beneath it.</summary>
    public required long TotalLogicalSize { get; init; }

    /// <summary>The total allocated size of the node and everything beneath it.</summary>
    public required long TotalAllocatedSize { get; init; }

    /// <summary>The number of files in the subtree.</summary>
    public required int TotalFileCount { get; init; }

    /// <summary>
    /// The number of directories in the subtree, not counting the node itself.
    /// </summary>
    public required int TotalDirectoryCount { get; init; }

    /// <summary>The node's last write time.</summary>
    public required DateTimeOffset LastWriteTimeUtc { get; init; }

    /// <summary>The file identifier, or zero when the file system does not provide one.</summary>
    public required ulong FileId { get; init; }

    /// <summary>The number of hard links to the file, or zero when unknown.</summary>
    public required ushort LinkCount { get; init; }

    /// <summary>Whether the node is a directory.</summary>
    public bool IsDirectory => (Flags & NodeFlags.IsDirectory) != 0;
}
