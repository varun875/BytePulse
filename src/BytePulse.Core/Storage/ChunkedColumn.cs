using System.Runtime.CompilerServices;

namespace BytePulse.Core.Storage;

/// <summary>
/// A growable, chunked column of unmanaged values.
/// </summary>
/// <remarks>
/// <para>
/// Values live in fixed-size chunks that are never moved or reallocated, so a
/// <c>ref</c> handed out by the indexer stays valid for the lifetime of the column. That is
/// what lets the scanner append to several columns concurrently without copying.
/// </para>
/// <para>
/// Chunks are allocated on the pinned object heap so the GC never relocates them and so
/// large allocations skip the zeroing cost.
/// </para>
/// </remarks>
internal sealed class ChunkedColumn<T> where T : unmanaged
{
    internal const int ChunkShift = 14;
    internal const int ChunkSize = 1 << ChunkShift;
    internal const int ChunkMask = ChunkSize - 1;

    private T[][] _chunks = new T[16][];
    private int _chunkCount;
    private int _count;
    private readonly object _growLock = new();

    /// <summary>The number of values the column can hold.</summary>
    internal int Count => Volatile.Read(ref _count);

    internal ref T this[int index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            if ((uint)index >= (uint)Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return ref _chunks[index >> ChunkShift][index & ChunkMask];
        }
    }

    /// <summary>Grows the column so that it can hold at least <paramref name="count"/> values.</summary>
    internal void EnsureCapacity(int count)
    {
        int required = (count + ChunkMask) >> ChunkShift;
        if (required > Volatile.Read(ref _chunkCount))
        {
            lock (_growLock)
            {
                while (Volatile.Read(ref _chunkCount) < required)
                {
                    int index = Volatile.Read(ref _chunkCount);
                    if (index >= _chunks.Length)
                    {
                        Array.Resize(ref _chunks, _chunks.Length * 2);
                    }

                    _chunks[index] = GC.AllocateUninitializedArray<T>(ChunkSize, pinned: true);
                    Volatile.Write(ref _chunkCount, index + 1);
                }
            }
        }

        IncreaseCount(count);
    }

    /// <summary>Copies the column into a dense array. Used to snapshot columns into a tree.</summary>
    internal T[] ToArray()
    {
        var result = new T[Count];
        for (int i = 0; i < result.Length; i++)
        {
            result[i] = this[i];
        }

        return result;
    }

    private void IncreaseCount(int count)
    {
        int current = Volatile.Read(ref _count);
        while (count > current)
        {
            int observed = Interlocked.CompareExchange(ref _count, count, current);
            if (observed == current)
            {
                return;
            }

            current = observed;
        }
    }
}
