using System.Text;
using BytePulse.Core.Native;
using BytePulse.Core.Scanning;
using Xunit;

namespace BytePulse.Core.Tests.Native;

public sealed class FindFirstFileEnumeratorTests : IDisposable
{
    private readonly string _root;

    public FindFirstFileEnumeratorTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "BytePulseTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(_root, "sub"));
        File.WriteAllText(Path.Combine(_root, "alpha.txt"), "hello");
        File.WriteAllBytes(Path.Combine(_root, "beta.bin"), new byte[4096]);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
        }

        GC.SuppressFinalize(this);
    }

    [Fact]
    public void EnumeratesEveryEntryInTheDirectory()
    {
        var found = new List<string>();

        using (var enumerator = new FindFirstFileEnumerator(_root))
        {
            do
            {
                found.Add(ReadName(enumerator.Current));
            }
            while (enumerator.MoveNext());
        }

        // The file system always reports the current and parent directory entries.
        Assert.Contains(".", found);
        Assert.Contains("..", found);
        Assert.Contains("alpha.txt", found);
        Assert.Contains("beta.bin", found);
        Assert.Contains("sub", found);
        Assert.Equal(5, found.Count);
    }

    [Fact]
    public void AgreesWithTheFrameworkAboutWhichEntriesExist()
    {
        var found = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        using (var enumerator = new FindFirstFileEnumerator(_root))
        {
            do
            {
                found.Add(ReadName(enumerator.Current));
            }
            while (enumerator.MoveNext());
        }

        var expected = new HashSet<string>(
            Directory.GetFileSystemEntries(_root).Select(Path.GetFileName)!,
            StringComparer.OrdinalIgnoreCase);

        expected.Add(".");
        expected.Add("..");

        Assert.Equal(expected, found);
    }

    [Fact]
    public void ReportsTheLogicalSizeOfAFile()
    {
        using var enumerator = new FindFirstFileEnumerator(_root);

        long? size = null;
        do
        {
            if (ReadName(enumerator.Current) == "beta.bin")
            {
                size = enumerator.Current.LogicalSize;
                break;
            }
        }
        while (enumerator.MoveNext());

        Assert.Equal(4096, size);
    }

    [Fact]
    public void ReportsDirectoriesAsDirectories()
    {
        using var enumerator = new FindFirstFileEnumerator(_root);

        bool? isDirectory = null;
        do
        {
            if (ReadName(enumerator.Current) == "sub")
            {
                isDirectory = enumerator.Current.IsDirectory;
                break;
            }
        }
        while (enumerator.MoveNext());

        Assert.True(isDirectory);
    }

    [Fact]
    public void ReportsFilesAsNotDirectories()
    {
        using var enumerator = new FindFirstFileEnumerator(_root);

        bool? isDirectory = null;
        do
        {
            if (ReadName(enumerator.Current) == "alpha.txt")
            {
                isDirectory = enumerator.Current.IsDirectory;
                break;
            }
        }
        while (enumerator.MoveNext());

        Assert.False(isDirectory);
    }

    [Fact]
    public void HandlesNamesOutsideAscii()
    {
        string name = "caf\u00e9-\u4e2d\u6587.txt";
        File.WriteAllText(Path.Combine(_root, name), "x");

        using var enumerator = new FindFirstFileEnumerator(_root);

        bool found = false;
        do
        {
            if (ReadName(enumerator.Current) == name)
            {
                found = true;
                break;
            }
        }
        while (enumerator.MoveNext());

        Assert.True(found);
    }

    [Fact]
    public void ReportsAMissingDirectoryAsNotFound()
    {
        string missing = Path.Combine(_root, "does-not-exist");

        DirectoryEnumerationException exception = Assert.Throws<DirectoryEnumerationException>(
            () => new FindFirstFileEnumerator(missing));

        Assert.Equal(ScanErrorKind.NotFound, exception.Kind);
        Assert.Equal(missing, exception.Path);
    }

    [Fact]
    public void ReportsAFileAsInvalidPath()
    {
        string file = Path.Combine(_root, "alpha.txt");

        DirectoryEnumerationException exception = Assert.Throws<DirectoryEnumerationException>(
            () => new FindFirstFileEnumerator(file));

        Assert.Equal(ScanErrorKind.InvalidPath, exception.Kind);
    }

    [Fact]
    public void StopsAtTheEndOfTheDirectory()
    {
        using var enumerator = new FindFirstFileEnumerator(_root);

        int count = 0;
        do
        {
            count++;
        }
        while (enumerator.MoveNext());

        Assert.Equal(5, count);

        // Once exhausted, further calls keep returning false.
        Assert.False(enumerator.MoveNext());
        Assert.False(enumerator.MoveNext());
    }

    [Theory]
    [InlineData(5, ScanErrorKind.AccessDenied)]
    [InlineData(65, ScanErrorKind.AccessDenied)]
    [InlineData(2, ScanErrorKind.NotFound)]
    [InlineData(3, ScanErrorKind.NotFound)]
    [InlineData(123, ScanErrorKind.InvalidPath)]
    [InlineData(87, ScanErrorKind.InvalidPath)]
    [InlineData(267, ScanErrorKind.InvalidPath)]
    [InlineData(1117, ScanErrorKind.DeviceError)]
    public void MapsWin32ErrorCodes(int error, ScanErrorKind expected)
    {
        Assert.Equal(expected, PathHelper.MapError(error));
    }

    private static string ReadName(DirectoryEntry entry) =>
        Encoding.Unicode.GetString(entry.NameUtf16);
}
