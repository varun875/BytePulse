using Microsoft.Win32.SafeHandles;

namespace BytePulse.Core.Native;

/// <summary>
/// The identity of a file on its volume.
/// </summary>
/// <param name="VolumeSerial">The volume the file lives on.</param>
/// <param name="FileId">The file's identifier within that volume.</param>
/// <remarks>
/// Every name for the same file has the same identity, so two paths that reach the same
/// file can be told apart from two paths that reach different files.
/// </remarks>
internal readonly record struct FileIdentity(uint VolumeSerial, ulong FileId);

/// <summary>Reads the identity of a file.</summary>
internal static class FileIdentityResolver
{
    /// <summary>
    /// Reads the identity of the file a path points at.
    /// </summary>
    /// <param name="path">The path to resolve.</param>
    /// <param name="followReparsePoints">
    /// When true the path is followed through a reparse point, so the identity returned
    /// belongs to the target rather than to the link.
    /// </param>
    /// <param name="identity">The identity that was read.</param>
    /// <returns>False when the path could not be opened.</returns>
    public static bool TryResolve(string path, bool followReparsePoints, out FileIdentity identity)
    {
        identity = default;

        uint flags = Win32.FILE_FLAG_BACKUP_SEMANTICS;
        if (!followReparsePoints)
        {
            flags |= Win32.FILE_FLAG_OPEN_REPARSE_POINT;
        }

        using SafeFileHandle handle = Interop.CreateFileW(
            PathHelper.Normalize(path),
            Win32.FILE_READ_ATTRIBUTES,
            Win32.FILE_SHARE_READ | Win32.FILE_SHARE_WRITE | Win32.FILE_SHARE_DELETE,
            0,
            Win32.OPEN_EXISTING,
            flags,
            0);

        if (handle.IsInvalid)
        {
            return false;
        }

        if (!Interop.GetFileInformationByHandle(handle, out ByHandleFileInformation information))
        {
            return false;
        }

        identity = new FileIdentity(information.VolumeSerialNumber, information.FileId);
        return true;
    }
}
