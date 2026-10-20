using System.Collections.Concurrent;

namespace BytePulse.Core.Scanning;

/// <summary>A directory waiting to be read.</summary>
/// <param name="NodeId">The node the directory's entries will be added to.</param>
/// <param name="Path">The directory's path.</param>
/// <param name="Depth">The directory's depth below the scan root.</param>
internal readonly record struct DirectoryWorkItem(int NodeId, string Path, int Depth);

/// <summary>
/// The directories a scan has found but not yet read, shared by its workers.
/// </summary>
/// <remarks>
/// <para>
/// This is deliberately the simplest thing that can work. A directory is counted when it
/// is queued and uncounted when it has been read, so <see cref="Pending"/> reaches zero
/// only when there is nothing left to do anywhere. A worker that finds the queue empty
/// keeps waiting while <see cref="Pending"/> is not zero, because a non-zero count means
/// some directory is either queued or being read by another worker, and both of those
/// states always end.
/// </para>
/// <para>
/// Nothing here blocks. An earlier version bounded the queue and made a worker wait for
/// room before it could queue more, which deadlocked as soon as the queue filled with
/// work that only the waiting workers could have drained.
/// </para>
/// </remarks>
internal sealed class WorkQueue
{
    private readonly ConcurrentQueue<DirectoryWorkItem> _items = new();
    private int _pending;

    /// <summary>The number of directories queued or being read.</summary>
    public int Pending => Volatile.Read(ref _pending);

    /// <summary>Whether the queue holds no directories.</summary>
    public bool IsEmpty => _items.IsEmpty;

    /// <summary>Queues a directory and counts it as work to be done.</summary>
    public void Add(DirectoryWorkItem item)
    {
        Interlocked.Increment(ref _pending);
        _items.Enqueue(item);
    }

    /// <summary>Takes the next directory, if there is one.</summary>
    public bool TryTake(out DirectoryWorkItem item) => _items.TryDequeue(out item);

    /// <summary>Records that a queued directory has been read.</summary>
    public void Complete() => Interlocked.Decrement(ref _pending);
}
