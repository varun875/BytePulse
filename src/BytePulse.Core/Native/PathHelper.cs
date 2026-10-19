using BytePulse.Core.Scanning;

namespace BytePulse.Core.Native;

/// <summary>Helpers for turning paths into the form the file system expects.</summary>
internal static class PathHelper
{
    private const string LongPathPrefix = @"\\?\";
    private const string LongUncPathPrefix = @"\\?\UNC\";

    /// <summary>
    /// Rewrites a path into its extended form, which is not limited to the legacy
    /// maximum path length.
    /// </summary>
    public static string Normalize(string path)
    {
        if (path.StartsWith(LongPathPrefix, StringComparison.Ordinal))
        {
            return path;
        }

        if (path.StartsWith(@"\\", StringComparison.Ordinal))
        {
            return LongUncPathPrefix + path[2..];
        }

        return Path.IsPathRooted(path) ? LongPathPrefix + path : path;
    }

    /// <summary>
    /// Builds the search pattern that matches every entry of a directory.
    /// </summary>
    public static string SearchPattern(string directoryPath)
    {
        string normalized = Normalize(directoryPath);
        char last = normalized[^1];

        if (last == Path.DirectorySeparatorChar || last == Path.AltDirectorySeparatorChar)
        {
            return normalized + "*";
        }

        return normalized + Path.DirectorySeparatorChar + "*";
    }

    /// <summary>Maps a Win32 error code onto the kind of failure it represents.</summary>
    public static ScanErrorKind MapError(int error) => error switch
    {
        Win32.ERROR_ACCESS_DENIED => ScanErrorKind.AccessDenied,
        Win32.ERROR_NETWORK_ACCESS_DENIED => ScanErrorKind.AccessDenied,
        Win32.ERROR_FILE_NOT_FOUND => ScanErrorKind.NotFound,
        Win32.ERROR_PATH_NOT_FOUND => ScanErrorKind.NotFound,
        Win32.ERROR_INVALID_NAME => ScanErrorKind.InvalidPath,
        Win32.ERROR_INVALID_PARAMETER => ScanErrorKind.InvalidPath,
        Win32.ERROR_DIRECTORY => ScanErrorKind.InvalidPath,
        _ => ScanErrorKind.DeviceError,
    };

    /// <summary>Maps a Win32 error code onto the kind of failure it represents.</summary>
    public static ScanErrorKind MapError(Exception exception) => exception switch
    {
        UnauthorizedAccessException => ScanErrorKind.AccessDenied,
        DirectoryNotFoundException => ScanErrorKind.NotFound,
        FileNotFoundException => ScanErrorKind.NotFound,
        ArgumentException => ScanErrorKind.InvalidPath,
        PathTooLongException => ScanErrorKind.InvalidPath,
        _ => ScanErrorKind.DeviceError,
    };
}
