using System.ComponentModel;
using BytePulse.AppState;
using BytePulse.Core.Formatting;
using BytePulse.Core.Model;
using BytePulse.ViewModels;
using BytePulse.Visualization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.UI;

namespace BytePulse.Pages;

/// <summary>The page that scans a path and shows what is using the space.</summary>
public sealed partial class ScanPage : Page
{
    public ScanPage()
    {
        InitializeComponent();

        ViewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    /// <summary>The state of the page.</summary>
    public ScanViewModel ViewModel { get; } = new();

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ScanViewModel.TotalBytes))
        {
            RedrawChart();
        }
    }

    private void OnItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is FileRow row)
        {
            ViewModel.ToggleCommand.Execute(row);
        }
    }

    private void OnSortName(object sender, RoutedEventArgs e) => ViewModel.SortByCommand.Execute("Name");

    private void OnSortSize(object sender, RoutedEventArgs e) => ViewModel.SortByCommand.Execute("Size");

    private void OnSortFiles(object sender, RoutedEventArgs e) => ViewModel.SortByCommand.Execute("Files");

    private void OnSortModified(object sender, RoutedEventArgs e) => ViewModel.SortByCommand.Execute("Modified");

    private async void OnBrowse(object sender, RoutedEventArgs e)
    {
        var picker = new Windows.Storage.Pickers.FolderPicker();
        picker.FileTypeFilter.Add("*");

        if (App.MainWindow is not null)
        {
            nint window = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, window);
        }

        Windows.Storage.StorageFolder? folder = await picker.PickSingleFolderAsync();
        if (folder is not null)
        {
            ViewModel.Path = folder.Path;
        }
    }

    /// <summary>
    /// Draws the ring of the root's largest children, with a legend beside it.
    /// </summary>
    private void RedrawChart()
    {
        DonutCanvas.Children.Clear();
        LegendPanel.Children.Clear();

        ScanTree? tree = ScanSession.Tree;

        if (tree is null || tree.RootNodeIds.Count == 0)
        {
            return;
        }

        int rootId = tree.RootNodeIds[0];
        var segments = new List<ChartSegment>();

        foreach (int child in tree.GetSortedChildren(rootId))
        {
            ScanNode node = tree.GetNode(child);

            if (node.TotalLogicalSize > 0)
            {
                segments.Add(new ChartSegment(node.Name, node.TotalLogicalSize));
            }
        }

        IReadOnlyList<ChartSegment> slices = DonutChartRenderer.BuildSlices(segments);

        // The hole is filled with the page colour so the ring reads as a ring.
        Color background = ActualTheme == ElementTheme.Dark
            ? Color.FromArgb(255, 0x20, 0x20, 0x20)
            : Color.FromArgb(255, 0xF3, 0xF3, 0xF3);

        DonutChartRenderer.Draw(DonutCanvas, segments, 240, new SolidColorBrush(background));

        for (int i = 0; i < slices.Count; i++)
        {
            var row = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 6,
            };

            var swatch = new Rectangle
            {
                Width = 10,
                Height = 10,
                RadiusX = 2,
                RadiusY = 2,
                Fill = new SolidColorBrush(ChartPalette.At(i)),
                VerticalAlignment = VerticalAlignment.Center,
            };

            var label = new TextBlock
            {
                Text = slices[i].Label,
                FontSize = 12,
                MaxWidth = 148,
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center,
            };

            var value = new TextBlock
            {
                Text = ByteFormatter.Format(slices[i].Value),
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center,
            };

            row.Children.Add(swatch);
            row.Children.Add(label);
            row.Children.Add(value);
            LegendPanel.Children.Add(row);
        }
    }
}
