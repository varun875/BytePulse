using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace BytePulse.Core.Native;

/// <summary>
/// The Win32 entry points the scanner uses.
/// </summary>
/// <remarks>
/// <para>
/// The find-data calls write into a buffer the scanner owns rather than through a
/// marshalled structure, so the file name is read straight out of the buffer as UTF-16
/// bytes. That avoids allocating a string for every entry in the tree.
/// </para>
/// </remarks>
internal static partial class Interop
{
    [LibraryImport("kernel32.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    internal static partial SafeFileHandle CreateFileW(
        string fileName,
        uint desiredAccess,
        uint shareMode,
        nint securityAttributes,
        uint creationDisposition,
        uint flagsAndAttributes,
        nint templateFile);

    [LibraryImport("kernel32.dll", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
    internal static partial nint FindFirstFileExW(
        string fileName,
        int infoLevel,
        Span<byte> findFileData,
        int searchOp,
        nint searchFilter,
        uint additionalFlags);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool FindNextFileW(nint findFile, Span<byte> findFileData);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool FindClose(nint findFile);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool GetFileInformationByHandle(SafeFileHandle file, out ByHandleFileInformation information);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static partial bool GetFileInformationByHandleEx(
        SafeFileHandle file,
        int informationClass,
        out FileStandardInformation information,
        uint length);
}
