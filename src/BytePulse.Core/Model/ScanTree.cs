using System.Text;
using BytePulse.Core.Storage;

namespace BytePulse.Core.Model;

/// <summary>
/// An immutable snapshot of a scanned file system.
/// </summary>
/// <remarks>
/// <para>
/// The tree is stored columnar, so a node's id is its index and every lookup is an array
/// access. Ids are stable: re-sorting a directory never changes a node's id, so a selection
/// or a highlighted row survives a change of sort order.
/// </para>
/// <para>
/// Subtree totals are accumulated in a single pass over the nodes in reverse id order,
/// which is a valid post-order because a parent's id is always lower than its children's.
/// </para>
/// </remarks>
public sealed class ScanTree
{
    private readonly NodeArena _arena;
    private readonly NameStore _names;
    private readonly int[] _dirIndex;
    private readonly long[] _aggregateLogicalSize;
    private readonly long[] _aggregateAllocatedSize;
    private readonly int[] _aggregateFileCount;
    private readonly int[] _aggregateDirectoryCount;
    private readonly int[] _childIndex;
    private readonly int[] _rangeStart;
    private readonly int[] _rangeCount;
    private readonly int[] _sortedGeneration;
    private readonly int[] _rootNodeIds;

    private int _sortGeneration;
    private SortSpecification _sort;

    internal ScanTree(
        NodeArena arena,
        NameStore names,
        int[] dirIndex,
        long[] aggregateLogicalSize,
        long[] aggregateAllocatedSize,
        int[] aggregateFileCount,
        int[] aggregateDirectoryCount,
        int[] childIndex,
        int[] rangeStart,
        int[] rangeCount,
        int[] rootNodeIds,
        SortSpecification sort)
    {
        _arena = arena;
        _names = names;
        _dirIndex = dirIndex;
        _aggregateLogicalSize = aggregateLogicalSize;
        _aggregateAllocatedSize = aggregateAllocatedSize;
        _aggregateFileCount = aggregateFileCount;
        _aggregateDirectoryCount = aggregateDirectoryCount;
        _childIndex = childIndex;
        _rangeStart = rangeStart;
        _rangeCount = rangeCount;
        _rootNodeIds = rootNodeIds;
        _sort = sort;
        _sortedGeneration = new int[rangeStart.Length];
        Array.Fill(_sortedGeneration, -1);
    }

    /// <summary>The number of nodes in the tree.</summary>
    public int NodeCount => _dirIndex.Length;

    /// <summary>The nodes that were scanned as roots.</summary>
    public IReadOnlyList<int> RootNodeIds => _rootNodeIds;

    /// <summary>The total logical size of every scanned root, in bytes.</summary>
    public long TotalLogicalSize { get; internal set; }

    /// <summary>The total allocated size of every scanned root, in bytes.</summary>
    public long TotalAllocatedSize { get; internal set; }

    /// <summary>The total number of files across every scanned root.</summary>
    public int TotalFileCount { get; internal set; }

    /// <summary>The total number of directories across every scanned root.</summary>
    public int TotalDirectoryCount { get; internal set; }

    /// <summary>The sort currently in effect.</summary>
    public SortSpecification Sort => _sort;

    /// <summary>Materializes a node for display.</summary>
    public ScanNode GetNode(int nodeId)
    {
        NodeLinks links = _arena.Links[nodeId];
        int row = _dirIndex[nodeId];
        bool isDirectory = (links.Flags & NodeFlags.IsDirectory) != 0;

        long totalLogical = isDirectory && row >= 0 ? _aggregateLogicalSize[row] : _arena.LogicalSize[nodeId];
        long totalAllocated = isDirectory && row >= 0 ? _aggregateAllocatedSize[row] : _arena.AllocatedSize[nodeId];
        int totalFiles = isDirectory ? (row >= 0 ? _aggregateFileCount[row] : 0) : 1;
        int totalDirectories = isDirectory && row >= 0 ? _aggregateDirectoryCount[row] : 0;

        return new ScanNode
        {
            Id = nodeId,
            ParentId = links.Parent,
            Depth = links.Depth,
            Flags = links.Flags,
            Name = _names.GetString(_arena.NameOffset[nodeId]),
            LogicalSize = _arena.LogicalSize[nodeId],
            AllocatedSize = _arena.AllocatedSize[nodeId],
            TotalLogicalSize = totalLogical,
            TotalAllocatedSize = totalAllocated,
            TotalFileCount = totalFiles,
            TotalDirectoryCount = totalDirectories,
            LastWriteTimeUtc = new DateTimeOffset(_arena.LastWriteTicks[nodeId], TimeSpan.Zero),
            FileId = _arena.FileId[nodeId],
            LinkCount = _arena.LinkCount[nodeId],
        };
    }

    /// <summary>The node's name.</summary>
    public string GetName(int nodeId) => _names.GetString(_arena.NameOffset[nodeId]);

    /// <summary>The full path of the node, built from the names of its ancestors.</summary>
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

        var builder = new StringBuilder();
        for (int i = 0; i < segments.Count; i++)
        {
            if (i == 0)
            {
                builder.Append(segments[i]);
                continue;
            }

            builder.Append(Path.DirectorySeparatorChar);
            builder.Append(segments[i]);
        }

        return builder.ToString();
    }

    /// <summary>The number of direct children of a node.</summary>
    public int GetChildCount(int nodeId) => _arena.Links[nodeId].ChildCount;

    /// <summary>Whether a node is a directory.</summary>
    public bool IsDirectory(int nodeId) => (_arena.Links[nodeId].Flags & NodeFlags.IsDirectory) != 0;

    /// <summary>
    /// The children of a directory, in the current sort order.
    /// </summary>
    /// <remarks>
    /// The returned span points into the tree's own storage, so reading it allocates
    /// nothing. The order is materialized on first use after a sort change, so a directory
    /// that is never displayed is never sorted.
    /// </remarks>
    public ReadOnlySpan<int> GetSortedChildren(int parentNodeId)
    {
        int row = _dirIndex[parentNodeId];
        if (row < 0)
        {
            return default;
        }

        EnsureSorted(row);
        return _childIndex.AsSpan(_rangeStart[row], _rangeCount[row]);
    }

    /// <summary>
    /// Changes the sort order. Directories are re-sorted the next time their children are
    /// requested, so the change is cheap regardless of how large the tree is.
    /// </summary>
    public void ReSort(SortSpecification sort)
    {
        _sort = sort;
        _sortGeneration++;
    }

    private void EnsureSorted(int row)
    {
        if (_sortedGeneration[row] == _sortGeneration)
        {
            return;
        }

        int start = _rangeStart[row];
        int count = _rangeCount[row];

        if (count > 1)
        {
            var comparer = new NodeOrderComparer(
                _arena,
                _dirIndex,
                _aggregateLogicalSize,
                _aggregateAllocatedSize,
                _aggregateFileCount,
                _aggregateDirectoryCount,
                _names,
                _sort);

            Array.Sort(_childIndex, start, count, comparer);
        }

        _sortedGeneration[row] = _sortGeneration;
    }
}
