using BytePulse.Core.Native;
using BytePulse.Core.Scanning;

namespace BytePulse.Benchmarks;

/// <summary>
/// A single threaded recursive walk, which is the strategy a typical disk usage tool
/// uses and the baseline the parallel scanner is measured against.
/// </summary>
internal static class NaiveWalker
{
    /// <summary>
    /// Walks the tree with the framework's own enumeration, allocating a string for every
    /// entry.
    /// </summary>
    public static (long Files, long Directories, long Bytes) UsingFrameworkEnumeration(string root)
    {
        long files = 0;
        long directories = 0;
        long bytes = 0;

        foreach (string entry in Directory.EnumerateFileSystemEntries(root))
        {
            if (Directory.Exists(entry))
            {
                directories++;
                (long f, long d, long b) = UsingFrameworkEnumeration(entry);
                files += f;
                directories += d;
                bytes += b;
            }
            else
            {
                files++;
                bytes += new FileInfo(entry).Length;
            }
        }

        return (files, directories, bytes);
    }

    /// <summary>
    /// Walks the tree with the raw file system calls, one thread, and no per entry
    /// allocation beyond the recursion.
    /// </summary>
    public static (long Files, long Directories, long Bytes) UsingNativeEnumeration(string root)
    {
        long files = 0;
        long directories = 0;
        long bytes = 0;

        var stack = new Stack<string>();
        stack.Push(root);

        while (stack.Count > 0)
        {
            string current = stack.Pop();

            using var enumerator = new FindFirstFileEnumerator(current);

            do
            {
                DirectoryEntry entry = enumerator.Current;

                if (IsCurrentOrParentDirectory(entry.NameUtf16))
                {
                    continue;
                }

                if (entry.IsDirectory)
                {
                    directories++;
                    string name = System.Text.Encoding.Unicode.GetString(entry.NameUtf16);
                    stack.Push(Path.Combine(current, name));
                }
                else
                {
                    files++;
                    bytes += entry.LogicalSize;
                }
            }
            while (enumerator.MoveNext());
        }

        return (files, directories, bytes);
    }

    /// <summary>
    /// Whether the entry is the <c>.</c> or <c>..</c> entry that every directory reports.
    /// The name is UTF-16, so each character occupies two bytes.
    /// </summary>
    private static bool IsCurrentOrParentDirectory(ReadOnlySpan<byte> name)
    {
        if (name.Length == 2)
        {
            return name[0] == (byte)'.' && name[1] == 0;
        }

        if (name.Length == 4)
        {
            return name[0] == (byte)'.' && name[1] == 0 && name[2] == (byte)'.' && name[3] == 0;
        }

        return false;
    }
}
