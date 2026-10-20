using System.Collections.Concurrent;
using BytePulse.Core.Model;
using BytePulse.Core.Native;

namespace BytePulse.Core.Scanning;

/// <summary>
/// Fills in the details that reading a directory does not provide: the space a file
/// occupies on disk, how many hard links point at it, and which of those links should
/// account for it.
/// </summary>
/// <remarks>
/// <para>
/// Directory enumeration reports a file's logical size but not its allocation, so this
/// pass has to open each file. That is far more expensive than the walk itself, which is
/// why it only runs when the options ask for it.
/// </para>
/// <para>
/// Hard links share one file, so the links are grouped by the file's identity on its
/// volume and exactly one of them is marked as the one that accounts for the file's size.
/// The lowest node id wins, so the choice is reproducible.
/// </para>
/// </remarks>
public static class FileDetailsEnricher
{
    /// <summary>
    /// Reads the details of every file in the tree.
    /// </summary>
    /// <param name="builder">The tree being built.</param>
    /// <param name="options">How the scan should behave.</param>
    /// <param name="progress">Receives progress updates.</param>
    /// <param name="cancellationToken">Stops the pass.</param>
    /// <returns>The number of files that could not be opened.</returns>
    public static int Apply(
        ScanTreeBuilder builder,
        ScanOptions options,
        IProgress<ScanProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        builder.FreezeNames();

        int nodeCount = builder.NodeCount;
        var identities = new FileIdentity[nodeCount];
        var failures = new ConcurrentBag<int>();
        long processed = 0;

        var parallelOptions = new ParallelOptions
        {
            CancellationToken = cancellationToken,
            MaxDegreeOfParallelism = Math.Max(1, options.ThreadCount),
        };

        Parallel.For(0, nodeCount, parallelOptions, nodeId =>
        {
            if (builder.IsDirectory(nodeId))
            {
                return;
            }

            if (ReadDetails(builder, nodeId, out FileIdentity identity))
            {
                identities[nodeId] = identity;
            }
            else
            {
                failures.Add(nodeId);
            }

            long count = Interlocked.Increment(ref processed);
            if ((count & 0x7FF) == 0)
            {
                progress?.Report(new ScanProgress(count, 0, 0, TimeSpan.Zero, false));
            }
        });

        if (options.DetectHardlinks)
        {
            MarkPrimaryLinks(builder, identities);
        }

        return failures.Count;
    }

    private static bool ReadDetails(ScanTreeBuilder builder, int nodeId, out FileIdentity identity)
    {
        identity = default;

        string path = builder.GetPath(nodeId);

        using var handle = Interop.CreateFileW(
            PathHelper.Normalize(path),
            Win32.FILE_READ_ATTRIBUTES,
            Win32.FILE_SHARE_READ | Win32.FILE_SHARE_WRITE | Win32.FILE_SHARE_DELETE,
            0,
            Win32.OPEN_EXISTING,
            Win32.FILE_FLAG_BACKUP_SEMANTICS,
            0);

        if (handle.IsInvalid)
        {
            return false;
        }

        if (Interop.GetFileInformationByHandleEx(
            handle,
            Win32.FileStandardInfo,
            out FileStandardInformation standard,
            (uint)System.Runtime.InteropServices.Marshal.SizeOf<FileStandardInformation>()))
        {
            builder.SetAllocatedSize(nodeId, standard.AllocationSize);

            if (standard.NumberOfLinks > 1)
            {
                builder.SetLinkCount(nodeId, (ushort)Math.Min(standard.NumberOfLinks, ushort.MaxValue));
                builder.SetFlag(nodeId, NodeFlags.HasHardLinks, true);
            }
        }

        if (!Interop.GetFileInformationByHandle(handle, out ByHandleFileInformation information))
        {
            return false;
        }

        identity = new FileIdentity(information.VolumeSerialNumber, information.FileId);
        return true;
    }

    /// <summary>
    /// Marks one link of each hard link group as the one that accounts for the file.
    /// </summary>
    /// <remarks>
    /// This runs after the parallel pass and in node id order, so the link that wins is
    /// always the same one no matter how the reading was scheduled.
    /// </remarks>
    private static void MarkPrimaryLinks(ScanTreeBuilder builder, FileIdentity[] identities)
    {
        var seen = new HashSet<FileIdentity>();

        for (int nodeId = 0; nodeId < identities.Length; nodeId++)
        {
            FileIdentity identity = identities[nodeId];
            if (identity == default)
            {
                continue;
            }

            if ((builder.GetFlags(nodeId) & NodeFlags.HasHardLinks) == 0)
            {
                continue;
            }

            builder.SetFlag(nodeId, NodeFlags.IsPrimaryLink, seen.Add(identity));
        }
    }
}
