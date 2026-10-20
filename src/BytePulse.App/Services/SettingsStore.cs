using System.Text.Json;
using BytePulse.Core.Scanning;

namespace BytePulse.Services;

/// <summary>
/// Reads and writes the application's settings, and turns them into scan options.
/// </summary>
/// <remarks>
/// An unpackaged application has no package settings store, so the settings are a small
/// JSON file under the user's local application data. A settings file that has been
/// damaged is replaced with the defaults rather than stopping the application from
/// starting.
/// </remarks>
public static class SettingsStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
    };

    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "BytePulse",
        "settings.json");

    /// <summary>The current settings.</summary>
    public static AppSettings Current { get; private set; } = Load();

    /// <summary>Reads the settings from disk, falling back to the defaults.</summary>
    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                string json = File.ReadAllText(FilePath);
                AppSettings? loaded = JsonSerializer.Deserialize<AppSettings>(json, SerializerOptions);

                if (loaded is not null)
                {
                    // A value written by a different machine could be out of range.
                    loaded.ThreadCount = Math.Clamp(loaded.ThreadCount, 1, 64);
                    return loaded;
                }
            }
        }
        catch (Exception)
        {
            // A damaged or unreadable file is not a reason to refuse to start.
        }

        return new AppSettings();
    }

    /// <summary>Writes the settings to disk.</summary>
    public static void Save(AppSettings settings)
    {
        Current = settings;

        try
        {
            string? directory = Path.GetDirectoryName(FilePath);

            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(FilePath, JsonSerializer.Serialize(settings, SerializerOptions));
        }
        catch (Exception)
        {
            // Settings that could not be written are not a reason to interrupt the user.
        }
    }

    /// <summary>Turns the current settings into the options for a scan.</summary>
    public static ScanOptions ToScanOptions()
    {
        AppSettings settings = Current;

        return new ScanOptions
        {
            ThreadCount = Math.Clamp(settings.ThreadCount, 1, 64),
            FollowReparsePoints = settings.FollowReparsePoints,
            IncludeHiddenFiles = settings.IncludeHiddenAndSystem,
            IncludeSystemFiles = settings.IncludeHiddenAndSystem,
            ComputeSizeOnDisk = settings.ComputeSizeOnDisk,
            DetectHardlinks = settings.DetectHardlinks,
            Sort = ToSort(settings),
        };
    }

    /// <summary>Builds the sort specification the settings describe.</summary>
    public static Core.Model.SortSpecification ToSort(AppSettings settings) => settings.SortColumn switch
    {
        "Name" => new Core.Model.SortSpecification
        {
            Key = Core.Model.SortKey.Name,
            Direction = Core.Model.SortDirection.Ascending,
            DirectoriesFirst = settings.DirectoriesFirst,
        },
        "Files" => new Core.Model.SortSpecification
        {
            Key = Core.Model.SortKey.FileCount,
            Direction = Core.Model.SortDirection.Descending,
            DirectoriesFirst = settings.DirectoriesFirst,
        },
        "Modified" => new Core.Model.SortSpecification
        {
            Key = Core.Model.SortKey.LastWriteTime,
            Direction = Core.Model.SortDirection.Descending,
            DirectoriesFirst = settings.DirectoriesFirst,
        },
        _ => new Core.Model.SortSpecification
        {
            Key = Core.Model.SortKey.LogicalSize,
            Direction = Core.Model.SortDirection.Descending,
            DirectoriesFirst = settings.DirectoriesFirst,
        },
    };
}
