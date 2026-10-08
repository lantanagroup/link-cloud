using System.Security.Cryptography;
using System.Text;
using LantanaGroup.Link.Shared.Application.Models.Kafka;

namespace LantanaGroup.Link.LinkAdmin.BFF.Application.KafkaOps;

public sealed class CopiedHeader
{
    public string Name { get; init; } = "";
    public byte[] Value { get; init; } = [];
}

public sealed class CopiedRecord
{
    public byte[]? Key { get; init; }
    public byte[]? Value { get; init; }
    public List<CopiedHeader> Headers { get; init; } = [];
    public long TimestampMs { get; init; }
    public int Partition { get; init; }
    public long Offset { get; init; }
}

public sealed class CopyRequest
{
    public Guid MigrationId { get; init; }
    public string SourceTopic { get; init; } = "";
    public int TargetPartitions { get; init; }
    public bool RequireComputedTargetEqualsPartition { get; init; }
    public List<CopiedRecord> Source { get; init; } = [];
    public List<CopiedRecord> Destination { get; init; } = [];
}

public sealed class CopyOutcome
{
    public bool Ok { get; init; }
    public string Failure { get; init; } = "";
    public List<CopiedRecord> Written { get; init; } = [];
    public Dictionary<int, int> Counts { get; init; } = [];
    public Dictionary<int, string> Digests { get; init; } = [];
    public int Total { get; init; }
    public bool ReachedEnd { get; init; }
    public bool CountsMatch { get; init; }
    public bool DigestsMatch { get; init; }
}

/// <summary>
/// Plans the backup copy from T onto the temp topic. Resume skips records whose
/// x-link-migration identity is already on the destination. The destination is never deleted.
/// A foreign header stops the copy. The recreated topic stays empty; this copier does not
/// write it.
/// </summary>
public static class LogCopier
{
    public static CopyOutcome Copy(CopyRequest request)
    {
        if (request.TargetPartitions < 1)
            return Fail("The target partition count is invalid.");

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var existing in request.Destination.OrderBy(record => record.Partition).ThenBy(record => record.Offset))
        {
            if (!TryReadIdentity(existing, out var id, out var identity) || id != request.MigrationId)
                return Fail("The destination has a record that is not part of this migration. It was not deleted.");
            seen.Add(identity);
        }

        var planned = new List<(CopiedRecord Source, int Target, string Identity, List<CopiedHeader> Headers)>();
        foreach (var source in request.Source.OrderBy(record => record.Partition).ThenBy(record => record.Offset))
        {
            var target = TargetPartition(source, request);
            if (target < 0 || target >= request.TargetPartitions)
                return Fail("A record mapped outside the target partition count.");
            if (request.RequireComputedTargetEqualsPartition && target != source.Partition)
                return Fail($"Record at {source.Partition}:{source.Offset} maps to partition {target}, so a 1:1 copy is refused.");

            var identity = IdentityOf(request, source);
            if (seen.Contains(identity))
                continue;

            var headers = source.Headers
                .Where(header => !string.Equals(header.Name, KafkaTopicCatalog.MigrationHeader, StringComparison.OrdinalIgnoreCase))
                .Select(header => new CopiedHeader { Name = header.Name, Value = header.Value })
                .ToList();
            headers.Add(new CopiedHeader
            {
                Name = KafkaTopicCatalog.MigrationHeader,
                Value = Encoding.UTF8.GetBytes(identity)
            });
            planned.Add((source, target, identity, headers));
            seen.Add(identity);
        }

        var nextOffset = new Dictionary<int, long>();
        foreach (var existing in request.Destination)
        {
            var candidate = existing.Offset + 1;
            if (!nextOffset.TryGetValue(existing.Partition, out var current) || candidate > current)
                nextOffset[existing.Partition] = candidate;
        }

        var written = new List<CopiedRecord>();
        foreach (var item in planned)
        {
            var offset = nextOffset.GetValueOrDefault(item.Target);
            nextOffset[item.Target] = offset + 1;
            written.Add(new CopiedRecord
            {
                Key = item.Source.Key,
                Value = item.Source.Value,
                Headers = item.Headers,
                TimestampMs = item.Source.TimestampMs,
                Partition = item.Target,
                Offset = offset
            });
        }

        var combined = request.Destination.Concat(written).ToList();
        return new CopyOutcome
        {
            Ok = true,
            Written = written,
            Counts = Counts(combined),
            Digests = Digests(combined),
            Total = combined.Count
        };
    }

    public static int TargetPartition(CopiedRecord record, int targetPartitions)
    {
        if (record.Key is null || record.Key.Length == 0)
            return KafkaMurmur.NullKeyPartition(record.Partition, targetPartitions);
        return KafkaMurmur.Partition(record.Key, targetPartitions);
    }

    /// <summary>
    /// Compares the source log with the backup. Counts and the order-sensitive digest
    /// use the source partition and offset, including the identity stamped on the backup.
    /// </summary>
    public static bool Matches(IReadOnlyList<CopiedRecord> source, IReadOnlyList<CopiedRecord> destination)
    {
        if (source.Count != destination.Count)
            return false;
        foreach (var group in source.GroupBy(record => record.Partition))
        {
            var copied = destination
                .Where(record => SourceIdentity(record).Partition == group.Key)
                .OrderBy(record => SourceIdentity(record).Offset)
                .ToList();
            if (copied.Count != group.Count())
                return false;
            if (!string.Equals(Digest(group.OrderBy(record => record.Offset)), Digest(copied), StringComparison.Ordinal))
                return false;
        }

        return true;
    }

    public static string Digest(IEnumerable<CopiedRecord> records)
    {
        var ordered = records.OrderBy(record => record.Offset).ToList();
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var record in ordered)
        {
            var (partition, offset) = SourceIdentity(record);
            hash.AppendData(BitConverter.GetBytes(partition));
            hash.AppendData(BitConverter.GetBytes(offset));
            hash.AppendData(record.Key ?? []);
            hash.AppendData(record.Value ?? []);
            hash.AppendData(BitConverter.GetBytes(record.TimestampMs));
            foreach (var header in record.Headers.Where(item => !string.Equals(item.Name, KafkaTopicCatalog.MigrationHeader, StringComparison.OrdinalIgnoreCase)).OrderBy(item => item.Name, StringComparer.Ordinal))
            {
                hash.AppendData(Encoding.UTF8.GetBytes(header.Name));
                hash.AppendData(header.Value);
            }
        }

        return Convert.ToHexString(hash.GetHashAndReset());
    }

    public static long RetentionFloor(long frozenRetentionMs, long oldestCreateTimeMs, DateTimeOffset now, int retentionHours)
    {
        if (oldestCreateTimeMs <= 0)
            return Math.Max(0, frozenRetentionMs);
        var age = Math.Max(0, now.ToUnixTimeMilliseconds() - oldestCreateTimeMs);
        var needed = age + Math.Max(1, retentionHours) * 3_600_000L;
        return Math.Max(frozenRetentionMs, needed);
    }

    private static int TargetPartition(CopiedRecord record, CopyRequest request)
    {
        if (record.Key is null || record.Key.Length == 0)
        {
            var sourcePartition = record.Partition;
            if (TryReadIdentity(record, out _, out var identity))
            {
                var parts = identity.Split(';');
                if (parts.Length == 4 && int.TryParse(parts[2], out var parsed))
                    sourcePartition = parsed;
            }

            return KafkaMurmur.NullKeyPartition(sourcePartition, request.TargetPartitions);
        }

        return KafkaMurmur.Partition(record.Key, request.TargetPartitions);
    }

    private static string IdentityOf(CopyRequest request, CopiedRecord source)
    {
        if (TryReadIdentity(source, out var id, out var identity) && id == request.MigrationId)
            return identity;
        return request.MigrationId.ToString("N") + ";" + request.SourceTopic + ";" + source.Partition + ";" + source.Offset;
    }

    private static bool TryReadIdentity(CopiedRecord record, out Guid id, out string identity)
    {
        id = Guid.Empty;
        identity = "";
        var header = record.Headers.FirstOrDefault(item => string.Equals(item.Name, KafkaTopicCatalog.MigrationHeader, StringComparison.OrdinalIgnoreCase));
        if (header is null)
            return false;
        identity = Encoding.UTF8.GetString(header.Value);
        var parts = identity.Split(';');
        return parts.Length == 4 && Guid.TryParseExact(parts[0], "N", out id);
    }

    private static (int Partition, long Offset) SourceIdentity(CopiedRecord record)
    {
        if (TryReadIdentity(record, out _, out var identity))
        {
            var parts = identity.Split(';');
            if (parts.Length == 4 && int.TryParse(parts[2], out var partition) && long.TryParse(parts[3], out var offset))
                return (partition, offset);
        }

        return (record.Partition, record.Offset);
    }

    private static Dictionary<int, int> Counts(IEnumerable<CopiedRecord> records)
    {
        return records.GroupBy(record => record.Partition).ToDictionary(group => group.Key, group => group.Count());
    }

    private static Dictionary<int, string> Digests(IEnumerable<CopiedRecord> records)
    {
        return records.GroupBy(record => record.Partition).ToDictionary(group => group.Key, group => Digest(group));
    }

    private static CopyOutcome Fail(string reason) => new() { Failure = reason };
}
