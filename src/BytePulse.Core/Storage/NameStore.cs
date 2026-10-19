using System.Buffers.Binary;
using System.Text;

namespace BytePulse.Core.Storage;

/// <summary>How the bytes of a stored name are encoded.</summary>
internal enum NameEncoding : byte
{
    /// <summary>UTF-16LE, two bytes per character.</summary>
    Utf16 = 0,

    /// <summary>One byte per character, each the low byte of a UTF-16 code unit.</summary>
    Latin1 = 1,
}

/// <summary>
/// An append-only blob that stores file names without allocating a string per name.
/// </summary>
/// <remarks>
/// <para>
/// Each record is laid out as <c>[ushort charCount][byte encoding][payload]</c>. Names whose
/// every code unit fits in one byte use the Latin-1 encoding, which halves the storage for
/// the overwhelmingly common case of ASCII names. The payload is copied verbatim from the
/// bytes the file system returned, so a name is reproduced exactly.
/// </para>
/// <para>
/// The store can be frozen once a scan has finished, after which reads no longer take the
/// write lock. That matters because sorting compares millions of names.
/// </para>
/// </remarks>
internal sealed class NameStore
{
    private const int HeaderSize = 3;

    /// <summary>Names longer than this are compared through decoded strings instead of on the stack.</summary>
    private const int StackNameLimit = 256;

    private byte[] _buffer;
    private int _used;
    private readonly object _writeLock = new();
    private volatile bool _frozen;

    internal NameStore(int initialCapacity = 1 << 16)
    {
        _buffer = new byte[Math.Max(initialCapacity, HeaderSize)];
    }

    /// <summary>The number of bytes currently occupied by stored names.</summary>
    internal int UsedBytes
    {
        get
        {
            lock (_writeLock)
            {
                return _used;
            }
        }
    }

    /// <summary>
    /// Stops the store accepting new names and removes the lock from the read path.
    /// </summary>
    internal void Freeze()
    {
        lock (_writeLock)
        {
            _frozen = true;
        }
    }

    /// <summary>Appends a UTF-16LE name and returns the offset it was written at.</summary>
    internal int Add(ReadOnlySpan<byte> utf16)
    {
        if (utf16.Length % 2 != 0)
        {
            throw new ArgumentException("Name bytes must be a whole number of UTF-16 code units.", nameof(utf16));
        }

        int charCount = utf16.Length / 2;
        bool latin1 = IsLatin1(utf16);
        int payloadLength = latin1 ? charCount : utf16.Length;
        int recordLength = HeaderSize + payloadLength;

        lock (_writeLock)
        {
            ObjectDisposedException.ThrowIf(_frozen, this);

            if (_used + recordLength > _buffer.Length)
            {
                int capacity = _buffer.Length;
                while (capacity < _used + recordLength)
                {
                    capacity *= 2;
                }

                Array.Resize(ref _buffer, capacity);
            }

            int offset = _used;
            Span<byte> destination = _buffer.AsSpan(offset, recordLength);

            BinaryPrimitives.WriteUInt16LittleEndian(destination, (ushort)charCount);
            destination[2] = latin1 ? (byte)NameEncoding.Latin1 : (byte)NameEncoding.Utf16;

            if (latin1)
            {
                for (int i = 0; i < charCount; i++)
                {
                    destination[HeaderSize + i] = utf16[i * 2];
                }
            }
            else
            {
                utf16.CopyTo(destination[HeaderSize..]);
            }

            _used += recordLength;
            return offset;
        }
    }

    /// <summary>Reconstructs the name stored at <paramref name="offset"/>.</summary>
    internal string GetString(int offset)
    {
        if (_frozen)
        {
            return ReadString(offset);
        }

        lock (_writeLock)
        {
            return ReadString(offset);
        }
    }

    /// <summary>The number of characters in the name stored at <paramref name="offset"/>.</summary>
    internal int GetCharCount(int offset)
    {
        if (_frozen)
        {
            return BinaryPrimitives.ReadUInt16LittleEndian(_buffer.AsSpan(offset, 2));
        }

        lock (_writeLock)
        {
            return BinaryPrimitives.ReadUInt16LittleEndian(_buffer.AsSpan(offset, 2));
        }
    }

    /// <summary>
    /// Compares two stored names for ordering, without allocating.
    /// </summary>
    /// <remarks>
    /// The names are compared ignoring case, using the invariant simple uppercase mapping
    /// applied per character. When two names differ only in case they are then compared
    /// case-sensitively, so the result is never zero for two different names and the
    /// comparison can be used directly as a sort key.
    /// </remarks>
    internal int CompareNames(int offsetA, int offsetB)
    {
        if (_frozen)
        {
            return CompareNamesCore(offsetA, offsetB);
        }

        lock (_writeLock)
        {
            return CompareNamesCore(offsetA, offsetB);
        }
    }

    private int CompareNamesCore(int offsetA, int offsetB)
    {
        int countA = BinaryPrimitives.ReadUInt16LittleEndian(_buffer.AsSpan(offsetA, 2));
        int countB = BinaryPrimitives.ReadUInt16LittleEndian(_buffer.AsSpan(offsetB, 2));

        if (countA > StackNameLimit || countB > StackNameLimit)
        {
            return string.Compare(ReadString(offsetA), ReadString(offsetB), StringComparison.OrdinalIgnoreCase);
        }

        Span<char> bufferA = stackalloc char[StackNameLimit];
        Span<char> bufferB = stackalloc char[StackNameLimit];

        DecodeChars(offsetA, bufferA[..countA]);
        DecodeChars(offsetB, bufferB[..countB]);

        int shared = Math.Min(countA, countB);
        for (int i = 0; i < shared; i++)
        {
            char a = char.ToUpperInvariant(bufferA[i]);
            char b = char.ToUpperInvariant(bufferB[i]);
            if (a != b)
            {
                return a < b ? -1 : 1;
            }
        }

        if (countA != countB)
        {
            return countA < countB ? -1 : 1;
        }

        // Equal ignoring case. Fall back to a case-sensitive comparison so the order is total.
        for (int i = 0; i < shared; i++)
        {
            if (bufferA[i] != bufferB[i])
            {
                return bufferA[i] < bufferB[i] ? -1 : 1;
            }
        }

        return 0;
    }

    private void DecodeChars(int offset, Span<char> destination)
    {
        var encoding = (NameEncoding)_buffer[offset + 2];
        if (encoding == NameEncoding.Latin1)
        {
            for (int i = 0; i < destination.Length; i++)
            {
                destination[i] = (char)_buffer[offset + HeaderSize + i];
            }

            return;
        }

        for (int i = 0; i < destination.Length; i++)
        {
            destination[i] = (char)BinaryPrimitives.ReadUInt16LittleEndian(
                _buffer.AsSpan(offset + HeaderSize + (i * 2), 2));
        }
    }

    private string ReadString(int offset)
    {
        int charCount = BinaryPrimitives.ReadUInt16LittleEndian(_buffer.AsSpan(offset, 2));
        var encoding = (NameEncoding)_buffer[offset + 2];

        return encoding == NameEncoding.Latin1
            ? string.Create(charCount, _buffer.AsSpan(offset + HeaderSize, charCount), static (destination, source) =>
            {
                for (int i = 0; i < destination.Length; i++)
                {
                    destination[i] = (char)source[i];
                }
            })
            : Encoding.Unicode.GetString(_buffer, offset + HeaderSize, charCount * 2);
    }

    private static bool IsLatin1(ReadOnlySpan<byte> utf16)
    {
        for (int i = 0; i < utf16.Length; i += 2)
        {
            if (utf16[i + 1] != 0)
            {
                return false;
            }
        }

        return true;
    }
}
