using System.Collections.Concurrent;

namespace BytePulse.Core.Scanning;

/// <summary>A directory waiting to be read.</summary>
/// <param name="NodeId">The node the directory's entries will be added to.</param>
/// <param name="Path">The directory's path.</param>
/// <param name="Depth">The directory's depth below the scan root.</param>
internal readonly record struct DirectoryWorkItem(int NodeId, string Path, int Depth);

/// <summary>
/// A queue of directories waiting to be read, shared by the workers of a scan.
/// </summary>
/// <remarks>
/// <para>
/// The number of directories that may be outstanding at once is bounded, so a tree with
/// very many directories cannot make the walk use an unbounded amount of memory. A worker
/// waits for room before it queues more work, and the wait always ends because the
/// directories already in flight are read and released.
/// </para>
/// <para>
/// The queue never blocks a worker that is reading a directory, so the workers can never
/// deadlock against each other.
/// </para>
/// </remarks>
internal sealed class WorkQueue : IDisposable
{
    private readonly ConcurrentQueue<DirectoryWorkItem> _items = new();
    private readonly SemaphoreSlim _available = new(0);
    private readonly int _maxOutstanding;
    private int _outstanding;

    public WorkQueue(int maxOutstanding)
    {
        _maxOutstanding = Math.Max(1, maxOutstanding);
    }

    /// <summary>The number of directories queued or being read.</summary>
    public int Outstanding => Volatile.Read(ref _outstanding);

    /// <summary>Whether the queue holds no directories.</summary>
    public bool IsEmpty => _items.IsEmpty;

    /// <summary>Queues a directory to be read.</summary>
    public void Enqueue(DirectoryWorkItem item)
    {
        while (Volatile.Read(ref _outstanding) >= _maxOutstanding)
        {
            Thread.Sleep(1);
        }

        Interlocked.Increment(ref _outstanding);
        _items.Enqueue(item);
        _available.Release();
    }

    /// <summary>
    /// Takes the next directory to read.
    /// </summary>
    /// <returns>False when nothing became available within the wait.</returns>
    public bool TryDequeue(out DirectoryWorkItem item)
    {
        if (_available.Wait(20))
        {
            if (_items.TryDequeue(out item))
            {
                return true;
            }
        }

        item = default;
        return false;
    }

    /// <summary>Releases the slot a finished directory was holding.</summary>
    public void Complete()
    {
        Interlocked.Decrement(ref _outstanding);
    }

    public void Dispose()
    {
        _available.Dispose();
    }
}
