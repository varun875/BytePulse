using BytePulse.Core.Storage;

namespace BytePulse.Core.Model;

/// <summary>
/// Orders sibling nodes by a <see cref="SortSpecification"/>.
/// </summary>
/// <remarks>
/// The comparer reads the arena's columns directly instead of materializing
/// <see cref="ScanNode"/> values, so sorting a large tree allocates nothing. Directories are
/// compared by their subtree totals, which is what a user means by "size" of a folder.
/// </remarks>
internal readonly struct NodeOrderComparer : IComparer<int>
{
    private readonly NodeArena _arena;
    private readonly int[] _dirIndex;
    private readonly long[] _aggregateLogicalSize;
    private readonly long[] _aggregateAllocatedSize;
    private readonly int[] _aggregateFileCount;
    private readonly int[] _aggregateDirectoryCount;
    private readonly NameStore _names;
    private readonly SortSpecification _sort;

    internal NodeOrderComparer(
        NodeArena arena,
        int[] dirIndex,
        long[] aggregateLogicalSize,
        long[] aggregateAllocatedSize,
        int[] aggregateFileCount,
        int[] aggregateDirectoryCount,
        NameStore names,
        SortSpecification sort)
    {
        _arena = arena;
        _dirIndex = dirIndex;
        _aggregateLogicalSize = aggregateLogicalSize;
        _aggregateAllocatedSize = aggregateAllocatedSize;
        _aggregateFileCount = aggregateFileCount;
        _aggregateDirectoryCount = aggregateDirectoryCount;
        _names = names;
        _sort = sort;
    }

    public int Compare(int x, int y)
    {
        if (x == y)
        {
            return 0;
        }

        NodeFlags xFlags = _arena.Links[x].Flags;
        NodeFlags yFlags = _arena.Links[y].Flags;
        bool xIsDirectory = (xFlags & NodeFlags.IsDirectory) != 0;
        bool yIsDirectory = (yFlags & NodeFlags.IsDirectory) != 0;

        if (_sort.DirectoriesFirst && xIsDirectory != yIsDirectory)
        {
            return xIsDirectory ? -1 : 1;
        }

        int result = _sort.Key switch
        {
            SortKey.Name => CompareNames(x, y),
            SortKey.LogicalSize => CompareLong(
                SizeValue(x, xIsDirectory, _aggregateLogicalSize, _arena.LogicalSize),
                SizeValue(y, yIsDirectory, _aggregateLogicalSize, _arena.LogicalSize)),
            SortKey.AllocatedSize => CompareLong(
                SizeValue(x, xIsDirectory, _aggregateAllocatedSize, _arena.AllocatedSize),
                SizeValue(y, yIsDirectory, _aggregateAllocatedSize, _arena.AllocatedSize)),
            SortKey.FileCount => CompareLong(
                FileCountValue(x, xIsDirectory),
                FileCountValue(y, yIsDirectory)),
            SortKey.DirectoryCount => CompareLong(
                DirectoryCountValue(x, xIsDirectory),
                DirectoryCountValue(y, yIsDirectory)),
            SortKey.LastWriteTime => _arena.LastWriteTicks[x].CompareTo(_arena.LastWriteTicks[y]),
            _ => 0,
        };

        if (result != 0)
        {
            return _sort.Direction == SortDirection.Descending ? -result : result;
        }

        // Tie-break on the name so the order is stable and human-predictable, then on the
        // node id so the order is total.
        result = CompareNames(x, y);
        if (result != 0)
        {
            return result;
        }

        return x.CompareTo(y);
    }

    private int CompareNames(int x, int y) =>
        _names.CompareNames(_arena.NameOffset[x], _arena.NameOffset[y]);

    private long SizeValue(int nodeId, bool isDirectory, long[] aggregate, ChunkedColumn<long> ownSize)
    {
        if (!isDirectory)
        {
            return ownSize[nodeId];
        }

        int row = _dirIndex[nodeId];
        return row < 0 ? 0 : aggregate[row];
    }

    private long FileCountValue(int nodeId, bool isDirectory)
    {
        if (!isDirectory)
        {
            return 1;
        }

        int row = _dirIndex[nodeId];
        return row < 0 ? 0 : _aggregateFileCount[row];
    }

    private long DirectoryCountValue(int nodeId, bool isDirectory)
    {
        if (!isDirectory)
        {
            return 0;
        }

        int row = _dirIndex[nodeId];
        return row < 0 ? 0 : _aggregateDirectoryCount[row];
    }

    private static int CompareLong(long a, long b) => a.CompareTo(b);
}
