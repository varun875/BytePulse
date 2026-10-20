using BytePulse.Pages;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace BytePulse
{
    public sealed partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            ExtendsContentIntoTitleBar = true;
            SetTitleBar(AppTitleBar);
            AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
            AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico"));

            AppState.ScanSession.ScanRequested += OnScanRequested;
        }

        /// <summary>
        /// Moves to the scan page when another page asks for a path to be scanned.
        /// </summary>
        private void OnScanRequested(string path)
        {
            NavigationViewItem? scanItem = NavView.MenuItems
                .OfType<NavigationViewItem>()
                .FirstOrDefault(item => (item.Tag as string) == "scan");

            if (scanItem is not null && !ReferenceEquals(NavView.SelectedItem, scanItem))
            {
                // Selecting the item raises the navigation, which is what keeps the
                // navigation pane in step with the page.
                NavView.SelectedItem = scanItem;
                return;
            }

            NavFrame.Navigate(typeof(ScanPage));
        }

        private void TitleBar_PaneToggleRequested(TitleBar sender, object args)
        {
            NavView.IsPaneOpen = !NavView.IsPaneOpen;
        }

        private void TitleBar_BackRequested(TitleBar sender, object args)
        {
            NavFrame.GoBack();
        }

        private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (args.IsSettingsSelected)
            {
                NavFrame.Navigate(typeof(SettingsPage));
            }
            else if (args.SelectedItem is NavigationViewItem item)
            {
                switch (item.Tag)
                {
                    case "scan":
                        NavFrame.Navigate(typeof(ScanPage));
                        break;
                    case "map":
                        NavFrame.Navigate(typeof(MapPage));
                        break;
                    case "home":
                        NavFrame.Navigate(typeof(HomePage));
                        break;
                    case "about":
                        NavFrame.Navigate(typeof(AboutPage));
                        break;
                    default:
                        throw new InvalidOperationException($"Unknown navigation item tag: {item.Tag}");
                }
            }
        }
    }
}
