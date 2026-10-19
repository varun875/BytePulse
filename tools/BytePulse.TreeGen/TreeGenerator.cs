using System.Buffers;
using System.Diagnostics;

namespace BytePulse.TreeGen;

/// <summary>How a synthetic tree is laid out.</summary>
/// <param name="DirectoryCount">How many directories to create.</param>
/// <param name="FilesPerDirectory">How many files each directory holds.</param>
/// <param name="FileSize">The size of each file in bytes.</param>
/// <param name="Seed">The seed for the random layout.</param>
public readonly record struct TreeLayout(int DirectoryCount, int FilesPerDirectory, int FileSize, int Seed)
{
    /// <summary>The number of files the layout produces.</summary>
    public int FileCount => DirectoryCount * FilesPerDirectory;

    /// <summary>A layout with the given total number of files.</summary>
    public static TreeLayout ForFileCount(int fileCount, int fileSize = 4096, int seed = 12345)
    {
        const int FilesPerDirectory = 50;
        int directories = Math.Max(1, fileCount / FilesPerDirectory);
        return new TreeLayout(directories, FilesPerDirectory, fileSize, seed);
    }
}

/// <summary>What a generated tree turned out to be.</summary>
/// <param name="Root">The root of the tree.</param>
/// <param name="FileCount">How many files were written.</param>
/// <param name="DirectoryCount">How many directories were created.</param>
/// <param name="TotalBytes">How many bytes were written.</param>
/// <param name="Elapsed">How long it took.</param>
public readonly record struct TreeGenerationResult(
    string Root,
    int FileCount,
    int DirectoryCount,
    long TotalBytes,
    TimeSpan Elapsed);

/// <summary>Creates a directory tree with a known number of files.</summary>
public static class TreeGenerator
{
    /// <summary>
    /// Writes a tree of the given shape under <paramref name="root"/>.
    /// </summary>
    /// <remarks>
    /// The files are written with one shared buffer, so generating a large tree is quick
    /// and does not fill the disk faster than the file system can keep up.
    /// </remarks>
    public static TreeGenerationResult Generate(string root, TreeLayout layout)
    {
        var stopwatch = Stopwatch.StartNew();
        var random = new Random(layout.Seed);

        Directory.CreateDirectory(root);

        byte[] buffer = new byte[Math.Min(layout.FileSize, 64 * 1024)];
        random.NextBytes(buffer);

        long totalBytes = 0;
        int fileCount = 0;

        for (int d = 0; d < layout.DirectoryCount; d++)
        {
            string directory = Path.Combine(root, $"dir{d:D6}");
            Directory.CreateDirectory(directory);

            for (int f = 0; f < layout.FilesPerDirectory; f++)
            {
                string path = Path.Combine(directory, $"file{f:D4}.dat");

                using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 64 * 1024))
                {
                    long written = 0;
                    while (written < layout.FileSize)
                    {
                        int chunk = (int)Math.Min(buffer.Length, layout.FileSize - written);
                        stream.Write(buffer, 0, chunk);
                        written += chunk;
                    }
                }

                totalBytes += layout.FileSize;
                fileCount++;
            }
        }

        stopwatch.Stop();

        return new TreeGenerationResult(root, fileCount, layout.DirectoryCount, totalBytes, stopwatch.Elapsed);
    }

    /// <summary>
    /// Counts the files and directories under a path, so a benchmark can check that the
    /// tree it is measuring is the tree it thinks it is.
    /// </summary>
    public static (int Files, int Directories, long Bytes) Measure(string root)
    {
        int files = 0;
        int directories = 0;
        long bytes = 0;

        var stack = new Stack<string>();
        stack.Push(root);

        while (stack.Count > 0)
        {
            string current = stack.Pop();

            foreach (string entry in Directory.EnumerateFileSystemEntries(current))
            {
                if (Directory.Exists(entry))
                {
                    directories++;
                    stack.Push(entry);
                }
                else
                {
                    files++;
                    bytes += new FileInfo(entry).Length;
                }
            }
        }

        return (files, directories, bytes);
    }
}
