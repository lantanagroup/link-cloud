using System.Text.Json.Serialization;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Models.PatientsOfInterest;

namespace LantanaGroup.Link.Nhsn.App.Bff.Infrastructure.Link.Mappers;

// Data Acquisition's sFTP configuration as it appears on the wire, and its mapping to/from
// PatientsOfInterest.SftpConfig.
//
// Declared locally because Create/Get/UpdateSftpConfigurationAsync all return or take the
// non-generic LinkApiResponse/object — no shared DTO exists in LinkSdk for this shape.
internal sealed record SftpConfigurationWire
{
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    [JsonPropertyName("host")]
    public string? Host { get; init; }

    [JsonPropertyName("port")]
    public int Port { get; init; }

    [JsonPropertyName("remoteDirectory")]
    public string? RemoteDirectory { get; init; }

    [JsonPropertyName("removeAfterProcessing")]
    public bool RemoveAfterProcessing { get; init; }
}

// Outbound shape for POST {organizationId}/sftp-configurations and
// PUT {organizationId}/sftp-configurations/{id} — both bind SftpConfigurationModel server-side, so
// one payload type covers create and update.
internal sealed class SftpConfigurationPayload
{
    // Omitted on create (Id null): SftpConfigurationModel.Id is a non-nullable Guid server-side —
    // sending "id": null fails JSON binding outright (400) before validation even runs.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Id { get; init; }
    public required string Host { get; init; }
    public required int Port { get; init; }
    public required string RemoteDirectory { get; init; }
    public required TimeSpan Timeout { get; init; }
    public bool RemoveAfterProcessing { get; init; }
    public string AuthenticationProtocol { get; init; } = "Basic";

    // A facility only ever has one acquisition type through this screen. Left null/default on the
    // nested entry itself: RemoteDirectory inherits the connection-level directory, and
    // FileNamePattern/ParsingConfiguration fall back to Data Acquisition's default parser for
    // Census + CernerCCLExtract.
    public List<SftpAcquisitionTypeConfigurationPayload> AcquisitionConfigurations { get; init; } =
    [
        new SftpAcquisitionTypeConfigurationPayload()
    ];
}

internal sealed class SftpAcquisitionTypeConfigurationPayload
{
    public string AcquisitionType { get; init; } = "Census";
    public string SubType { get; init; } = "CernerCCLExtract";
}

internal sealed class SftpCredentialsPayload
{
    public required string Username { get; init; }
    public required string Password { get; init; }
}

internal sealed record SftpCredentialStatusWire
{
    [JsonPropertyName("hasCredentials")]
    public bool HasCredentials { get; init; }
}

internal static class SftpConfigurationMapper
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);

    public static SftpConfig? ToDomain(SftpConfigurationWire? source) =>
        source is null
            ? null
            : new SftpConfig
            {
                Host = source.Host ?? string.Empty,
                Port = source.Port,
                RemoteDirectory = source.RemoteDirectory ?? "/",
                RemoveAfterProcessing = source.RemoveAfterProcessing
                // Username/Password are never round-tripped back out — Data Acquisition's own
                // credentials endpoints are write-only for the same reason.
            };

    public static SftpConfigurationPayload ToPayload(SftpConfig config, string? existingId) => new()
    {
        Id = existingId,
        Host = config.Host,
        Port = config.Port,
        RemoteDirectory = string.IsNullOrWhiteSpace(config.RemoteDirectory) ? "/" : config.RemoteDirectory,
        Timeout = DefaultTimeout,
        RemoveAfterProcessing = config.RemoveAfterProcessing
    };
}
