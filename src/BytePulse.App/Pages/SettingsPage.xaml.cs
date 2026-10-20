using BytePulse.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace BytePulse.Pages;

/// <summary>The settings, applied as they are changed.</summary>
public sealed partial class SettingsPage : Page
{
    private readonly bool _loading;

    public SettingsPage()
    {
        InitializeComponent();

        _loading = true;

        AppSettings settings = SettingsStore.Current;
        ThreadBox.Value = settings.ThreadCount;
        HiddenToggle.IsOn = settings.IncludeHiddenAndSystem;
        ReparseToggle.IsOn = settings.FollowReparsePoints;
        SizeOnDiskToggle.IsOn = settings.ComputeSizeOnDisk;
        HardlinkToggle.IsOn = settings.DetectHardlinks;
        FoldersFirstToggle.IsOn = settings.DirectoriesFirst;

        _loading = false;
    }

    private void OnThreadsChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
    {
        if (_loading || double.IsNaN(args.NewValue))
        {
            return;
        }

        Save(settings => settings.ThreadCount = (int)args.NewValue);
    }

    private void OnSettingToggled(object sender, RoutedEventArgs e)
    {
        if (_loading)
        {
            return;
        }

        Save(settings =>
        {
            settings.IncludeHiddenAndSystem = HiddenToggle.IsOn;
            settings.FollowReparsePoints = ReparseToggle.IsOn;
            settings.ComputeSizeOnDisk = SizeOnDiskToggle.IsOn;
            settings.DetectHardlinks = HardlinkToggle.IsOn;
            settings.DirectoriesFirst = FoldersFirstToggle.IsOn;
        });
    }

    private void Save(Action<AppSettings> change)
    {
        AppSettings settings = SettingsStore.Current.Clone();
        change(settings);
        SettingsStore.Save(settings);

        SavedText.Text = "Saved.";
    }
}
