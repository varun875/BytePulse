namespace BytePulse.Core.Scanning;

/// <summary>
/// One entry produced by a directory enumerator.
/// </summary>
/// <remarks>
/// The name is carried as the raw UTF-16 bytes the file system returned, so it can be
/// copied into the name store without being decoded first.
/// </remarks>
public readonly ref struct DirectoryEntry
{
    /// <summary>The entry's name, as UTF-16LE bytes without a terminator.</summary>
    public ReadOnlySpan<byte> NameUtf16 { get; }

    /// <summary>The file's logical size in bytes.</summary>
    public long LogicalSize { get; }

    /// <summary>The space the file occupies on disk, in bytes.</summary>
    public long AllocatedSize { get; }

    /// <summary>The raw Win32 file attributes.</summary>
    public uint Attributes { get; }

    /// <summary>The file identifier, or zero when the file system does not provide one.</summary>
    public ulong FileId { get; }

    /// <summary>Last write time, in UTC ticks.</summary>
    public long LastWriteTicks { get; }

    /// <summary>Creation time, in UTC ticks.</summary>
    public long CreationTicks { get; }

    /// <summary>Last access time, in UTC ticks.</summary>
    public long AccessTicks { get; }

    /// <summary>Whether the entry is a directory.</summary>
    public bool IsDirectory { get; }

    /// <summary>Whether the entry is a reparse point.</summary>
    public bool IsReparsePoint { get; }

    /// <summary>Creates an entry.</summary>
    public DirectoryEntry(
        ReadOnlySpan<byte> nameUtf16,
        long LogicalSize,
        long AllocatedSize,
        uint Attributes,
        ulong FileId,
        long LastWriteTicks,
        long CreationTicks,
        long AccessTicks,
        bool IsDirectory,
        bool IsReparsePoint)
    {
        NameUtf16 = nameUtf16;
        this.LogicalSize = LogicalSize;
        this.AllocatedSize = AllocatedSize;
        this.Attributes = Attributes;
        this.FileId = FileId;
        this.LastWriteTicks = LastWriteTicks;
        this.CreationTicks = CreationTicks;
        this.AccessTicks = AccessTicks;
        this.IsDirectory = IsDirectory;
        this.IsReparsePoint = IsReparsePoint;
    }
}
