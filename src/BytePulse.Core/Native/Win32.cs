using System.Runtime.InteropServices;

namespace BytePulse.Core.Native;

/// <summary>The Win32 constants the scanner needs.</summary>
internal static class Win32
{
    internal const uint FILE_READ_DATA = 0x0001;
    internal const uint FILE_LIST_DIRECTORY = 0x0001;
    internal const uint FILE_READ_ATTRIBUTES = 0x0080;

    internal const uint FILE_SHARE_READ = 0x00000001;
    internal const uint FILE_SHARE_WRITE = 0x00000002;
    internal const uint FILE_SHARE_DELETE = 0x00000004;

    internal const uint OPEN_EXISTING = 3;

    internal const uint FILE_FLAG_BACKUP_SEMANTICS = 0x02000000;
    internal const uint FILE_FLAG_OPEN_REPARSE_POINT = 0x00200000;

    internal const uint FILE_ATTRIBUTE_READONLY = 0x00000001;
    internal const uint FILE_ATTRIBUTE_HIDDEN = 0x00000002;
    internal const uint FILE_ATTRIBUTE_SYSTEM = 0x00000004;
    internal const uint FILE_ATTRIBUTE_DIRECTORY = 0x00000010;
    internal const uint FILE_ATTRIBUTE_ARCHIVE = 0x00000020;
    internal const uint FILE_ATTRIBUTE_DEVICE = 0x00000040;
    internal const uint FILE_ATTRIBUTE_NORMAL = 0x00000080;
    internal const uint FILE_ATTRIBUTE_TEMPORARY = 0x00000100;
    internal const uint FILE_ATTRIBUTE_SPARSE_FILE = 0x00000200;
    internal const uint FILE_ATTRIBUTE_REPARSE_POINT = 0x00000400;
    internal const uint FILE_ATTRIBUTE_COMPRESSED = 0x00000800;
    internal const uint FILE_ATTRIBUTE_OFFLINE = 0x00001000;
    internal const uint FILE_ATTRIBUTE_ENCRYPTED = 0x00004000;

    internal const int FindExInfoBasic = 1;
    internal const int FindExSearchNameMatch = 0;

    /// <summary>The <c>FileStandardInfo</c> information class.</summary>
    internal const int FileStandardInfo = 1;

    internal const int ERROR_FILE_NOT_FOUND = 2;
    internal const int ERROR_PATH_NOT_FOUND = 3;
    internal const int ERROR_ACCESS_DENIED = 5;
    internal const int ERROR_NO_MORE_FILES = 18;
    internal const int ERROR_INVALID_NAME = 123;
    internal const int ERROR_BAD_NETPATH = 53;
    internal const int ERROR_NETWORK_ACCESS_DENIED = 65;
    internal const int ERROR_INVALID_PARAMETER = 87;
    internal const int ERROR_DIRECTORY = 267;

    /// <summary>The number of 100-nanosecond intervals between 0001-01-01 and 1601-01-01.</summary>
    internal const long FileTimeEpochTicks = 504911232000000000;

    /// <summary>The largest value a <see cref="DateTime"/> can hold.</summary>
    internal const long MaxDateTimeTicks = 3155378975999999999;

    /// <summary>The offset of the file name inside a <c>WIN32_FIND_DATAW</c>.</summary>
    internal const int FindDataNameOffset = 44;

    /// <summary>The number of characters the file name field can hold, including the terminator.</summary>
    internal const int FindDataNameLength = 260;

    /// <summary>The number of characters the alternate file name field can hold.</summary>
    internal const int FindDataAlternateNameLength = 14;

    /// <summary>
    /// The size of a <c>WIN32_FIND_DATAW</c> structure.
    /// </summary>
    /// <remarks>
    /// The structure is larger than the two name fields alone suggest: it also holds a
    /// fourteen character alternate name after the main name, and the kernel writes the
    /// whole structure, so the buffer has to be big enough for all of it.
    /// </remarks>
    internal const int FindDataSize =
        FindDataNameOffset + (FindDataNameLength * 2) + (FindDataAlternateNameLength * 2);
}

/// <summary>The <c>FILETIME</c> structure, as two 32-bit halves.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct FileTime
{
    public uint Low;
    public uint High;

    /// <summary>The value as a 64-bit count of 100-nanosecond intervals.</summary>
    public readonly ulong Value => ((ulong)High << 32) | Low;

    /// <summary>The value as .NET ticks, or zero when it cannot be represented.</summary>
    public readonly long Ticks
    {
        get
        {
            ulong value = Value;
            if (value == 0)
            {
                return 0;
            }

            // The largest file time that still fits in a DateTime.
            if (value > MaxFileTime)
            {
                return 0;
            }

            return (long)(value + (ulong)Win32.FileTimeEpochTicks);
        }
    }

    /// <summary>The largest file time that can be expressed as .NET ticks.</summary>
    private const ulong MaxFileTime = (ulong)(Win32.MaxDateTimeTicks - Win32.FileTimeEpochTicks);
}

/// <summary>Information about a file, as returned by <c>GetFileInformationByHandleEx</c>.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct FileStandardInformation
{
    public long AllocationSize;
    public long EndOfFile;
    public uint NumberOfLinks;
    public byte DeletePending;
    public byte Directory;
}

/// <summary>Information about a file, as returned by <c>GetFileInformationByHandle</c>.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct ByHandleFileInformation
{
    public uint FileAttributes;
    public FileTime CreationTime;
    public FileTime LastAccessTime;
    public FileTime LastWriteTime;
    public uint VolumeSerialNumber;
    public uint FileSizeHigh;
    public uint FileSizeLow;
    public uint NumberOfLinks;
    public uint FileIndexHigh;
    public uint FileIndexLow;

    /// <summary>The file's unique identifier on its volume.</summary>
    public readonly ulong FileId => ((ulong)FileIndexHigh << 32) | FileIndexLow;
}
