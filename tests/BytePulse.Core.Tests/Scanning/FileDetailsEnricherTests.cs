using System.Runtime.InteropServices;
using BytePulse.Core.Model;
using BytePulse.Core.Scanning;
using Xunit;

namespace BytePulse.Core.Tests.Scanning;

public sealed class FileDetailsEnricherTests : IDisposable
{
    private readonly string _root;

    public FileDetailsEnricherTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "BytePulseTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        TryDelete(_root);
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void ReadsTheSpaceAFileOccupiesOnDisk()
    {
        CreateFile("a.bin", 5000);

        ScanResult result = Scan(new ScanOptions { ComputeSizeOnDisk = true });

        ScanNode file = FindFile(result, "a.bin");
        Assert.True((file.Flags & NodeFlags.AllocatedSizeKnown) != 0);
        Assert.True(file.AllocatedSize > 0, "expected the allocation size to be filled in");
    }

    [Fact]
    public void TheAllocatedTotalIsAtLeastTheLogicalTotal()
    {
        CreateFile("a.bin", 5000);
        CreateFile("b.bin", 9000);

        ScanResult result = Scan(new ScanOptions { ComputeSizeOnDisk = true });

        Assert.True(result.Tree.TotalAllocatedSize >= result.Tree.TotalLogicalSize);
    }

    [Fact]
    public void DoesNotReadAllocatedSizesUnlesAsked()
    {
        CreateFile("a.bin", 5000);

        ScanResult result = Scan(new ScanOptions());

        Assert.Equal(0, result.Tree.TotalAllocatedSize);
    }

    [Fact]
    public void CountsAHardLinkedFileOnce()
    {
        string original = CreateFile("original.bin", 8192);
        string link = Path.Combine(_root, "link.bin");

        if (!TryCreateHardLink(link, original))
        {
            return;
        }

        ScanResult withoutLinks = Scan(new ScanOptions());
        ScanResult withLinks = Scan(new ScanOptions { DetectHardlinks = true, ComputeSizeOnDisk = true });

        // Both names are in the tree either way.
        Assert.Equal(2, withoutLinks.Tree.TotalFileCount);

        // With hard links understood, the file accounts for its size once.
        Assert.Equal(1, withLinks.Tree.TotalFileCount);
        Assert.Equal(8192, withLinks.Tree.TotalLogicalSize);
    }

    [Fact]
    public void MarksExactlyOneLinkAsThePrimaryOne()
    {
        string original = CreateFile("original.bin", 4096);
        string link = Path.Combine(_root, "link.bin");

        if (!TryCreateHardLink(link, original))
        {
            return;
        }

        ScanResult result = Scan(new ScanOptions { DetectHardlinks = true, ComputeSizeOnDisk = true });

        int primary = 0;
        int linked = 0;

        for (int id = 0; id < result.Tree.NodeCount; id++)
        {
            NodeFlags flags = result.Tree.GetNode(id).Flags;

            if ((flags & NodeFlags.HasHardLinks) != 0)
            {
                linked++;

                if ((flags & NodeFlags.IsPrimaryLink) != 0)
                {
                    primary++;
                }
            }
        }

        Assert.Equal(2, linked);
        Assert.Equal(1, primary);
    }

    [Fact]
    public void ThePrimaryLinkIsTheSameOnEveryRun()
    {
        string original = CreateFile("original.bin", 4096);
        string link = Path.Combine(_root, "link.bin");

        if (!TryCreateHardLink(link, original))
        {
            return;
        }

        string FirstPrimary()
        {
            ScanResult result = Scan(new ScanOptions { DetectHardlinks = true, ComputeSizeOnDisk = true });

            for (int id = 0; id < result.Tree.NodeCount; id++)
            {
                ScanNode node = result.Tree.GetNode(id);

                if ((node.Flags & NodeFlags.IsPrimaryLink) != 0)
                {
                    return node.Name;
                }
            }

            return string.Empty;
        }

        Assert.Equal(FirstPrimary(), FirstPrimary());
    }

    [Fact]
    public void RecordsHowManyLinksAFileHas()
    {
        string original = CreateFile("original.bin", 4096);
        string link = Path.Combine(_root, "link.bin");

        if (!TryCreateHardLink(link, original))
        {
            return;
        }

        ScanResult result = Scan(new ScanOptions { DetectHardlinks = true, ComputeSizeOnDisk = true });

        ScanNode file = FindFile(result, "original.bin");
        Assert.True(file.LinkCount >= 2, $"expected at least two links, got {file.LinkCount}");
    }

    private ScanResult Scan(ScanOptions options) =>
        new DirectoryScanner().Scan(new[] { _root }, options);

    private ScanNode FindFile(ScanResult result, string name)
    {
        for (int id = 0; id < result.Tree.NodeCount; id++)
        {
            ScanNode node = result.Tree.GetNode(id);

            if (node.Name == name)
            {
                return node;
            }
        }

        throw new InvalidOperationException($"the tree does not contain {name}");
    }

    private string CreateFile(string relativePath, int size)
    {
        string path = Path.Combine(_root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, new byte[size]);
        return path;
    }

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode, ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateHardLinkW(string fileName, string existingFileName, nint securityAttributes);

    private static bool TryCreateHardLink(string linkPath, string targetPath)
    {
        try
        {
            return CreateHardLinkW(linkPath, targetPath, 0) && File.Exists(linkPath);
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            foreach (string entry in Directory.GetFileSystemEntries(path))
            {
                if (Directory.Exists(entry))
                {
                    TryDelete(entry);
                }
                else
                {
                    File.Delete(entry);
                }
            }

            Directory.Delete(path);
        }
        catch (Exception)
        {
        }
    }
}
