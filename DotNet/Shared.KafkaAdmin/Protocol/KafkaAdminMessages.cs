namespace LantanaGroup.Link.Shared.KafkaAdmin.Protocol;

/// <summary>
/// Encoders and decoders for the few Kafka admin requests Confluent.Kafka does not expose:
/// AlterPartitionReassignments (key 45) and ListPartitionReassignments (key 46) from KIP-455,
/// plus the ApiVersions, Metadata and SASL requests needed to reach them. Pure functions over bytes, so they are unit tested
/// against hand-built frames.
/// </summary>
public static class KafkaAdminMessages
{
    public const short ApiVersionsKey = 18;
    public const short MetadataKey = 3;
    public const short SaslHandshakeKey = 17;
    public const short SaslAuthenticateKey = 36;
    public const short AlterPartitionReassignmentsKey = 45;
    public const short ListPartitionReassignmentsKey = 46;

    public const short ErrorNone = 0;
    public const short ErrorUnknownTopicOrPartition = 3;
    public const short ErrorRequestTimedOut = 7;
    public const short ErrorClusterAuthorizationFailed = 31;
    public const short ErrorUnsupportedSaslMechanism = 33;
    public const short ErrorUnsupportedVersion = 35;
    public const short ErrorInvalidReplicaAssignment = 39;
    public const short ErrorNotController = 41;
    public const short ErrorSaslAuthenticationFailed = 58;
    public const short ErrorNoReassignmentInProgress = 85;

    /// <summary>Request header v1 (non-flexible) or v2 (flexible: adds an empty tagged-field section).</summary>
    public static byte[] Frame(short apiKey, short apiVersion, int correlationId, string clientId, bool flexible, byte[] body)
    {
        var header = new KafkaWireWriter().Int16(apiKey).Int16(apiVersion).Int32(correlationId).String(clientId);
        if (flexible)
            header.NoTags();
        var head = header.ToArray();
        var frame = new KafkaWireWriter().Int32(head.Length + body.Length).ToArray();
        return [.. frame, .. head, .. body];
    }

    /// <summary>Reads the response header and returns a reader positioned at the body.</summary>
    public static KafkaWireReader OpenResponse(byte[] payload, int correlationId, bool flexibleHeader)
    {
        var reader = new KafkaWireReader(payload);
        var correlation = reader.Int32();
        if (correlation != correlationId)
            throw new KafkaWireException("The broker answered a different request (correlation " + correlation + ", expected " + correlationId + ").");
        if (flexibleHeader)
            reader.SkipTags();
        return reader;
    }

    public static byte[] ApiVersionsRequest() => [];

    public static Dictionary<short, (short Min, short Max)> ReadApiVersions(KafkaWireReader reader)
    {
        var error = reader.Int16();
        if (error != ErrorNone && error != ErrorUnsupportedVersion)
            throw new KafkaAdminProtocolException(error, "ApiVersions failed.");
        var count = reader.ArrayLength();
        var versions = new Dictionary<short, (short, short)>();
        for (var index = 0; index < count; index++)
            versions[reader.Int16()] = (reader.Int16(), reader.Int16());
        return versions;
    }

    /// <summary>Metadata v1 with an empty topic list: brokers and the controller id only.</summary>
    public static byte[] MetadataRequest() => new KafkaWireWriter().Int32(0).ToArray();

    public static (List<(int Id, string Host, int Port)> Brokers, int ControllerId) ReadMetadata(KafkaWireReader reader)
    {
        var count = reader.ArrayLength();
        var brokers = new List<(int, string, int)>();
        for (var index = 0; index < count; index++)
        {
            var id = reader.Int32();
            var host = reader.String() ?? "";
            var port = reader.Int32();
            reader.String();
            brokers.Add((id, host, port));
        }

        return (brokers, reader.Int32());
    }

    public static byte[] SaslHandshakeRequest(string mechanism) => new KafkaWireWriter().String(mechanism).ToArray();

    public static void ReadSaslHandshake(KafkaWireReader reader, string mechanism)
    {
        var error = reader.Int16();
        var count = reader.ArrayLength();
        var offered = new List<string>();
        for (var index = 0; index < count; index++)
            offered.Add(reader.String() ?? "");
        if (error != ErrorNone)
            throw new KafkaAdminProtocolException(error, "The broker does not accept SASL mechanism " + mechanism + ". It offers " + (offered.Count == 0 ? "none" : string.Join(", ", offered)) + ".");
    }

    public static byte[] SaslAuthenticateRequest(byte[] token) => new KafkaWireWriter().Bytes(token).ToArray();

    /// <summary>SaslAuthenticate v1 response.</summary>
    public static byte[] ReadSaslAuthenticate(KafkaWireReader reader)
    {
        var error = reader.Int16();
        var message = reader.String();
        var bytes = reader.Bytes() ?? [];
        reader.Int64();
        if (error != ErrorNone)
            throw new KafkaAdminProtocolException(error, "Kafka sign-in failed. " + (message ?? ""));
        return bytes;
    }

    /// <summary>AlterPartitionReassignments v0. A null replica list cancels the partition's reassignment.</summary>
    public static byte[] AlterPartitionReassignmentsRequest(IReadOnlyList<PartitionTarget> targets, int timeoutMs)
    {
        var writer = new KafkaWireWriter().Int32(timeoutMs);
        var topics = targets.GroupBy(target => target.Topic, StringComparer.Ordinal).ToList();
        writer.CompactArrayLength(topics.Count);
        foreach (var topic in topics)
        {
            writer.CompactString(topic.Key);
            var partitions = topic.ToList();
            writer.CompactArrayLength(partitions.Count);
            foreach (var partition in partitions)
            {
                writer.Int32(partition.Partition);
                writer.CompactInt32Array(partition.Replicas);
                writer.NoTags();
            }

            writer.NoTags();
        }

        return writer.NoTags().ToArray();
    }

    public static ReassignmentOutcome ReadAlterPartitionReassignments(KafkaWireReader reader)
    {
        reader.Int32();
        var outcome = new ReassignmentOutcome { ErrorCode = reader.Int16(), ErrorMessage = reader.CompactString() ?? "" };
        var topics = reader.CompactArrayLength();
        for (var topicIndex = 0; topicIndex < topics; topicIndex++)
        {
            var name = reader.CompactString() ?? "";
            var partitions = reader.CompactArrayLength();
            for (var index = 0; index < partitions; index++)
            {
                outcome.Partitions.Add(new PartitionOutcome
                {
                    Topic = name,
                    Partition = reader.Int32(),
                    ErrorCode = reader.Int16(),
                    ErrorMessage = reader.CompactString() ?? ""
                });
                reader.SkipTags();
            }

            reader.SkipTags();
        }

        reader.SkipTags();
        return outcome;
    }

    /// <summary>ListPartitionReassignments v0 for every partition (null topic list).</summary>
    public static byte[] ListPartitionReassignmentsRequest(int timeoutMs) =>
        new KafkaWireWriter().Int32(timeoutMs).CompactArrayLength(null).NoTags().ToArray();

    public static List<OngoingReassignment> ReadListPartitionReassignments(KafkaWireReader reader)
    {
        reader.Int32();
        var error = reader.Int16();
        var message = reader.CompactString();
        if (error != ErrorNone)
            throw new KafkaAdminProtocolException(error, "Kafka did not list the partition reassignments. " + (message ?? ""));
        var ongoing = new List<OngoingReassignment>();
        var topics = reader.CompactArrayLength();
        for (var topicIndex = 0; topicIndex < topics; topicIndex++)
        {
            var name = reader.CompactString() ?? "";
            var partitions = reader.CompactArrayLength();
            for (var index = 0; index < partitions; index++)
            {
                ongoing.Add(new OngoingReassignment
                {
                    Topic = name,
                    Partition = reader.Int32(),
                    Replicas = reader.CompactInt32Array(),
                    Adding = reader.CompactInt32Array(),
                    Removing = reader.CompactInt32Array()
                });
                reader.SkipTags();
            }

            reader.SkipTags();
        }

        reader.SkipTags();
        return ongoing;
    }

    public static string ErrorName(short code) => code switch
    {
        ErrorUnknownTopicOrPartition => "UNKNOWN_TOPIC_OR_PARTITION",
        ErrorRequestTimedOut => "REQUEST_TIMED_OUT",
        ErrorClusterAuthorizationFailed => "CLUSTER_AUTHORIZATION_FAILED",
        ErrorUnsupportedSaslMechanism => "UNSUPPORTED_SASL_MECHANISM",
        ErrorUnsupportedVersion => "UNSUPPORTED_VERSION",
        ErrorInvalidReplicaAssignment => "INVALID_REPLICA_ASSIGNMENT",
        ErrorNotController => "NOT_CONTROLLER",
        ErrorSaslAuthenticationFailed => "SASL_AUTHENTICATION_FAILED",
        ErrorNoReassignmentInProgress => "NO_REASSIGNMENT_IN_PROGRESS",
        _ => "error " + code
    };
}

public sealed record PartitionTarget(string Topic, int Partition, IReadOnlyList<int>? Replicas);

public sealed class PartitionOutcome
{
    public string Topic { get; set; } = "";
    public int Partition { get; set; }
    public short ErrorCode { get; set; }
    public string ErrorMessage { get; set; } = "";
}

public sealed class ReassignmentOutcome
{
    public short ErrorCode { get; set; }
    public string ErrorMessage { get; set; } = "";
    public List<PartitionOutcome> Partitions { get; } = [];
}

public sealed class OngoingReassignment
{
    public string Topic { get; set; } = "";
    public int Partition { get; set; }
    public List<int> Replicas { get; set; } = [];
    public List<int> Adding { get; set; } = [];
    public List<int> Removing { get; set; } = [];
}

public sealed class KafkaAdminProtocolException : Exception
{
    public KafkaAdminProtocolException(short code, string message)
        : base(message.Trim() + " (" + KafkaAdminMessages.ErrorName(code) + ")") => Code = code;

    public short Code { get; }
}
