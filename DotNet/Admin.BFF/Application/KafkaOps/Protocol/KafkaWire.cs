using System.Buffers.Binary;
using System.Text;

namespace LantanaGroup.Link.LinkAdmin.BFF.Application.KafkaOps.Protocol;

/// <summary>
/// Big-endian Kafka wire primitives, including the compact (flexible-version) encodings and tagged fields.
/// See https://kafka.apache.org/protocol for the type definitions.
/// </summary>
public sealed class KafkaWireWriter
{
    private readonly MemoryStream _buffer = new();

    public byte[] ToArray() => _buffer.ToArray();

    public KafkaWireWriter Int16(short value)
    {
        Span<byte> span = stackalloc byte[2];
        BinaryPrimitives.WriteInt16BigEndian(span, value);
        _buffer.Write(span);
        return this;
    }

    public KafkaWireWriter Int32(int value)
    {
        Span<byte> span = stackalloc byte[4];
        BinaryPrimitives.WriteInt32BigEndian(span, value);
        _buffer.Write(span);
        return this;
    }

    public KafkaWireWriter UVarint(uint value)
    {
        while ((value & ~0x7Fu) != 0)
        {
            _buffer.WriteByte((byte)((value & 0x7F) | 0x80));
            value >>= 7;
        }

        _buffer.WriteByte((byte)value);
        return this;
    }

    /// <summary>STRING / NULLABLE_STRING: int16 length, -1 for null.</summary>
    public KafkaWireWriter String(string? value)
    {
        if (value is null)
            return Int16(-1);
        var bytes = Encoding.UTF8.GetBytes(value);
        if (bytes.Length > short.MaxValue)
            throw new ArgumentException("The string is too long for the Kafka protocol.", nameof(value));
        Int16((short)bytes.Length);
        _buffer.Write(bytes);
        return this;
    }

    /// <summary>COMPACT_STRING / COMPACT_NULLABLE_STRING: uvarint length + 1, 0 for null.</summary>
    public KafkaWireWriter CompactString(string? value)
    {
        if (value is null)
            return UVarint(0);
        var bytes = Encoding.UTF8.GetBytes(value);
        UVarint((uint)bytes.Length + 1);
        _buffer.Write(bytes);
        return this;
    }

    /// <summary>BYTES: int32 length.</summary>
    public KafkaWireWriter Bytes(byte[] value)
    {
        Int32(value.Length);
        _buffer.Write(value);
        return this;
    }

    /// <summary>Length prefix of a COMPACT_ARRAY; null is written as 0.</summary>
    public KafkaWireWriter CompactArrayLength(int? count) => count is null ? UVarint(0) : UVarint((uint)count.Value + 1);

    public KafkaWireWriter CompactInt32Array(IReadOnlyCollection<int>? values)
    {
        CompactArrayLength(values?.Count);
        if (values is not null)
        {
            foreach (var value in values)
                Int32(value);
        }

        return this;
    }

    /// <summary>An empty tagged-field section.</summary>
    public KafkaWireWriter NoTags() => UVarint(0);
}

public sealed class KafkaWireReader
{
    private readonly byte[] _data;
    private int _position;

    public KafkaWireReader(byte[] data) => _data = data;

    public int Remaining => _data.Length - _position;

    private ReadOnlySpan<byte> Take(int count)
    {
        if (count < 0 || count > Remaining)
            throw new KafkaWireException("The broker response ended early.");
        var span = new ReadOnlySpan<byte>(_data, _position, count);
        _position += count;
        return span;
    }

    public short Int16() => BinaryPrimitives.ReadInt16BigEndian(Take(2));

    public int Int32() => BinaryPrimitives.ReadInt32BigEndian(Take(4));

    public long Int64() => BinaryPrimitives.ReadInt64BigEndian(Take(8));

    public uint UVarint()
    {
        uint value = 0;
        for (var shift = 0; shift <= 28; shift += 7)
        {
            var next = Take(1)[0];
            value |= (uint)(next & 0x7F) << shift;
            if ((next & 0x80) == 0)
                return value;
        }

        throw new KafkaWireException("The broker response has an invalid varint.");
    }

    public string? String()
    {
        var length = Int16();
        return length < 0 ? null : Encoding.UTF8.GetString(Take(length));
    }

    public string? CompactString()
    {
        var length = (int)UVarint() - 1;
        return length < 0 ? null : Encoding.UTF8.GetString(Take(length));
    }

    public byte[]? Bytes()
    {
        var length = Int32();
        return length < 0 ? null : Take(length).ToArray();
    }

    /// <summary>Element count of a COMPACT_ARRAY, or -1 for null.</summary>
    public int CompactArrayLength()
    {
        var length = (int)UVarint() - 1;
        if (length > Remaining)
            throw new KafkaWireException("The broker response has an invalid array length.");
        return length;
    }

    /// <summary>Element count of an int32-prefixed ARRAY, or -1 for null.</summary>
    public int ArrayLength()
    {
        var length = Int32();
        if (length > Remaining)
            throw new KafkaWireException("The broker response has an invalid array length.");
        return length;
    }

    public List<int> CompactInt32Array()
    {
        var count = CompactArrayLength();
        var values = new List<int>(Math.Max(0, count));
        for (var index = 0; index < count; index++)
            values.Add(Int32());
        return values;
    }

    public void SkipTags()
    {
        var count = UVarint();
        for (var index = 0; index < count; index++)
        {
            UVarint();
            Take((int)UVarint());
        }
    }
}

public sealed class KafkaWireException : Exception
{
    public KafkaWireException(string message) : base(message) { }

    public KafkaWireException(string message, Exception inner) : base(message, inner) { }
}
