using BytePulse.Core.Model;
using BytePulse.Core.Scanning;
using BytePulse.Core.Tests.TestSupport;
using Xunit;

namespace BytePulse.Core.Tests.Model;

public sealed class ScanTreeSortTests
{
    [Fact]
    public void OrdersDirectoriesBySubtreeSizeDescending()
    {
        var builder = new ScanTreeBuilder();
        int root = builder.AddEntry(-1, 0, TreeBuilder.Directory("root"));

        int small = builder.AddEntry(root, 1, TreeBuilder.Directory("small"));
        builder.AddEntry(small, 2, TreeBuilder.File("a.bin", 10));

        int medium = builder.AddEntry(root, 1, TreeBuilder.Directory("medium"));
        builder.AddEntry(medium, 2, TreeBuilder.File("b.bin", 1000));

        int large = builder.AddEntry(root, 1, TreeBuilder.Directory("large"));
        builder.AddEntry(large, 2, TreeBuilder.File("c.bin", 100_000));

        ScanTree tree = builder.Build(SortSpecification.Default);

        Assert.Equal(new[] { "large", "medium", "small" }, NamesOf(tree, root));
    }

    [Fact]
    public void PutsDirectoriesBeforeFiles()
    {
        var builder = new ScanTreeBuilder();
        int root = builder.AddEntry(-1, 0, TreeBuilder.Directory("root"));

        builder.AddEntry(root, 1, TreeBuilder.File("zzz.bin", 999_999));
        builder.AddEntry(root, 1, TreeBuilder.Directory("aaa"));

        ScanTree tree = builder.Build(SortSpecification.Default);

        Assert.Equal(new[] { "aaa", "zzz.bin" }, NamesOf(tree, root));
    }

    [Fact]
    public void SortingByNameIsCaseInsensitive()
    {
        var builder = new ScanTreeBuilder();
        int root = builder.AddEntry(-1, 0, TreeBuilder.Directory("root"));

        builder.AddEntry(root, 1, TreeBuilder.File("banana.bin", 1));
        builder.AddEntry(root, 1, TreeBuilder.File("Apple.bin", 1));
        builder.AddEntry(root, 1, TreeBuilder.File("cherry.bin", 1));

        ScanTree tree = builder.Build(new SortSpecification
        {
            Key = SortKey.Name,
            Direction = SortDirection.Ascending,
            DirectoriesFirst = true,
        });

        Assert.Equal(new[] { "Apple.bin", "banana.bin", "cherry.bin" }, NamesOf(tree, root));
    }

    [Fact]
    public void ReSortingChangesTheOrderWithoutChangingNodeIds()
    {
        var builder = new ScanTreeBuilder();
        int root = builder.AddEntry(-1, 0, TreeBuilder.Directory("root"));
        int first = builder.AddEntry(root, 1, TreeBuilder.File("a.bin", 10));
        int second = builder.AddEntry(root, 1, TreeBuilder.File("b.bin", 20));

        ScanTree tree = builder.Build(SortSpecification.Default);
        Assert.Equal(new[] { "b.bin", "a.bin" }, NamesOf(tree, root));

        tree.ReSort(new SortSpecification
        {
            Key = SortKey.LogicalSize,
            Direction = SortDirection.Ascending,
            DirectoriesFirst = true,
        });

        Assert.Equal(new[] { "a.bin", "b.bin" }, NamesOf(tree, root));

        // The node ids are untouched, so a selection survives the change of order.
        Assert.Equal("a.bin", tree.GetName(first));
        Assert.Equal("b.bin", tree.GetName(second));
    }

    [Fact]
    public void SortingIsDeterministic()
    {
        var builder = new ScanTreeBuilder();
        int root = builder.AddEntry(-1, 0, TreeBuilder.Directory("root"));

        for (int i = 0; i < 50; i++)
        {
            builder.AddEntry(root, 1, TreeBuilder.File($"file{i % 5}.bin", 100));
        }

        ScanTree tree = builder.Build(SortSpecification.Default);
        string[] first = NamesOf(tree, root);

        tree.ReSort(SortSpecification.Default);
        string[] second = NamesOf(tree, root);

        Assert.Equal(first, second);
    }

    [Fact]
    public void SortsAreATotalOrder()
    {
        var builder = new ScanTreeBuilder();
        int root = builder.AddEntry(-1, 0, TreeBuilder.Directory("root"));

        for (int i = 0; i < 20; i++)
        {
            builder.AddEntry(root, 1, TreeBuilder.File($"same{i}.bin", 500));
        }

        ScanTree tree = builder.Build(SortSpecification.Default);
        int[] children = tree.GetSortedChildren(root).ToArray();

        // Every entry has the same size, so the name decides, and no two entries compare equal.
        for (int i = 0; i < children.Length; i++)
        {
            for (int j = i + 1; j < children.Length; j++)
            {
                int comparison = string.Compare(
                    tree.GetName(children[i]),
                    tree.GetName(children[j]),
                    StringComparison.OrdinalIgnoreCase);

                Assert.True(comparison < 0, $"expected {tree.GetName(children[i])} before {tree.GetName(children[j])}");
            }
        }
    }

    [Fact]
    public void SortingOneDirectoryLeavesOtherDirectoriesAlone()
    {
        var builder = new ScanTreeBuilder();
        int root = builder.AddEntry(-1, 0, TreeBuilder.Directory("root"));

        int first = builder.AddEntry(root, 1, TreeBuilder.Directory("first"));
        builder.AddEntry(first, 2, TreeBuilder.File("a.bin", 10));
        builder.AddEntry(first, 2, TreeBuilder.File("b.bin", 20));

        int second = builder.AddEntry(root, 1, TreeBuilder.Directory("second"));
        builder.AddEntry(second, 2, TreeBuilder.File("c.bin", 30));
        builder.AddEntry(second, 2, TreeBuilder.File("d.bin", 40));

        ScanTree tree = builder.Build(SortSpecification.Default);

        tree.ReSort(new SortSpecification
        {
            Key = SortKey.LogicalSize,
            Direction = SortDirection.Ascending,
            DirectoriesFirst = true,
        });

        Assert.Equal(new[] { "a.bin", "b.bin" }, NamesOf(tree, first));
        Assert.Equal(new[] { "c.bin", "d.bin" }, NamesOf(tree, second));
    }

    [Fact]
    public void SortsByLastWriteTime()
    {
        var builder = new ScanTreeBuilder();
        int root = builder.AddEntry(-1, 0, TreeBuilder.Directory("root"));

        const long earlier = 1_000_000_000_000_000;
        const long later = 1_700_000_000_000_000;

        builder.AddEntry(root, 1, new DirectoryEntry(
            System.Text.Encoding.Unicode.GetBytes("older.bin"),
            LogicalSize: 1,
            AllocatedSize: 1,
            Attributes: 0x80,
            FileId: 0,
            LastWriteTicks: earlier,
            CreationTicks: 0,
            AccessTicks: 0,
            IsDirectory: false,
            IsReparsePoint: false));

        builder.AddEntry(root, 1, new DirectoryEntry(
            System.Text.Encoding.Unicode.GetBytes("newer.bin"),
            LogicalSize: 1,
            AllocatedSize: 1,
            Attributes: 0x80,
            FileId: 0,
            LastWriteTicks: later,
            CreationTicks: 0,
            AccessTicks: 0,
            IsDirectory: false,
            IsReparsePoint: false));

        ScanTree tree = builder.Build(new SortSpecification
        {
            Key = SortKey.LastWriteTime,
            Direction = SortDirection.Descending,
            DirectoriesFirst = true,
        });

        Assert.Equal(new[] { "newer.bin", "older.bin" }, NamesOf(tree, root));
    }

    private static string[] NamesOf(ScanTree tree, int parentId)
    {
        var names = new List<string>();
        foreach (int childId in tree.GetSortedChildren(parentId))
        {
            names.Add(tree.GetName(childId));
        }

        return names.ToArray();
    }
}
