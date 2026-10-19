using BytePulse.Core.Storage;
using Xunit;

namespace BytePulse.Core.Tests.Storage;

public sealed class ChunkedColumnTests
{
    [Fact]
    public void GrowsAcrossChunkBoundaries()
    {
        var column = new ChunkedColumn<long>();
        int count = (ChunkedColumn<long>.ChunkSize * 3) + 17;

        column.EnsureCapacity(count);

        Assert.Equal(count, column.Count);

        for (int i = 0; i < count; i++)
        {
            column[i] = i * 3L;
        }

        for (int i = 0; i < count; i++)
        {
            Assert.Equal(i * 3L, column[i]);
        }
    }

    [Fact]
    public void KeepsReferencesValidAcrossGrowth()
    {
        var column = new ChunkedColumn<long>();
        column.EnsureCapacity(1);
        column[0] = 42;

        ref long reference = ref column[0];

        column.EnsureCapacity(ChunkedColumn<long>.ChunkSize * 4);

        Assert.Equal(42, reference);
    }

    [Fact]
    public void RejectsIndicesBeyondTheCount()
    {
        var column = new ChunkedColumn<long>();
        column.EnsureCapacity(4);

        Assert.Throws<ArgumentOutOfRangeException>(() => column[4]);
        Assert.Throws<ArgumentOutOfRangeException>(() => column[-1]);
    }

    [Fact]
    public void KeepsTheCountWhenAskedToShrink()
    {
        var column = new ChunkedColumn<long>();
        column.EnsureCapacity(100);

        column.EnsureCapacity(10);

        Assert.Equal(100, column.Count);
    }

    [Fact]
    public void CopiesToADenseArray()
    {
        var column = new ChunkedColumn<int>();
        column.EnsureCapacity(3);
        column[0] = 10;
        column[1] = 20;
        column[2] = 30;

        Assert.Equal(new[] { 10, 20, 30 }, column.ToArray());
    }

    [Fact]
    public void SupportsGrowingConcurrently()
    {
        var column = new ChunkedColumn<long>();
        const int workers = 8;
        const int perWorker = 5000;

        Parallel.For(0, workers, worker =>
        {
            for (int i = 0; i < perWorker; i++)
            {
                int index = (worker * perWorker) + i;
                column.EnsureCapacity(index + 1);
                column[index] = index;
            }
        });

        Assert.Equal(workers * perWorker, column.Count);

        for (int i = 0; i < workers * perWorker; i++)
        {
            Assert.Equal(i, column[i]);
        }
    }
}
