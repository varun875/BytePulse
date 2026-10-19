using BytePulse.Core.Model;
using BytePulse.Core.Scanning;
using BytePulse.Core.Tests.TestSupport;
using Xunit;

namespace BytePulse.Core.Tests.Model;

public sealed class ScanTreeAggregationTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(12345)]
    public void TotalsMatchTheReferenceModel(int seed)
    {
        var builder = new ScanTreeBuilder();
        var random = new Random(seed);

        TreeBuilder.ModelNode model = TreeBuilder.BuildRandom(builder, random, 0, 4);
        model.ComputeTotals();

        ScanTree tree = builder.Build(SortSpecification.Default);

        Assert.Equal(model.TotalSize, tree.TotalLogicalSize);
        Assert.Equal(model.TotalFiles, tree.TotalFileCount);
        Assert.Equal(model.TotalDirectories, tree.TotalDirectoryCount);
    }

    [Fact]
    public void TotalsMatchTheReferenceModelForEveryNode()
    {
        var builder = new ScanTreeBuilder();
        var random = new Random(99);

        TreeBuilder.ModelNode model = TreeBuilder.BuildRandom(builder, random, 0, 3);
        model.ComputeTotals();

        ScanTree tree = builder.Build(SortSpecification.Default);

        // Walk the model and the columnar tree side by side, matching children by name.
        AssertNodesMatch(model, tree, tree.RootNodeIds[0]);
    }

    [Fact]
    public void CountsADirectorysOwnSize()
    {
        var builder = new ScanTreeBuilder();
        builder.AddEntry(-1, 0, TreeBuilder.Directory("root", 4096));

        ScanTree tree = builder.Build(SortSpecification.Default);

        Assert.Equal(4096, tree.TotalLogicalSize);
    }

    [Fact]
    public void HandlesAnEmptyTree()
    {
        var builder = new ScanTreeBuilder();

        ScanTree tree = builder.Build(SortSpecification.Default);

        Assert.Equal(0, tree.NodeCount);
        Assert.Equal(0, tree.TotalLogicalSize);
        Assert.Equal(0, tree.TotalFileCount);
        Assert.Empty(tree.RootNodeIds);
    }

    [Fact]
    public void ReportsTheTotalSizeOnDiskAlongsideTheLogicalSize()
    {
        var builder = new ScanTreeBuilder();
        int root = builder.AddEntry(-1, 0, TreeBuilder.Directory("root"));
        builder.AddEntry(root, 1, new DirectoryEntry(
            System.Text.Encoding.Unicode.GetBytes("sparse.bin"),
            LogicalSize: 1024 * 1024,
            AllocatedSize: 4096,
            Attributes: 0x80,
            FileId: 0,
            LastWriteTicks: 0,
            CreationTicks: 0,
            AccessTicks: 0,
            IsDirectory: false,
            IsReparsePoint: false));

        ScanTree tree = builder.Build(SortSpecification.Default);

        Assert.Equal(1024 * 1024, tree.TotalLogicalSize);
        Assert.Equal(4096, tree.TotalAllocatedSize);
    }

    [Fact]
    public void BuildsAMillionNodeTreeWithinTheMemoryBudget()
    {
        var builder = new ScanTreeBuilder();
        const int directoryCount = 1000;
        const int filesPerDirectory = 999;

        for (int d = 0; d < directoryCount; d++)
        {
            int directory = builder.AddEntry(-1, 0, TreeBuilder.Directory($"dir{d}"));
            for (int f = 0; f < filesPerDirectory; f++)
            {
                builder.AddEntry(directory, 1, TreeBuilder.File($"file{d}_{f}.dat", 1024));
            }
        }

        long before = GC.GetTotalAllocatedBytes(precise: true);
        ScanTree tree = builder.Build(SortSpecification.Default);
        long after = GC.GetTotalAllocatedBytes(precise: true);

        Assert.Equal(directoryCount * (filesPerDirectory + 1), tree.NodeCount);
        Assert.Equal(directoryCount * filesPerDirectory, tree.TotalFileCount);

        // The budget is 110 bytes per node excluding names. Names are stored in a separate
        // blob, so this is a generous ceiling for the fixed cost of the tree itself.
        long budget = (long)tree.NodeCount * 110;
        Assert.True(after - before < budget, $"tree used {after - before} bytes, budget is {budget}");
    }

    private static void AssertNodesMatch(TreeBuilder.ModelNode model, ScanTree tree, int nodeId)
    {
        ScanNode node = tree.GetNode(nodeId);

        Assert.Equal(model.Name, node.Name);
        Assert.Equal(model.TotalSize, node.TotalLogicalSize);
        Assert.Equal(model.TotalFiles, node.TotalFileCount);
        Assert.Equal(model.TotalDirectories, node.TotalDirectoryCount);

        int childCount = 0;
        foreach (int childId in tree.GetSortedChildren(nodeId))
        {
            TreeBuilder.ModelNode? match = model.Children.FirstOrDefault(c => c.Name == tree.GetName(childId));
            Assert.NotNull(match);
            AssertNodesMatch(match!, tree, childId);
            childCount++;
        }

        Assert.Equal(model.Children.Count, childCount);
    }
}
