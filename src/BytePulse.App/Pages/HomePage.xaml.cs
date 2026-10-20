using BytePulse.AppState;
using BytePulse.Core.Formatting;
using BytePulse.Core.Model;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace BytePulse.Pages;

/// <summary>The landing page: what was scanned last, and what can be scanned now.</summary>
public sealed partial class HomePage : Page
{
    public HomePage()
    {
        InitializeComponent();

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        ScanSession.Changed += Refresh;

        DriveList.ItemsSource = DriveSummary.ForThisMachine();
        Refresh();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e) => ScanSession.Changed -= Refresh;

    private void Refresh()
    {
        ScanTree? tree = ScanSession.Tree;

        if (tree is null)
        {
            LastScanText.Text = "Nothing has been scanned yet.";
            RescanButton.IsEnabled = false;
            ResultsButton.IsEnabled = false;
            MapButton.IsEnabled = false;
            return;
        }

        LastScanText.Text =
            $"{ScanSession.RootPath}\n" +
            $"{ByteFormatter.Format(tree.TotalLogicalSize)} in " +
            $"{ByteFormatter.FormatCount(tree.TotalFileCount, "file", "files")} and " +
            $"{ByteFormatter.FormatCount(tree.TotalDirectoryCount, "folder", "folders")}";

        RescanButton.IsEnabled = true;
        ResultsButton.IsEnabled = true;
        MapButton.IsEnabled = true;
    }

    private void OnRescan(object sender, RoutedEventArgs e) => ScanSession.RequestScan(ScanSession.RootPath);

    private void OnOpenResults(object sender, RoutedEventArgs e) => Frame.Navigate(typeof(ScanPage));

    private void OnOpenMap(object sender, RoutedEventArgs e) => Frame.Navigate(typeof(MapPage));

    private void OnScanDrive(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string path })
        {
            ScanSession.RequestScan(path);
        }
    }

    /// <summary>One drive, described for the list.</summary>
    public sealed record DriveSummary(string Name, string Path, string Description, double UsedPercent)
    {
        /// <summary>Describes the fixed and removable drives that are ready.</summary>
        public static IReadOnlyList<DriveSummary> ForThisMachine()
        {
            var drives = new List<DriveSummary>();

            foreach (DriveInfo drive in DriveInfo.GetDrives())
            {
                if (!drive.IsReady || drive.DriveType is not (DriveType.Fixed or DriveType.Removable))
                {
                    continue;
                }

                long used = drive.TotalSize - drive.AvailableFreeSpace;
                double percent = drive.TotalSize > 0 ? used * 100.0 / drive.TotalSize : 0;

                string description =
                    $"{ByteFormatter.Format(used)} of {ByteFormatter.Format(drive.TotalSize)} used, " +
                    $"{ByteFormatter.Format(drive.AvailableFreeSpace)} free";

                drives.Add(new DriveSummary(drive.Name, drive.RootDirectory.FullName, description, percent));
            }

            return drives;
        }
    }
}
