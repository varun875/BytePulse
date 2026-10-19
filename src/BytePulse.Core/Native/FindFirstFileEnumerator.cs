using System.Runtime.InteropServices;
using BytePulse.Core.Scanning;

namespace BytePulse.Core.Native;

/// <summary>
/// Enumerates a directory with <c>FindFirstFileEx</c> and <c>FindNextFile</c>.
/// </summary>
/// <remarks>
/// <para>
/// The find-data buffer is owned by the enumerator and the file name is read straight out
/// of it as UTF-16 bytes, so no string is allocated for any entry.
/// </para>
/// <para>
/// The enumeration includes the <c>.</c> and <c>..</c> entries that every directory
/// reports; the scanner skips them.
/// </para>
/// </remarks>
internal sealed class FindFirstFileEnumerator : DirectoryEnumerator
{
    private const nint InvalidHandleValue = -1;

    private readonly byte[] _findData = new byte[Win32.FindDataSize];
    private nint _handle = InvalidHandleValue;

    private FindDataEntry _entry;
    private bool _exhausted;

    /// <summary>Opens a directory for enumeration.</summary>
    /// <exception cref="DirectoryEnumerationException">The directory could not be opened.</exception>
    public FindFirstFileEnumerator(string directoryPath)
    {
        string pattern = PathHelper.SearchPattern(directoryPath);

        _handle = Interop.FindFirstFileExW(
            pattern,
            Win32.FindExInfoBasic,
            _findData,
            Win32.FindExSearchNameMatch,
            0,
            0);

        if (_handle == InvalidHandleValue || _handle == 0)
        {
            int error = Marshal.GetLastWin32Error();
            throw new DirectoryEnumerationException(directoryPath, PathHelper.MapError(error), error);
        }

        _entry = FindDataParser.Parse(_findData);
    }

    public override bool MoveNext()
    {
        if (_exhausted)
        {
            return false;
        }

        if (!Interop.FindNextFileW(_handle, _findData))
        {
            _exhausted = true;
            return false;
        }

        _entry = FindDataParser.Parse(_findData);
        return true;
    }

    public override DirectoryEntry Current => new(
        _findData.AsSpan(_entry.NameOffset, _entry.NameLength),
        _entry.Size,
        0,
        _entry.Attributes,
        0,
        _entry.LastWriteTicks,
        _entry.CreationTicks,
        _entry.LastAccessTicks,
        (_entry.Attributes & Win32.FILE_ATTRIBUTE_DIRECTORY) != 0,
        (_entry.Attributes & Win32.FILE_ATTRIBUTE_REPARSE_POINT) != 0);

    public override void Dispose()
    {
        if (_handle != InvalidHandleValue && _handle != 0)
        {
            Interop.FindClose(_handle);
            _handle = InvalidHandleValue;
        }

        base.Dispose();
    }
}
