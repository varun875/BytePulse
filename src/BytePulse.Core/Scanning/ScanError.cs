namespace BytePulse.Core.Scanning;

/// <summary>Why a directory could not be scanned.</summary>
public enum ScanErrorKind
{
    /// <summary>The directory could not be opened, usually because access was denied.</summary>
    AccessDenied,

    /// <summary>The directory disappeared while it was being scanned.</summary>
    NotFound,

    /// <summary>The path was too long, or otherwise not a valid path.</summary>
    InvalidPath,

    /// <summary>The file system reported an error that has no more specific kind.</summary>
    DeviceError,

    /// <summary>The scan was cancelled before this directory was reached.</summary>
    Cancelled,
}

/// <summary>A directory that could not be scanned, and why.</summary>
/// <param name="Path">The path that could not be scanned.</param>
/// <param name="Kind">The category of the failure.</param>
/// <param name="NativeErrorCode">The Win32 error code, when there was one.</param>
public readonly record struct ScanError(string Path, ScanErrorKind Kind, int NativeErrorCode)
{
    /// <summary>A human-readable description of the failure.</summary>
    public string Message => Kind switch
    {
        ScanErrorKind.AccessDenied => "Access denied",
        ScanErrorKind.NotFound => "Not found",
        ScanErrorKind.InvalidPath => "Invalid path",
        ScanErrorKind.Cancelled => "Cancelled",
        _ => $"Device error (0x{NativeErrorCode:X8})",
    };
}
