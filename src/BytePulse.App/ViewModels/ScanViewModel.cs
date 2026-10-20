using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Globalization;
using BytePulse.Core.Formatting;
using BytePulse.Core.Model;
using BytePulse.Core.Scanning;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using SortKey = BytePulse.Core.Model.SortKey;

namespace BytePulse.ViewModels;

/// <summary>One row of the results grid.</summary>
/// <param name="NodeId">The node this row points at.</param>
/// <param name="Depth">How deep the node is, used for the indent.</param>
/// <param name="Name">The node's name.</param>
/// <param name="Size">The node's size, already formatted.</param>
/// <param name="SizeOnDisk">The node's size on disk, already formatted.</param>
/// <param name="Files">How many files the node holds, already formatted.</param>
/// <param name="Modified">When the node was last written.</param>
/// <param name="IsDirectory">Whether the node is a directory.</param>
/// <param name="HasChildren">Whether the node can be expanded.</param>
/// <param name="IsExpanded">Whether the node is expanded.</param>
public sealed record FileRow(
    int NodeId,
    int Depth,
    string Name,
    string Size,
    string SizeOnDisk,
    string Files,
    string Modified,
    bool IsDirectory,
    bool HasChildren,
    bool IsExpanded)
{
    /// <summary>The indent that lines the row up with its depth in the tree.</summary>
    public Thickness Indent => new(Depth * 16, 0, 0, 0);

    /// <summary>The glyph for the expander, which points down when the row is open.</summary>
    public string ChevronGlyph => IsExpanded ? "\uE70D" : "\uE76C";
}

/// <summary>The state of the scan page.</summary>
public partial class ScanViewModel : ObservableObject
{
    private readonly DirectoryScanner _scanner = new();
    private CancellationTokenSource? _cancellation;
    private ScanTree? _tree;
    private VisibleRowModel? _rowModel;

    /// <summary>The path to scan.</summary>
    [ObservableProperty]
    public partial string Path { get; set; } = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    /// <summary>Whether a scan is running.</summary>
    [ObservableProperty]
    public partial bool IsScanning { get; set; }

    /// <summary>What the scan is doing.</summary>
    [ObservableProperty]
    public partial string StatusText { get; set; } = "Ready.";

    /// <summary>The total size of everything that was found.</summary>
    [ObservableProperty]
    public partial long TotalBytes { get; set; }

    /// <summary>How many files were found.</summary>
    [ObservableProperty]
    public partial int TotalFiles { get; set; }

    /// <summary>How many folders were found.</summary>
    [ObservableProperty]
    public partial int TotalDirectories { get; set; }

    /// <summary>How many folders could not be read.</summary>
    [ObservableProperty]
    public partial int ErrorCount { get; set; }

    /// <summary>The column the results are sorted by.</summary>
    [ObservableProperty]
    public partial string SortColumn { get; set; } = Services.SettingsStore.Current.SortColumn;

    /// <summary>Whether folders are listed before files.</summary>
    [ObservableProperty]
    public partial bool DirectoriesFirst { get; set; } = Services.SettingsStore.Current.DirectoriesFirst;

    /// <summary>The total size, already formatted for display.</summary>
    public string TotalSizeText => ByteFormatter.Format(TotalBytes);

    /// <summary>The rows shown in the grid.</summary>
    public ObservableCollection<FileRow> Rows { get; } = [];

    /// <summary>Starts a scan of <see cref="Path"/>.</summary>
    [RelayCommand(CanExecute = nameof(CanScan))]
    private async Task ScanAsync()
    {
        if (string.IsNullOrWhiteSpace(Path))
        {
            StatusText = "Enter a path to scan.";
            return;
        }

        _cancellation?.Dispose();
        _cancellation = new CancellationTokenSource();

        IsScanning = true;
        ErrorCount = 0;
        StatusText = "Scanning...";
        Rows.Clear();
        _tree = null;
        _rowModel = null;

        var progress = new Progress<ScanProgress>(OnProgress);

        try
        {
            ScanOptions options = Services.SettingsStore.ToScanOptions() with { Sort = CurrentSort };

            ScanResult result = await _scanner.ScanAsync(
                new[] { Path },
                options,
                progress,
                _cancellation.Token);

            _tree = result.Tree;
            _rowModel = new VisibleRowModel(result.Tree);
            _rowModel.CollectionChanged += OnRowsChanged;

            AppState.ScanSession.Set(result.Tree, Path);

            TotalBytes = result.Tree.TotalLogicalSize;
            TotalFiles = result.Tree.TotalFileCount;
            TotalDirectories = result.Tree.TotalDirectoryCount;
            ErrorCount = result.Errors.Count;

            RebuildRows();

            StatusText = result.IsComplete
                ? $"Scanned in {result.Elapsed.TotalSeconds:F2}s"
                : "Cancelled.";
        }
        catch (Exception ex)
        {
            StatusText = $"The scan failed: {ex.Message}";
        }
        finally
        {
            IsScanning = false;
        }
    }

    /// <summary>Stops the scan that is running.</summary>
    [RelayCommand(CanExecute = nameof(IsScanning))]
    private void Cancel()
    {
        _cancellation?.Cancel();
        StatusText = "Cancelling...";
    }

    /// <summary>Expands or collapses a row.</summary>
    [RelayCommand]
    private void Toggle(FileRow? row)
    {
        if (row is null || !row.HasChildren)
        {
            return;
        }

        _rowModel?.Toggle(row.NodeId);
    }

    /// <summary>Sorts the results by a column.</summary>
    [RelayCommand]
    private void SortBy(string? column)
    {
        if (string.IsNullOrEmpty(column) || _tree is null)
        {
            return;
        }

        SortColumn = column;
        _tree.ReSort(CurrentSort);
        _rowModel?.Rebuild();
        RebuildRows();
    }

    private SortSpecification CurrentSort => SortColumn switch
    {
        "Name" => new SortSpecification
        {
            Key = SortKey.Name,
            Direction = SortDirection.Ascending,
            DirectoriesFirst = DirectoriesFirst,
        },
        "Files" => new SortSpecification
        {
            Key = SortKey.FileCount,
            Direction = SortDirection.Descending,
            DirectoriesFirst = DirectoriesFirst,
        },
        "Modified" => new SortSpecification
        {
            Key = SortKey.LastWriteTime,
            Direction = SortDirection.Descending,
            DirectoriesFirst = DirectoriesFirst,
        },
        _ => new SortSpecification
        {
            Key = SortKey.LogicalSize,
            Direction = SortDirection.Descending,
            DirectoriesFirst = DirectoriesFirst,
        },
    };

    private bool CanScan() => !IsScanning;

    partial void OnIsScanningChanged(bool value) => ScanCommand.NotifyCanExecuteChanged();

    partial void OnTotalBytesChanged(long value) => OnPropertyChanged(nameof(TotalSizeText));

    partial void OnDirectoriesFirstChanged(bool value)
    {
        Services.AppSettings settings = Services.SettingsStore.Current.Clone();
        settings.DirectoriesFirst = value;
        Services.SettingsStore.Save(settings);
    }

    partial void OnSortColumnChanged(string value)
    {
        Services.AppSettings settings = Services.SettingsStore.Current.Clone();
        settings.SortColumn = value;
        Services.SettingsStore.Save(settings);
    }

    private void OnProgress(ScanProgress progress)
    {
        StatusText =
            $"{progress.EntriesScanned:N0} entries, {ByteFormatter.Format(progress.BytesScanned)}";
    }

    private void OnRowsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Reset:
                RebuildRows();
                break;

            case NotifyCollectionChangedAction.Add when e.NewItems is not null:
                int at = e.NewStartingIndex >= 0 ? e.NewStartingIndex : Rows.Count;
                for (int i = 0; i < e.NewItems.Count; i++)
                {
                    Rows.Insert(at + i, CreateRow((VisibleRow)e.NewItems[i]!));
                }

                break;

            case NotifyCollectionChangedAction.Remove when e.OldItems is not null:
                int from = e.OldStartingIndex >= 0 ? e.OldStartingIndex : 0;
                for (int i = 0; i < e.OldItems.Count && from + i < Rows.Count; i++)
                {
                    Rows.RemoveAt(from + i);
                }

                break;

            case NotifyCollectionChangedAction.Replace when e.NewItems is not null:
                int index = e.NewStartingIndex;
                if (index >= 0 && index < Rows.Count)
                {
                    Rows[index] = CreateRow((VisibleRow)e.NewItems[0]!);
                }

                break;
        }
    }

    private void RebuildRows()
    {
        Rows.Clear();

        if (_rowModel is null || _tree is null)
        {
            return;
        }

        for (int i = 0; i < _rowModel.Count; i++)
        {
            Rows.Add(CreateRow(_rowModel[i]));
        }
    }

    private FileRow CreateRow(VisibleRow row)
    {
        ScanNode node = _tree!.GetNode(row.NodeId);

        return new FileRow(
            row.NodeId,
            row.Depth,
            node.Name,
            ByteFormatter.Format(node.TotalLogicalSize),
            ByteFormatter.Format(node.TotalAllocatedSize),
            node.TotalFileCount.ToString("N0", CultureInfo.CurrentCulture),
            node.LastWriteTimeUtc.ToString("yyyy-MM-dd HH:mm", CultureInfo.CurrentCulture),
            node.IsDirectory,
            row.HasChildren,
            row.IsExpanded);
    }
}
