using System.Text.Json.Serialization;

namespace Automation.UI.Services.Persistence;

public interface ISnapshotPayloadStore
{
    bool ShouldExternalize(string domain, int payloadUtf8Bytes);
    Task<SnapshotPayloadPointer> StoreAsync(Guid runId, string domain, string payloadJson, CancellationToken ct = default);
    Task<string?> ReadAsync(SnapshotPayloadPointer pointer, CancellationToken ct = default);
    Task DeleteIfExistsAsync(SnapshotPayloadPointer pointer, CancellationToken ct = default);
    Task DeleteRunPayloadsAsync(Guid runId, CancellationToken ct = default);
}

public sealed class SnapshotPayloadPointer
{
    public const string KindValue = "abs";

    [JsonPropertyName("kind")]
    public string Kind { get; init; } = KindValue;

    [JsonPropertyName("blob")]
    public string BlobName { get; init; } = string.Empty;

    [JsonPropertyName("bytes")]
    public int Utf8Bytes { get; init; }

    [JsonPropertyName("etag")]
    public string? ETag { get; init; }
}
