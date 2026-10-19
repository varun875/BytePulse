using System.Collections.Specialized;

namespace BytePulse.Core.Model;

/// <summary>
/// The flattened view of a tree: the rows that are currently visible.
/// </summary>
/// <remarks>
/// <para>
/// WinUI's <c>TreeView</c> control does not cope with large item counts, so the tree is
/// flattened into a plain list of rows instead. Only the rows that are on screen exist, and
/// expanding a folder splices its children into the list rather than rebuilding it.
/// </para>
/// <para>
/// The model raises a single change notification per user action, so a virtualized list
/// bound to it only re-renders the part that actually changed.
/// </para>
/// </remarks>
public sealed class VisibleRowModel
{
    private readonly ScanTree _tree;
    private readonly List<VisibleRow> _rows = new();
    private readonly HashSet<int> _expanded = new();

    /// <summary>Raised when the visible rows change.</summary>
    public event NotifyCollectionChangedEventHandler? CollectionChanged;

    /// <summary>Creates a model over a tree, with every root expanded.</summary>
    public VisibleRowModel(ScanTree tree)
    {
        _tree = tree;

        foreach (int root in tree.RootNodeIds)
        {
            _expanded.Add(root);
        }

        Rebuild();
    }

    /// <summary>The number of visible rows.</summary>
    public int Count => _rows.Count;

    /// <summary>The row at an index.</summary>
    public VisibleRow this[int index] => _rows[index];

    /// <summary>Whether a node is currently expanded.</summary>
    public bool IsExpanded(int nodeId) => _expanded.Contains(nodeId);

    /// <summary>
    /// Rebuilds every row from the tree, keeping the current expansion state.
    /// </summary>
    /// <remarks>
    /// This is what a change of sort order requires: the tree re-sorts lazily, so the rows
    /// have to be re-read to pick up the new order.
    /// </remarks>
    public void Rebuild()
    {
        _rows.Clear();

        foreach (int root in _tree.RootNodeIds)
        {
            AppendRow(root, 0);
        }

        CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }

    /// <summary>Expands a node, revealing its children.</summary>
    public void Expand(int nodeId)
    {
        int index = IndexOf(nodeId);
        if (index < 0 || !_tree.IsDirectory(nodeId))
        {
            return;
        }

        bool wasExpanded = _expanded.Contains(nodeId);
        _expanded.Add(nodeId);

        if (wasExpanded)
        {
            ReplaceRow(index, nodeId);
            return;
        }

        var inserted = new List<VisibleRow>();
        AppendChildren(nodeId, index + 1, inserted);
        _rows.InsertRange(index + 1, inserted);

        ReplaceRow(index, nodeId);

        if (inserted.Count > 0)
        {
            CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(
                NotifyCollectionChangedAction.Add,
                inserted,
                index + 1));
        }
    }

    /// <summary>Collapses a node, hiding its children.</summary>
    public void Collapse(int nodeId)
    {
        int index = IndexOf(nodeId);
        if (index < 0)
        {
            return;
        }

        _expanded.Remove(nodeId);

        int depth = _rows[index].Depth;
        int end = index + 1;
        while (end < _rows.Count && _rows[end].Depth > depth)
        {
            end++;
        }

        if (end > index + 1)
        {
            var removed = _rows.GetRange(index + 1, end - index - 1);
            _rows.RemoveRange(index + 1, end - index - 1);

            CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(
                NotifyCollectionChangedAction.Remove,
                removed,
                index + 1));
        }

        ReplaceRow(index, nodeId);
    }

    /// <summary>Expands a node if it is collapsed, and collapses it if it is expanded.</summary>
    public void Toggle(int nodeId)
    {
        if (_expanded.Contains(nodeId))
        {
            Collapse(nodeId);
        }
        else
        {
            Expand(nodeId);
        }
    }

    private void AppendRow(int nodeId, int depth)
    {
        bool isDirectory = _tree.IsDirectory(nodeId);
        bool hasChildren = _tree.GetChildCount(nodeId) > 0;
        bool expanded = _expanded.Contains(nodeId);

        _rows.Add(new VisibleRow(nodeId, depth, isDirectory, hasChildren, expanded));

        if (expanded && isDirectory)
        {
            AppendChildren(nodeId, depth + 1, _rows);
        }
    }

    private void AppendChildren(int nodeId, int depth, List<VisibleRow> destination)
    {
        foreach (int child in _tree.GetSortedChildren(nodeId))
        {
            bool isDirectory = _tree.IsDirectory(child);
            bool hasChildren = _tree.GetChildCount(child) > 0;
            bool expanded = _expanded.Contains(child);

            destination.Add(new VisibleRow(child, depth, isDirectory, hasChildren, expanded));

            if (expanded && isDirectory)
            {
                AppendChildren(child, depth + 1, destination);
            }
        }
    }

    private void ReplaceRow(int index, int nodeId)
    {
        VisibleRow previous = _rows[index];
        _rows[index] = previous with { IsExpanded = _expanded.Contains(nodeId) };

        CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(
            NotifyCollectionChangedAction.Replace,
            _rows[index],
            previous,
            index));
    }

    private int IndexOf(int nodeId)
    {
        for (int i = 0; i < _rows.Count; i++)
        {
            if (_rows[i].NodeId == nodeId)
            {
                return i;
            }
        }

        return -1;
    }
}
