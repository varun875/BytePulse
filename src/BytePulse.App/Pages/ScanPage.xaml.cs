using BytePulse.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace BytePulse.Pages;

/// <summary>The page that scans a path and shows what is using the space.</summary>
public sealed partial class ScanPage : Page
{
    public ScanPage()
    {
        InitializeComponent();
    }

    /// <summary>The state of the page.</summary>
    public ScanViewModel ViewModel { get; } = new();

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
}
