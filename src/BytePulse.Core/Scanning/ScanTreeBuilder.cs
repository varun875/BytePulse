using System.Collections.Concurrent;
using System.Text;
using BytePulse.Core.Model;
using BytePulse.Core.Storage;

namespace BytePulse.Core.Scanning;

/// <summary>
/// Builds a <see cref="ScanTree"/> from directory entries.
/// </summary>
/// <remarks>
/// <para>
/// The builder is safe to use from several threads at once. Node ids are handed out by a
/// single atomic counter, which guarantees a parent's id is always lower than its
/// children's; that is what lets the final aggregation pass walk the nodes in reverse and
/// accumulate subtree totals without recursion.
/// </para>
/// <para>
/// Each directory's children are linked by exactly one thread - the one that enumerated
/// that directory - so the hot path takes no locks at all.
/// </para>
/// </remarks>
public sealed class ScanTreeBuilder
{
    private readonly NodeArena _arena = new();
    private readonly NameStore _names = new();
    private readonly ConcurrentQueue<ScanError> _errors = new();
    private readonly List<int> _rootNodeIds = new();
    private readonly object _rootLock = new();

    private int _nextNodeId;
    private int _nextDirectoryCount;

    /// <summary>The number of nodes added so far.</summary>
    public int NodeCount => Volatile.Read(ref _nextNodeId);

    /// <summary>Errors recorded while building the tree.</summary>
    public IReadOnlyCollection<ScanError> Errors => _errors;

    /// <summary>
    /// Adds an entry below <paramref name="parentId"/> and returns its node id.
    /// </summary>
    /// <param name="parentId">The parent node, or -1 to add a scan root.</param>
    /// <param name="depth">The entry's depth below the scan root.</param>
    /// <param name="entry">The entry to add.</param>
    public int AddEntry(int parentId, int depth, in DirectoryEntry entry)
    {
        int id = Interlocked.Increment(ref _nextNodeId) - 1;

        bool isDirectory = entry.IsDirectory;
        int directoryRow = -1;
        if (isDirectory)
        {
            directoryRow = Interlocked.Increment(ref _nextDirectoryCount) - 1;
        }

        EnsureCapacity(id + 1);

        NodeFlags flags = (NodeFlags)(ushort)0;
        if (isDirectory)
        {
            flags |= NodeFlags.IsDirectory;
        }

        if (entry.IsReparsePoint)
        {
            flags |= NodeFlags.IsReparsePoint;
        }

        if ((entry.Attributes & 0x2) != 0)
        {
            flags |= NodeFlags.IsHidden;
        }

        if ((entry.Attributes & 0x4) != 0)
        {
            flags |= NodeFlags.IsSystem;
        }

        if ((entry.Attributes & 0x1000) != 0)
        {
            flags |= NodeFlags.IsOffline;
        }

        if ((entry.Attributes & 0x200) != 0)
        {
            flags |= NodeFlags.IsSparse;
        }

        if ((entry.Attributes & 0x800) != 0)
        {
            flags |= NodeFlags.IsCompressed;
        }

        if ((entry.Attributes & 0x4000) != 0)
        {
            flags |= NodeFlags.IsEncrypted;
        }

        flags |= NodeFlags.SizeKnown;
        flags |= NodeFlags.AllocatedSizeKnown;

        var links = new NodeLinks
        {
            Parent = parentId,
            FirstChild = -1,
            NextSibling = -1,
            ChildCount = 0,
            Depth = (ushort)Math.Clamp(depth, 0, ushort.MaxValue),
            Flags = flags,
        };

        _arena.Links[id] = links;

        // The name is copied into the blob before the node is published, so a reader that
        // reaches the node through the tree always sees a complete name.
        _arena.NameOffset[id] = _names.Add(entry.NameUtf16);
        _arena.DirIndex[id] = directoryRow;
        _arena.Attributes[id] = entry.Attributes;
        _arena.LogicalSize[id] = entry.LogicalSize;
        _arena.AllocatedSize[id] = entry.AllocatedSize;
        _arena.FileId[id] = entry.FileId;
        _arena.LastWriteTicks[id] = entry.LastWriteTicks;
        _arena.CreationTicks[id] = entry.CreationTicks;
        _arena.AccessTicks[id] = entry.AccessTicks;

        if (parentId < 0)
        {
            lock (_rootLock)
            {
                _rootNodeIds.Add(id);
            }
        }
        else
        {
            LinkChild(parentId, id);
        }

        return id;
    }

    /// <summary>Records that a directory was enumerated to completion.</summary>
    public void MarkDirectoryScanned(int nodeId)
    {
        NodeLinks links = _arena.Links[nodeId];
        links.Flags |= NodeFlags.DirectoryScanned;
        _arena.Links[nodeId] = links;
    }

    /// <summary>Records an error that stopped a directory from being scanned.</summary>
    public void RecordError(ScanError error) => _errors.Enqueue(error);

    /// <summary>
    /// Stops the name store accepting new names. Called before a pass that only reads the
    /// tree, so name lookups no longer take a lock.
    /// </summary>
    public void FreezeNames() => _names.Freeze();

    /// <summary>Whether a node is a directory.</summary>
    public bool IsDirectory(int nodeId) => (_arena.Links[nodeId].Flags & NodeFlags.IsDirectory) != 0;

    /// <summary>The node's flags.</summary>
    public NodeFlags GetFlags(int nodeId) => _arena.Links[nodeId].Flags;

    /// <summary>The node's name.</summary>
    public string GetName(int nodeId) => _names.GetString(_arena.NameOffset[nodeId]);

    /// <summary>The full path of a node.</summary>
    public string GetPath(int nodeId)
    {
        var segments = new List<string>();
        int current = nodeId;

        while (current >= 0)
        {
            segments.Add(_names.GetString(_arena.NameOffset[current]));
            current = _arena.Links[current].Parent;
        }

        segments.Reverse();

        if (segments.Count == 1)
        {
            return segments[0];
        }

        var builder = new StringBuilder();
        builder.Append(segments[0]);

        for (int i = 1; i < segments.Count; i++)
        {
            builder.Append(Path.DirectorySeparatorChar);
            builder.Append(segments[i]);
        }

        return builder.ToString();
    }

    /// <summary>Records the space a file occupies on disk.</summary>
    public void SetAllocatedSize(int nodeId, long bytes)
    {
        _arena.AllocatedSize[nodeId] = bytes;

        NodeLinks links = _arena.Links[nodeId];
        links.Flags |= NodeFlags.AllocatedSizeKnown;
        _arena.Links[nodeId] = links;
    }

    /// <summary>Records how many hard links point at a file.</summary>
    public void SetLinkCount(int nodeId, ushort count) => _arena.LinkCount[nodeId] = count;

    /// <summary>Sets or clears a flag on a node.</summary>
    public void SetFlag(int nodeId, NodeFlags flag, bool value)
    {
        NodeLinks links = _arena.Links[nodeId];

        links.Flags = value
            ? links.Flags | flag
            : links.Flags & ~flag;

        _arena.Links[nodeId] = links;
    }

    /// <summary>
    /// Finalizes the tree: accumulates subtree totals and prepares the child index.
    /// </summary>
    public ScanTree Build(SortSpecification sort)
    {
        _names.Freeze();

        int nodeCount = Volatile.Read(ref _nextNodeId);
        int directoryCount = Volatile.Read(ref _nextDirectoryCount);

        int[] dirIndex = _arena.DirIndex.ToArray();
        var aggregateLogicalSize = new long[directoryCount];
        var aggregateAllocatedSize = new long[directoryCount];
        var aggregateFileCount = new int[directoryCount];
        var aggregateDirectoryCount = new int[directoryCount];

        // Seed each directory with its own size, so a directory's total already includes
        // whatever the file system reported for the directory entry itself.
        for (int id = 0; id < nodeCount; id++)
        {
            int row = dirIndex[id];
            if (row < 0)
            {
                continue;
            }

            aggregateLogicalSize[row] = _arena.LogicalSize[id];
            aggregateAllocatedSize[row] = _arena.AllocatedSize[id];
        }

        // A parent's id is always lower than its children's, so walking ids backwards is a
        // valid post-order: when a node is visited, all of its descendants have already
        // contributed to it.
        for (int id = nodeCount - 1; id >= 0; id--)
        {
            int parent = _arena.Links[id].Parent;
            if (parent < 0)
            {
                continue;
            }

            int parentRow = dirIndex[parent];
            if (parentRow < 0)
            {
                continue;
            }

            int row = dirIndex[id];
            if (row >= 0)
            {
                aggregateLogicalSize[parentRow] += aggregateLogicalSize[row];
                aggregateAllocatedSize[parentRow] += aggregateAllocatedSize[row];
                aggregateFileCount[parentRow] += aggregateFileCount[row];

                // The child contributes the directories below it, plus the child itself.
                aggregateDirectoryCount[parentRow] += aggregateDirectoryCount[row] + 1;
            }
            else
            {
                // A hard linked file has one name for each link. Counting every name would
                // report the same bytes several times, so only the first name found accounts
                // for the file; the others are shown but contribute nothing.
                NodeFlags flags = _arena.Links[id].Flags;
                bool alreadyCounted = (flags & NodeFlags.HasHardLinks) != 0
                    && (flags & NodeFlags.IsPrimaryLink) == 0;

                if (!alreadyCounted)
                {
                    aggregateLogicalSize[parentRow] += _arena.LogicalSize[id];
                    aggregateAllocatedSize[parentRow] += _arena.AllocatedSize[id];
                    aggregateFileCount[parentRow]++;
                }
            }
        }

        // Lay the children of every directory out contiguously, so a directory's children
        // can be re-sorted in place without disturbing any other directory.
        int totalChildren = 0;
        for (int id = 0; id < nodeCount; id++)
        {
            totalChildren += _arena.Links[id].ChildCount;
        }

        var childIndex = new int[totalChildren];
        var rangeStart = new int[directoryCount];
        var rangeCount = new int[directoryCount];

        int cursor = 0;
        for (int id = 0; id < nodeCount; id++)
        {
            int row = dirIndex[id];
            if (row < 0)
            {
                continue;
            }

            rangeStart[row] = cursor;

            int count = 0;
            for (int child = _arena.Links[id].FirstChild; child >= 0; child = _arena.Links[child].NextSibling)
            {
                childIndex[cursor + count] = child;
                count++;
            }

            rangeCount[row] = count;
            cursor += count;
        }

        var tree = new ScanTree(
            _arena,
            _names,
            dirIndex,
            aggregateLogicalSize,
            aggregateAllocatedSize,
            aggregateFileCount,
            aggregateDirectoryCount,
            childIndex,
            rangeStart,
            rangeCount,
            _rootNodeIds.ToArray(),
            sort);

        long totalLogical = 0;
        long totalAllocated = 0;
        int totalFiles = 0;
        int totalDirectories = 0;

        foreach (int rootId in _rootNodeIds)
        {
            int row = dirIndex[rootId];
            if (row < 0)
            {
                continue;
            }

            totalLogical += aggregateLogicalSize[row];
            totalAllocated += aggregateAllocatedSize[row];
            totalFiles += aggregateFileCount[row];
            totalDirectories += aggregateDirectoryCount[row];
        }

        tree.TotalLogicalSize = totalLogical;
        tree.TotalAllocatedSize = totalAllocated;
        tree.TotalFileCount = totalFiles;
        tree.TotalDirectoryCount = totalDirectories;

        return tree;
    }

    private void LinkChild(int parentId, int childId)
    {
        int first = Volatile.Read(ref _arena.Links[parentId].FirstChild);
        if (first < 0)
        {
            Interlocked.CompareExchange(ref _arena.Links[parentId].FirstChild, childId, -1);
        }
        else
        {
            // Publish the sibling link before the child becomes the head, so a reader that
            // sees the new head always sees the rest of the chain.
            Volatile.Write(ref _arena.Links[childId].NextSibling, first);
            Volatile.Write(ref _arena.Links[parentId].FirstChild, childId);
        }

        Interlocked.Increment(ref _arena.Links[parentId].ChildCount);

        NodeLinks links = _arena.Links[parentId];
        links.Flags |= NodeFlags.HasChildren;
        _arena.Links[parentId] = links;
    }

    private void EnsureCapacity(int count)
    {
        _arena.Links.EnsureCapacity(count);
        _arena.NameOffset.EnsureCapacity(count);
        _arena.DirIndex.EnsureCapacity(count);
        _arena.Attributes.EnsureCapacity(count);
        _arena.LogicalSize.EnsureCapacity(count);
        _arena.AllocatedSize.EnsureCapacity(count);
        _arena.FileId.EnsureCapacity(count);
        _arena.LinkCount.EnsureCapacity(count);
        _arena.LastWriteTicks.EnsureCapacity(count);
        _arena.CreationTicks.EnsureCapacity(count);
        _arena.AccessTicks.EnsureCapacity(count);
    }
}
