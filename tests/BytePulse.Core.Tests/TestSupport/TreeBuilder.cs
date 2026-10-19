using System.Text;
using BytePulse.Core.Scanning;

namespace BytePulse.Core.Tests.TestSupport;

/// <summary>Helpers for building trees in tests.</summary>
internal static class TreeBuilder
{
    /// <summary>Creates a directory entry with the given name.</summary>
    public static DirectoryEntry Directory(string name, long size = 0) =>
        new(Encoding.Unicode.GetBytes(name), size, size, 0x10, 0, 0, 0, 0, true, false);

    /// <summary>Creates a file entry with the given name and size.</summary>
    public static DirectoryEntry File(string name, long size) =>
        new(Encoding.Unicode.GetBytes(name), size, size, 0x80, 0, 0, 0, 0, false, false);

    /// <summary>Creates a reparse point entry.</summary>
    public static DirectoryEntry ReparsePoint(string name, bool isDirectory) =>
        new(Encoding.Unicode.GetBytes(name), 0, 0, 0x400, 0, 0, 0, 0, isDirectory, true);

    /// <summary>
    /// A plain object tree used as the reference model the columnar tree is checked against.
    /// </summary>
    internal sealed class ModelNode
    {
        public required string Name { get; init; }

        public long Size { get; init; }

        public bool IsDirectory { get; init; }

        public List<ModelNode> Children { get; } = new();

        public long TotalSize { get; private set; }

        public int TotalFiles { get; private set; }

        public int TotalDirectories { get; private set; }

        /// <summary>
        /// Recomputes the subtree totals from the leaves upwards.
        /// </summary>
        /// <remarks>
        /// The totals follow the same convention as the columnar tree: a directory's counts
        /// describe what is below it and never include the directory itself.
        /// </remarks>
        public void ComputeTotals()
        {
            TotalSize = Size;
            TotalFiles = IsDirectory ? 0 : 1;
            TotalDirectories = 0;

            foreach (ModelNode child in Children)
            {
                child.ComputeTotals();
                TotalSize += child.TotalSize;
                TotalFiles += child.TotalFiles;
                TotalDirectories += child.TotalDirectories + (child.IsDirectory ? 1 : 0);
            }
        }
    }

    /// <summary>
    /// Builds a random tree of directories and files with the given seed, and mirrors it
    /// into the columnar builder as a single root.
    /// </summary>
    public static ModelNode BuildRandom(ScanTreeBuilder builder, Random random, int depth, int breadth)
    {
        ModelNode model = BuildModel(random, depth, breadth);
        model.ComputeTotals();

        int rootId = Mirror(builder, model, -1, 0);
        return model;
    }

    /// <summary>Builds the reference model without touching the columnar builder.</summary>
    public static ModelNode BuildModel(Random random, int depth, int breadth)
    {
        var node = new ModelNode
        {
            Name = NextName(random),
            Size = random.Next(0, 4096),
            IsDirectory = true,
        };

        int childCount = random.Next(0, breadth + 1);
        for (int i = 0; i < childCount; i++)
        {
            ModelNode child = random.Next(0, 100) < 30 && depth < 4
                ? BuildModel(random, depth + 1, breadth)
                : new ModelNode
                {
                    Name = NextName(random),
                    Size = random.Next(0, 1 << 20),
                    IsDirectory = false,
                };

            node.Children.Add(child);
        }

        return node;
    }

    private static int _nameCounter;

    /// <summary>Produces a name that is unique across the whole model.</summary>
    private static string NextName(Random random) => $"n{random.Next()}_{Interlocked.Increment(ref _nameCounter)}";

    /// <summary>Copies a reference model into the columnar builder and returns the root id.</summary>
    public static int Mirror(ScanTreeBuilder builder, ModelNode node, int parentId, int depth)
    {
        int nodeId = builder.AddEntry(
            parentId,
            depth,
            node.IsDirectory ? Directory(node.Name, node.Size) : File(node.Name, node.Size));

        foreach (ModelNode child in node.Children)
        {
            Mirror(builder, child, nodeId, depth + 1);
        }

        return nodeId;
    }
}
