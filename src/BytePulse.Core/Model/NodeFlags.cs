namespace BytePulse.Core.Model;

/// <summary>
/// Per-node state. Stored in the hot column of the node arena, so the layout is
/// deliberately narrow.
/// </summary>
[Flags]
public enum NodeFlags : ushort
{
    None = 0,

    /// <summary>The node is a directory.</summary>
    IsDirectory = 1 << 0,

    /// <summary>The node has at least one child.</summary>
    HasChildren = 1 << 1,

    /// <summary>The node is a reparse point (junction, symlink, mount point).</summary>
    IsReparsePoint = 1 << 2,

    IsHidden = 1 << 3,
    IsSystem = 1 << 4,
    IsOffline = 1 << 5,
    IsSparse = 1 << 6,
    IsCompressed = 1 << 7,
    IsEncrypted = 1 << 8,

    /// <summary>The node is a volume root.</summary>
    IsVolumeRoot = 1 << 9,

    /// <summary>More than one directory entry refers to the same underlying file.</summary>
    HasHardLinks = 1 << 10,

    /// <summary>This entry is the canonical one for its hard link group.</summary>
    IsPrimaryLink = 1 << 11,

    /// <summary>The node's content is identical to that of another file.</summary>
    IsDuplicate = 1 << 12,

    /// <summary>The logical size of the node is known.</summary>
    SizeKnown = 1 << 13,

    /// <summary>The allocated (on-disk) size of the node is known.</summary>
    AllocatedSizeKnown = 1 << 14,

    /// <summary>The directory was enumerated to completion.</summary>
    DirectoryScanned = 1 << 15,
}
