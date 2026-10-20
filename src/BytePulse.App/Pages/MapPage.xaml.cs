using BytePulse.AppState;
using BytePulse.Core.Formatting;
using BytePulse.Core.Model;
using BytePulse.Visualization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace BytePulse.Pages;

/// <summary>
/// Shows a folder as a map of boxes, where the area of a box is the amount of space the
/// item takes up.
/// </summary>
public sealed partial class MapPage : Page
{
    private static readonly Brush LabelBrush = new SolidColorBrush(Microsoft.UI.Colors.White);

    public MapPage()
    {
        InitializeComponent();

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ScanSession.Changed += Redraw;
        Redraw();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        ScanSession.Changed -= Redraw;
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e) => Redraw();

    private void OnRefresh(object sender, RoutedEventArgs e) => Redraw();

    private void OnUp(object sender, RoutedEventArgs e)
    {
        ScanSession.MoveUp();
        Redraw();
    }

    private void Redraw()
    {
        ScanTree? tree = ScanSession.Tree;

        if (tree is null || ScanSession.CurrentNodeId < 0)
        {
            PathText.Text = "Scan a folder first.";
            StatusText.Text = string.Empty;
            MapCanvas.Children.Clear();
            return;
        }

        int nodeId = ScanSession.CurrentNodeId;
        ScanNode node = tree.GetNode(nodeId);

        PathText.Text = tree.GetPath(nodeId);
        StatusText.Text =
            $"{ByteFormatter.Format(node.TotalLogicalSize)} in " +
            $"{ByteFormatter.FormatCount(node.TotalFileCount, "file", "files")} and " +
            $"{ByteFormatter.FormatCount(node.TotalDirectoryCount, "folder", "folders")}";

        double width = MapCanvas.ActualWidth;
        double height = MapCanvas.ActualHeight;

        if (width <= 0 || height <= 0)
        {
            return;
        }

        var items = new List<TreemapItem>();
        var byLabel = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (int child in tree.GetSortedChildren(nodeId))
        {
            ScanNode childNode = tree.GetNode(child);

            if (childNode.TotalLogicalSize <= 0)
            {
                continue;
            }

            string name = childNode.Name;
            items.Add(new TreemapItem(name, childNode.TotalLogicalSize));

            if (!byLabel.ContainsKey(name))
            {
                byLabel[name] = child;
            }
        }

        TreemapRenderer.Draw(MapCanvas, items, width, height, LabelBrush, selected =>
        {
            if (byLabel.TryGetValue(selected.Label, out int childId) && tree.IsDirectory(childId))
            {
                ScanSession.MoveTo(childId);
            }
        });
    }
}
