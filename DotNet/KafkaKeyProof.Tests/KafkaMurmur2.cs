using System.Text;

namespace LantanaGroup.Link.KafkaKeyProof.Tests;

/// <summary>
/// Kafka's Utils.murmur2, the partition hash used by the Java client and by librdkafka Murmur2Random.
/// </summary>
public static class KafkaMurmur2
{
    public static int Partition(string key, int partitionCount)
    {
        if (partitionCount < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(partitionCount));
        }

        var hash = Murmur2(Encoding.UTF8.GetBytes(key));
        return ToPositive(hash) % partitionCount;
    }

    public static int ToPositive(int number) => number & 0x7fffffff;

    public static int Murmur2(byte[] data)
    {
        const int seed = unchecked((int)0x9747b28c);
        const int m = 0x5bd1e995;
        const int r = 24;
        var length = data.Length;
        var h = seed ^ length;
        var length4 = length / 4;

        for (var i = 0; i < length4; i++)
        {
            var i4 = i * 4;
            var k = (data[i4] & 0xff)
                | ((data[i4 + 1] & 0xff) << 8)
                | ((data[i4 + 2] & 0xff) << 16)
                | ((data[i4 + 3] & 0xff) << 24);
            k *= m;
            k ^= (int)((uint)k >> r);
            k *= m;
            h *= m;
            h ^= k;
        }

        var remainder = length & ~3;
        switch (length % 4)
        {
            case 3:
                h ^= (data[remainder + 2] & 0xff) << 16;
                goto case 2;
            case 2:
                h ^= (data[remainder + 1] & 0xff) << 8;
                goto case 1;
            case 1:
                h ^= data[remainder] & 0xff;
                h *= m;
                break;
        }

        h ^= (int)((uint)h >> 13);
        h *= m;
        h ^= (int)((uint)h >> 15);
        return h;
    }
}
