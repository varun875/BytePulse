namespace BytePulse.Core.Model;

/// <summary>
/// One row of the flattened tree shown in the UI.
/// </summary>
/// <remarks>
/// WinUI's <c>TreeView</c> control does not cope with large item counts, so the tree is
/// flattened into a plain list of rows instead. A row is a value type, and the list only
/// ever holds the rows that are actually visible.
/// </remarks>
public readonly record struct VisibleRow(int NodeId, int Depth, bool IsDirectory, bool HasChildren, bool IsExpanded);
