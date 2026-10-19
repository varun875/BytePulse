using System.Collections.Specialized;
using BytePulse.Core.Model;
using BytePulse.Core.Scanning;
using BytePulse.Core.Tests.TestSupport;
using Xunit;

namespace BytePulse.Core.Tests.Model;

public sealed class VisibleRowModelTests
{
    /// <summary>
    /// Builds this tree, where the root is expanded to begin with:
    /// <code>
    /// root
    ///   alpha
    ///     sub
    ///       deep.bin
    ///   beta.bin
    ///   gamma.bin
    /// </code>
    /// </summary>
    private static (ScanTree Tree, int RootId, int AlphaId, int SubId) BuildTree()
    {
        var builder = new ScanTreeBuilder();
        int root = builder.AddEntry(-1, 0, TreeBuilder.Directory("root"));

        int alpha = builder.AddEntry(root, 1, TreeBuilder.Directory("alpha"));
        int sub = builder.AddEntry(alpha, 2, TreeBuilder.Directory("sub"));
        builder.AddEntry(sub, 3, TreeBuilder.File("deep.bin", 10));

        builder.AddEntry(root, 1, TreeBuilder.File("beta.bin", 20));
        builder.AddEntry(root, 1, TreeBuilder.File("gamma.bin", 30));

        return (builder.Build(SortSpecification.Default), root, alpha, sub);
    }

    [Fact]
    public void StartsWithTheRootsExpanded()
    {
        (ScanTree tree, int root, _, _) = BuildTree();

        var model = new VisibleRowModel(tree);

        // The root plus its three children.
        Assert.Equal(4, model.Count);
        Assert.Equal(root, model[0].NodeId);
        Assert.True(model[0].IsExpanded);
        Assert.Equal(1, model[1].Depth);
    }

    [Fact]
    public void ExpandingRevealsTheChildren()
    {
        (ScanTree tree, _, int alpha, _) = BuildTree();
        var model = new VisibleRowModel(tree);

        model.Expand(alpha);

        Assert.Equal(5, model.Count);
        Assert.Equal(alpha, model[1].NodeId);
        Assert.True(model[1].IsExpanded);
        Assert.Equal(1, model[1].Depth);
    }

    [Fact]
    public void CollapsingHidesTheChildrenAgain()
    {
        (ScanTree tree, _, int alpha, _) = BuildTree();
        var model = new VisibleRowModel(tree);

        model.Expand(alpha);
        model.Collapse(alpha);

        Assert.Equal(4, model.Count);
        Assert.False(model[1].IsExpanded);
    }

    [Fact]
    public void NestedExpansionKeepsTheDepthsRight()
    {
        (ScanTree tree, _, int alpha, int sub) = BuildTree();
        var model = new VisibleRowModel(tree);

        model.Expand(alpha);
        model.Expand(sub);

        Assert.Equal(6, model.Count);
        Assert.Equal(0, model[0].Depth);
        Assert.Equal(1, model[1].Depth);
        Assert.Equal(2, model[2].Depth);
        Assert.Equal(3, model[3].Depth);
        Assert.Equal(1, model[4].Depth);
        Assert.Equal(1, model[5].Depth);
    }

    [Fact]
    public void CollapsingAParentHidesTheWholeSubtree()
    {
        (ScanTree tree, _, int alpha, int sub) = BuildTree();
        var model = new VisibleRowModel(tree);

        model.Expand(alpha);
        model.Expand(sub);
        model.Collapse(alpha);

        Assert.Equal(4, model.Count);
    }

    [Fact]
    public void RaisesASingleChangeNotificationPerAction()
    {
        (ScanTree tree, _, int alpha, _) = BuildTree();
        var model = new VisibleRowModel(tree);

        var notifications = new List<NotifyCollectionChangedAction>();
        model.CollectionChanged += (_, e) => notifications.Add(e.Action);

        model.Expand(alpha);

        Assert.Contains(NotifyCollectionChangedAction.Add, notifications);
        Assert.Contains(NotifyCollectionChangedAction.Replace, notifications);
    }

    [Fact]
    public void RebuildingKeepsTheExpansionState()
    {
        (ScanTree tree, _, int alpha, _) = BuildTree();
        var model = new VisibleRowModel(tree);

        model.Expand(alpha);

        model.Rebuild();

        Assert.Equal(5, model.Count);
        Assert.True(model.IsExpanded(alpha));
    }

    [Fact]
    public void RebuildingPicksUpANewSortOrder()
    {
        (ScanTree tree, int root, int alpha, _) = BuildTree();
        var model = new VisibleRowModel(tree);

        model.Expand(alpha);

        tree.ReSort(new SortSpecification
        {
            Key = SortKey.Name,
            Direction = SortDirection.Ascending,
            DirectoriesFirst = true,
        });

        model.Rebuild();

        Assert.Equal(5, model.Count);

        // The rows follow the tree's new order rather than the order they had before.
        int expected = tree.GetSortedChildren(root)[0];
        Assert.Equal(expected, model[1].NodeId);
    }

    [Fact]
    public void TogglingExpandsThenCollapses()
    {
        (ScanTree tree, _, int alpha, _) = BuildTree();
        var model = new VisibleRowModel(tree);

        model.Toggle(alpha);
        Assert.True(model.IsExpanded(alpha));
        Assert.Equal(5, model.Count);

        model.Toggle(alpha);
        Assert.False(model.IsExpanded(alpha));
        Assert.Equal(4, model.Count);
    }
}
