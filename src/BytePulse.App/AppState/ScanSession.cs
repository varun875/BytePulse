using BytePulse.Core.Model;

namespace BytePulse.AppState;

/// <summary>
/// The scan the pages share.
/// </summary>
/// <remarks>
/// The pages are separate frames, so the tree they work with is kept here rather than
/// being passed through navigation. There is only ever one scan at a time, so a single
/// slot is enough.
/// </remarks>
internal static class ScanSession
{
    /// <summary>The tree from the last completed scan, if there is one.</summary>
    public static ScanTree? Tree { get; private set; }

    /// <summary>The folder the map is currently showing.</summary>
    public static int CurrentNodeId { get; private set; } = -1;

    /// <summary>The path that was scanned.</summary>
    public static string RootPath { get; private set; } = string.Empty;

    /// <summary>Raised when the session changes.</summary>
    public static event Action? Changed;

    /// <summary>Records a completed scan.</summary>
    public static void Set(ScanTree tree, string rootPath)
    {
        Tree = tree;
        RootPath = rootPath;
        CurrentNodeId = tree.RootNodeIds.Count > 0 ? tree.RootNodeIds[0] : -1;
        Changed?.Invoke();
    }

    /// <summary>Moves the map to a folder.</summary>
    public static void MoveTo(int nodeId)
    {
        CurrentNodeId = nodeId;
        Changed?.Invoke();
    }

    /// <summary>Moves the map to the folder above the current one.</summary>
    public static void MoveUp()
    {
        if (Tree is null || CurrentNodeId < 0)
        {
            return;
        }

        ScanNode node = Tree.GetNode(CurrentNodeId);

        if (node.ParentId >= 0)
        {
            CurrentNodeId = node.ParentId;
            Changed?.Invoke();
        }
    }
}
