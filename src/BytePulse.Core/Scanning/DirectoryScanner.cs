using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using BytePulse.Core.Model;
using BytePulse.Core.Native;

namespace BytePulse.Core.Scanning;

/// <summary>Reads a file system into a tree.</summary>
public interface IScanner
{
    /// <summary>Scans a set of roots.</summary>
    /// <param name="roots">The paths to scan.</param>
    /// <param name="options">How the scan should behave.</param>
    /// <param name="progress">Receives progress updates.</param>
    /// <param name="cancellationToken">Stops the scan.</param>
    Task<ScanResult> ScanAsync(
        IReadOnlyList<string> roots,
        ScanOptions? options = null,
        IProgress<ScanProgress>? progress = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Reads a file system into a tree, using several threads.
/// </summary>
/// <remarks>
/// <para>
/// Each worker reads a directory, adds its entries to the tree, and queues the
/// subdirectories it finds. The tree is built as the scan runs: node ids are handed out by
/// one atomic counter, and each directory's children are linked by the single worker that
/// read that directory, so the hot path takes no locks.
/// </para>
/// <para>
/// A directory that cannot be read is recorded as an error and the scan carries on. One
/// unreadable directory never stops a scan.
/// </para>
/// </remarks>
public sealed class DirectoryScanner : IScanner
{
    private const byte Dot = (byte)'.';

    /// <summary>Scans a set of roots.</summary>
    public ScanResult Scan(
        IReadOnlyList<string> roots,
        ScanOptions? options = null,
        IProgress<ScanProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(roots);

        ScanOptions effective = options ?? new ScanOptions();
        var builder = new ScanTreeBuilder();
        var reporter = new ProgressReporter(progress);
        var followedTargets = new ConcurrentDictionary<FileIdentity, byte>();
        var stopwatch = Stopwatch.StartNew();

        using var queue = new WorkQueue(effective.MaxQueuedDirectories);

        foreach (string root in roots)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            int rootId = builder.AddEntry(-1, 0, CreateRootEntry(root));
            queue.Enqueue(new DirectoryWorkItem(rootId, root, 0));
        }

        int workerCount = Math.Max(1, effective.ThreadCount);
        var workers = new Thread[workerCount];

        for (int i = 0; i < workerCount; i++)
        {
            workers[i] = new Thread(() => WorkerLoop(queue, builder, effective, reporter, followedTargets, cancellationToken))
            {
                IsBackground = true,
                Name = $"BytePulse.Scan.W{i}",
            };

            workers[i].Start();
        }

        foreach (Thread worker in workers)
        {
            worker.Join();
        }

        if (!cancellationToken.IsCancellationRequested
            && (effective.ComputeSizeOnDisk || effective.DetectHardlinks))
        {
            FileDetailsEnricher.Apply(builder, effective, progress, cancellationToken);
        }

        stopwatch.Stop();
        reporter.Report(force: true);

        ScanTree tree = builder.Build(effective.Sort);

        bool isComplete = !cancellationToken.IsCancellationRequested;

        return new ScanResult(
            tree,
            builder.Errors.ToArray(),
            isComplete,
            stopwatch.Elapsed,
            reporter.FilesScanned,
            reporter.DirectoriesScanned,
            reporter.BytesScanned);
    }

    /// <summary>Scans a set of roots on a background thread.</summary>
    public Task<ScanResult> ScanAsync(
        IReadOnlyList<string> roots,
        ScanOptions? options = null,
        IProgress<ScanProgress>? progress = null,
        CancellationToken cancellationToken = default) =>
        Task.Run(() => Scan(roots, options, progress, cancellationToken), cancellationToken);

    private static void WorkerLoop(
        WorkQueue queue,
        ScanTreeBuilder builder,
        ScanOptions options,
        ProgressReporter reporter,
        ConcurrentDictionary<FileIdentity, byte> followedTargets,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            if (!queue.TryDequeue(out DirectoryWorkItem work))
            {
                if (queue.IsEmpty && queue.Outstanding == 0)
                {
                    break;
                }

                continue;
            }

            try
            {
                ReadDirectory(work, queue, builder, options, reporter, followedTargets, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (DirectoryEnumerationException ex)
            {
                builder.RecordError(ex.ToScanError());
            }
            catch (Exception ex)
            {
                builder.RecordError(new ScanError(work.Path, ScanErrorKind.DeviceError, ex.HResult));
            }
            finally
            {
                queue.Complete();
            }
        }
    }

    private static void ReadDirectory(
        DirectoryWorkItem work,
        WorkQueue queue,
        ScanTreeBuilder builder,
        ScanOptions options,
        ProgressReporter reporter,
        ConcurrentDictionary<FileIdentity, byte> followedTargets,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var enumerator = new FindFirstFileEnumerator(work.Path);

        do
        {
            cancellationToken.ThrowIfCancellationRequested();

            DirectoryEntry entry = enumerator.Current;
            ReadOnlySpan<byte> name = entry.NameUtf16;

            if (IsCurrentOrParentDirectory(name))
            {
                continue;
            }

            if (!options.IncludeHiddenFiles && (entry.Attributes & Win32.FILE_ATTRIBUTE_HIDDEN) != 0)
            {
                continue;
            }

            if (!options.IncludeSystemFiles && (entry.Attributes & Win32.FILE_ATTRIBUTE_SYSTEM) != 0)
            {
                continue;
            }

            bool isDirectory = entry.IsDirectory;
            bool isReparsePoint = entry.IsReparsePoint;
            long size = entry.LogicalSize;

            int childId = builder.AddEntry(work.NodeId, work.Depth + 1, entry);
            reporter.AddEntry(size, isDirectory);

            if (!isDirectory)
            {
                continue;
            }

            if (isReparsePoint && !options.FollowReparsePoints)
            {
                continue;
            }

            if (work.Depth + 1 > options.MaxDepth)
            {
                continue;
            }

            string childName = Encoding.Unicode.GetString(name);
            string childPath = Path.Combine(work.Path, childName);

            if (isReparsePoint && !TryFollowReparsePoint(childPath, followedTargets))
            {
                continue;
            }

            queue.Enqueue(new DirectoryWorkItem(childId, childPath, work.Depth + 1));
        }
        while (enumerator.MoveNext());

        builder.MarkDirectoryScanned(work.NodeId);
        reporter.Report(force: false);
    }

    private static DirectoryEntry CreateRootEntry(string path) => new(
        Encoding.Unicode.GetBytes(path),
        LogicalSize: 0,
        AllocatedSize: 0,
        Attributes: Win32.FILE_ATTRIBUTE_DIRECTORY,
        FileId: 0,
        LastWriteTicks: 0,
        CreationTicks: 0,
        AccessTicks: 0,
        IsDirectory: true,
        IsReparsePoint: false);

    /// <summary>
    /// Whether a reparse point may be followed.
    /// </summary>
    /// <remarks>
    /// The target of the link is identified by opening it, and the identity is recorded.
    /// A link whose target has already been followed is a loop, so it is skipped. That
    /// keeps a junction that points at one of its own ancestors from being followed for
    /// ever.
    /// </remarks>
    private static bool TryFollowReparsePoint(
        string path,
        ConcurrentDictionary<FileIdentity, byte> followedTargets)
    {
        if (!FileIdentityResolver.TryResolve(path, followReparsePoints: true, out FileIdentity identity))
        {
            // A link that cannot be opened cannot be followed safely.
            return false;
        }

        return followedTargets.TryAdd(identity, 0);
    }

    /// <summary>
    /// Whether the entry is the <c>.</c> or <c>..</c> entry that every directory reports.
    /// </summary>
    /// <remarks>
    /// The name is UTF-16, so each character occupies two bytes and the high byte of a
    /// character below the basic multilingual plane is zero.
    /// </remarks>
    private static bool IsCurrentOrParentDirectory(ReadOnlySpan<byte> name)
    {
        if (name.Length == 2)
        {
            return name[0] == Dot && name[1] == 0;
        }

        if (name.Length == 4)
        {
            return name[0] == Dot && name[1] == 0 && name[2] == Dot && name[3] == 0;
        }

        return false;
    }

    /// <summary>
    /// Collects the counters the scan reports and throttles the progress updates.
    /// </summary>
    private sealed class ProgressReporter
    {
        private static readonly TimeSpan ReportInterval = TimeSpan.FromMilliseconds(100);

        private readonly IProgress<ScanProgress>? _progress;
        private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
        private long _files;
        private long _directories;
        private long _bytes;
        private long _lastReport;

        public ProgressReporter(IProgress<ScanProgress>? progress)
        {
            _progress = progress;
        }

        public long FilesScanned => Interlocked.Read(ref _files);

        public long DirectoriesScanned => Interlocked.Read(ref _directories);

        public long BytesScanned => Interlocked.Read(ref _bytes);

        public void AddEntry(long size, bool isDirectory)
        {
            Interlocked.Add(ref _bytes, size);

            if (isDirectory)
            {
                Interlocked.Increment(ref _directories);
            }
            else
            {
                Interlocked.Increment(ref _files);
            }
        }

        public void Report(bool force)
        {
            if (_progress is null)
            {
                return;
            }

            long now = Environment.TickCount64;
            long last = Interlocked.Read(ref _lastReport);

            if (!force && now - last < (long)ReportInterval.TotalMilliseconds)
            {
                return;
            }

            if (Interlocked.CompareExchange(ref _lastReport, now, last) != last)
            {
                return;
            }

            _progress.Report(new ScanProgress(
                FilesScanned,
                DirectoriesScanned,
                BytesScanned,
                _stopwatch.Elapsed,
                force));
        }
    }
}
