using System.Buffers.Binary;

namespace BytePulse.Core.Native;

/// <summary>The fields of a <c>WIN32_FIND_DATAW</c> that the scanner needs.</summary>
internal readonly struct FindDataEntry
{
    /// <summary>The offset of the name within the find-data buffer.</summary>
    public int NameOffset { get; init; }

    /// <summary>The length of the name in bytes, without the terminator.</summary>
    public int NameLength { get; init; }

    /// <summary>The file's logical size in bytes.</summary>
    public long Size { get; init; }

    /// <summary>The raw Win32 file attributes.</summary>
    public uint Attributes { get; init; }

    /// <summary>Creation time, in UTC ticks.</summary>
    public long CreationTicks { get; init; }

    /// <summary>Last access time, in UTC ticks.</summary>
    public long LastAccessTicks { get; init; }

    /// <summary>Last write time, in UTC ticks.</summary>
    public long LastWriteTicks { get; init; }
}

/// <summary>
/// Reads the fields of a <c>WIN32_FIND_DATAW</c> structure.
/// </summary>
/// <remarks>
/// Every field is read at an explicit offset with <see cref="BinaryPrimitives"/> rather
/// than by marshalling the structure, so the layout is checked by the tests rather than
/// assumed, and the file name can be used in place without being decoded.
/// </remarks>
internal static class FindDataParser
{
    private const int AttributesOffset = 0;
    private const int CreationTimeOffset = 4;
    private const int LastAccessTimeOffset = 12;
    private const int LastWriteTimeOffset = 20;
    private const int SizeHighOffset = 28;
    private const int SizeLowOffset = 32;

    /// <summary>Reads an entry from the find-data buffer.</summary>
    public static FindDataEntry Parse(ReadOnlySpan<byte> data)
    {
        return new FindDataEntry
        {
            Attributes = BinaryPrimitives.ReadUInt32LittleEndian(data[AttributesOffset..]),
            CreationTicks = ReadFileTime(data, CreationTimeOffset),
            LastAccessTicks = ReadFileTime(data, LastAccessTimeOffset),
            LastWriteTicks = ReadFileTime(data, LastWriteTimeOffset),
            Size = ((long)BinaryPrimitives.ReadUInt32LittleEndian(data[SizeHighOffset..]) << 32)
                | BinaryPrimitives.ReadUInt32LittleEndian(data[SizeLowOffset..]),
            NameOffset = Win32.FindDataNameOffset,
            NameLength = MeasureNameLength(data),
        };
    }

    /// <summary>
    /// Finds the length of the null terminated UTF-16 name that starts at the name offset.
    /// </summary>
    public static int MeasureNameLength(ReadOnlySpan<byte> data)
    {
        int offset = Win32.FindDataNameOffset;
        int limit = Win32.FindDataNameOffset + (Win32.FindDataNameLength * 2);

        while (offset + 2 <= limit)
        {
            if (data[offset] == 0 && data[offset + 1] == 0)
            {
                return offset - Win32.FindDataNameOffset;
            }

            offset += 2;
        }

        return limit - Win32.FindDataNameOffset;
    }

    private static long ReadFileTime(ReadOnlySpan<byte> data, int offset)
    {
        uint low = BinaryPrimitives.ReadUInt32LittleEndian(data[offset..]);
        uint high = BinaryPrimitives.ReadUInt32LittleEndian(data[(offset + 4)..]);

        return new FileTime { Low = low, High = high }.Ticks;
    }
}
