using System.Runtime.InteropServices;
using BytePulse.Core.Model;

namespace BytePulse.Core.Storage;

/// <summary>
/// The links between a node and its place in the tree.
/// </summary>
/// <remarks>
/// These six fields are read together on every tree traversal and aggregation step, so they
/// share one column to land in a single cache line. The struct is 20 bytes and unpadded.
/// </remarks>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
internal struct NodeLinks
{
    /// <summary>Node id of the parent, or -1 for a scan root.</summary>
    public int Parent;

    /// <summary>Node id of the first child, or -1 when the node has no children.</summary>
    public int FirstChild;

    /// <summary>Node id of the next sibling, or -1 when this is the last child.</summary>
    public int NextSibling;

    /// <summary>The number of direct children.</summary>
    public int ChildCount;

    /// <summary>Distance from the scan root. Zero for a scan root.</summary>
    public ushort Depth;

    /// <summary>The node's <see cref="NodeFlags"/>.</summary>
    public NodeFlags Flags;
}

/// <summary>
/// The columnar store that holds every node of a scan.
/// </summary>
/// <remarks>
/// A node's id is its index in these columns. Ids are handed out monotonically, which
/// guarantees a parent's id is always lower than its children's; that in turn makes
/// iterating ids in reverse a valid post-order traversal, so subtree totals can be
/// accumulated in a single pass without recursion or an explicit stack.
/// </remarks>
internal sealed class NodeArena
{
    /// <summary>Tree structure and per-node state.</summary>
    internal ChunkedColumn<NodeLinks> Links { get; } = new();

    /// <summary>Offset of the node's name within the <see cref="NameStore"/>.</summary>
    internal ChunkedColumn<int> NameOffset { get; } = new();

    /// <summary>Index of the node's aggregate row, or -1 for files.</summary>
    internal ChunkedColumn<int> DirIndex { get; } = new();

    /// <summary>The raw Win32 file attributes.</summary>
    internal ChunkedColumn<uint> Attributes { get; } = new();

    /// <summary>The node's own logical size in bytes.</summary>
    internal ChunkedColumn<long> LogicalSize { get; } = new();

    /// <summary>The node's own allocated size in bytes.</summary>
    internal ChunkedColumn<long> AllocatedSize { get; } = new();

    /// <summary>The file identifier, or zero when the file system does not provide one.</summary>
    internal ChunkedColumn<ulong> FileId { get; } = new();

    /// <summary>The number of hard links to the file, or zero when unknown.</summary>
    internal ChunkedColumn<ushort> LinkCount { get; } = new();

    /// <summary>Last write time, in UTC ticks.</summary>
    internal ChunkedColumn<long> LastWriteTicks { get; } = new();

    /// <summary>Creation time, in UTC ticks.</summary>
    internal ChunkedColumn<long> CreationTicks { get; } = new();

    /// <summary>Last access time, in UTC ticks.</summary>
    internal ChunkedColumn<long> AccessTicks { get; } = new();
}
