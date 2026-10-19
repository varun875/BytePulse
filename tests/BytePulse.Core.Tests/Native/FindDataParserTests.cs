using System.Buffers.Binary;
using System.Text;
using BytePulse.Core.Native;
using Xunit;

namespace BytePulse.Core.Tests.Native;

public sealed class FindDataParserTests
{
    /// <summary>Builds a find-data buffer with the given values.</summary>
    private static byte[] BuildFindData(
        string name,
        long size,
        uint attributes,
        long fileTime = 0)
    {
        var buffer = new byte[Win32.FindDataSize];

        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(0), attributes);

        // A FILETIME of 0x01D0000000000000 is 2020-01-01 in UTC.
        long ticks = fileTime;
        uint low = (uint)(ticks & 0xFFFFFFFF);
        uint high = (uint)(ticks >> 32);

        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(4), low);
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(8), high);
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(12), low);
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(16), high);
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(20), low);
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(24), high);

        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(28), (uint)(size >> 32));
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(32), (uint)(size & 0xFFFFFFFF));

        Encoding.Unicode.GetBytes(name, 0, name.Length, buffer, Win32.FindDataNameOffset);

        return buffer;
    }

    [Fact]
    public void ReadsTheSizeAttributesAndTimes()
    {
        byte[] buffer = BuildFindData("report.pdf", 123_456_789, Win32.FILE_ATTRIBUTE_ARCHIVE);

        FindDataEntry entry = FindDataParser.Parse(buffer);

        Assert.Equal(123_456_789, entry.Size);
        Assert.Equal(Win32.FILE_ATTRIBUTE_ARCHIVE, entry.Attributes);
        Assert.Equal(0, entry.CreationTicks);
        Assert.Equal(0, entry.LastAccessTicks);
        Assert.Equal(0, entry.LastWriteTicks);
    }

    [Fact]
    public void ReadsTheNameLength()
    {
        byte[] buffer = BuildFindData("report.pdf", 0, 0);

        FindDataEntry entry = FindDataParser.Parse(buffer);

        Assert.Equal("report.pdf".Length * 2, entry.NameLength);
        Assert.Equal(Win32.FindDataNameOffset, entry.NameOffset);
    }

    [Fact]
    public void ReadsAnEmptyName()
    {
        byte[] buffer = BuildFindData(string.Empty, 0, 0);

        FindDataEntry entry = FindDataParser.Parse(buffer);

        Assert.Equal(0, entry.NameLength);
    }

    [Fact]
    public void ReadsTheNameOutOfTheBuffer()
    {
        byte[] buffer = BuildFindData("report.pdf", 0, 0);

        FindDataEntry entry = FindDataParser.Parse(buffer);

        string name = Encoding.Unicode.GetString(buffer, entry.NameOffset, entry.NameLength);
        Assert.Equal("report.pdf", name);
    }

    [Fact]
    public void ReadsNamesOutsideAscii()
    {
        byte[] buffer = BuildFindData("caf\u00e9-\u4e2d\u6587.txt", 0, 0);

        FindDataEntry entry = FindDataParser.Parse(buffer);

        string name = Encoding.Unicode.GetString(buffer, entry.NameOffset, entry.NameLength);
        Assert.Equal("caf\u00e9-\u4e2d\u6587.txt", name);
    }

    [Fact]
    public void ReadsAFileTimeAsTicks()
    {
        // 2020-01-01T00:00:00Z in .NET ticks.
        const long expected = 637134336000000000;
        const long fileTime = expected - Win32.FileTimeEpochTicks;

        byte[] buffer = BuildFindData("a.txt", 0, 0, fileTime);

        FindDataEntry entry = FindDataParser.Parse(buffer);

        Assert.Equal(expected, entry.LastWriteTicks);
        Assert.Equal(expected, entry.CreationTicks);
        Assert.Equal(expected, entry.LastAccessTicks);
    }

    [Fact]
    public void TreatsAnImpossibleFileTimeAsUnknown()
    {
        var buffer = new byte[Win32.FindDataSize];
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(20), 0xFFFFFFFF);
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.AsSpan(24), 0xFFFFFFFF);

        FindDataEntry entry = FindDataParser.Parse(buffer);

        Assert.Equal(0, entry.LastWriteTicks);
    }

    [Fact]
    public void NeverMeasuresPastTheNameField()
    {
        // A buffer with no terminator at all must not read past the structure.
        var buffer = new byte[Win32.FindDataSize];
        buffer.AsSpan(Win32.FindDataNameOffset).Fill(0x41);

        int length = FindDataParser.MeasureNameLength(buffer);

        Assert.Equal(Win32.FindDataNameLength * 2, length);
    }
}
