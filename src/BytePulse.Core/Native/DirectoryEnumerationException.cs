using BytePulse.Core.Scanning;

namespace BytePulse.Core.Native;

/// <summary>Thrown when a directory cannot be enumerated.</summary>
public sealed class DirectoryEnumerationException : Exception
{
    /// <summary>Creates an exception for a directory that could not be enumerated.</summary>
    public DirectoryEnumerationException(string path, ScanErrorKind kind, int nativeErrorCode)
        : base(Describe(path, kind, nativeErrorCode))
    {
        Path = path;
        Kind = kind;
        NativeErrorCode = nativeErrorCode;
    }

    /// <summary>The directory that could not be enumerated.</summary>
    public string Path { get; }

    /// <summary>The kind of failure.</summary>
    public ScanErrorKind Kind { get; }

    /// <summary>The Win32 error code, when there was one.</summary>
    public int NativeErrorCode { get; }

    /// <summary>The failure as a scan error.</summary>
    public ScanError ToScanError() => new(Path, Kind, NativeErrorCode);

    private static string Describe(string path, ScanErrorKind kind, int nativeErrorCode) => kind switch
    {
        ScanErrorKind.AccessDenied => $"Access denied: {path}",
        ScanErrorKind.NotFound => $"Not found: {path}",
        ScanErrorKind.InvalidPath => $"Invalid path: {path}",
        _ => $"Failed to read {path} (0x{nativeErrorCode:X8})",
    };
}
