using System.Text.Json;
using BytePulse.Core.Model;
using BytePulse.Core.Scanning;

namespace BytePulse.Cli.Output;

/// <summary>The results as a JSON document.</summary>
internal static class JsonReport
{
    public static void Write(ScanResult result)
    {
        using var stream = Console.OpenStandardOutput();
        using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });

        ScanTree tree = result.Tree;

        writer.WriteStartObject();
        writer.WriteString("scannedAt", DateTimeOffset.UtcNow);
        writer.WriteBoolean("isComplete", result.IsComplete);
        writer.WriteNumber("elapsedMilliseconds", result.Elapsed.TotalMilliseconds);
        writer.WriteNumber("totalBytes", tree.TotalLogicalSize);
        writer.WriteNumber("totalFiles", tree.TotalFileCount);
        writer.WriteNumber("totalDirectories", tree.TotalDirectoryCount);

        writer.WriteStartArray("roots");
        foreach (int root in tree.RootNodeIds)
        {
            WriteNode(writer, tree, root);
        }

        writer.WriteEndArray();

        writer.WriteStartArray("errors");
        foreach (ScanError error in result.Errors)
        {
            writer.WriteStartObject();
            writer.WriteString("path", error.Path);
            writer.WriteString("kind", error.Kind.ToString());
            writer.WriteNumber("nativeErrorCode", error.NativeErrorCode);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteEndObject();

        writer.Flush();
        stream.WriteByte((byte)'\n');
    }

    private static void WriteNode(Utf8JsonWriter writer, ScanTree tree, int nodeId)
    {
        ScanNode node = tree.GetNode(nodeId);

        writer.WriteStartObject();
        writer.WriteString("name", node.Name);
        writer.WriteString("path", tree.GetPath(nodeId));
        writer.WriteBoolean("isDirectory", node.IsDirectory);
        writer.WriteNumber("size", node.LogicalSize);
        writer.WriteNumber("sizeOnDisk", node.TotalAllocatedSize);
        writer.WriteNumber("totalBytes", node.TotalLogicalSize);
        writer.WriteNumber("totalFiles", node.TotalFileCount);
        writer.WriteNumber("totalDirectories", node.TotalDirectoryCount);
        writer.WriteString("modified", node.LastWriteTimeUtc);

        writer.WriteStartArray("children");
        foreach (int child in tree.GetSortedChildren(nodeId))
        {
            WriteNode(writer, tree, child);
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }
}
