using Automation.UI.Models;
using Automation.UI.Services.Persistence;

namespace Automation.UI.Services;

/// <summary>
/// Seeds system default normalization operations, sequences, and a default suite on startup.
/// Mirrors the pattern used by <see cref="QueryPlanTemplateSeedService"/>.
/// </summary>
public sealed class NormalizationSuiteSeedService : IHostedService
{
    // Well-known IDs for system defaults.
    private static readonly Guid OpCopyLocationId = new("00000000-0000-0000-2000-000000000001");
    private static readonly Guid OpRemoveExtensionsId = new("00000000-0000-0000-2000-000000000002");
    private static readonly Guid OpCopyIdentifierToTypeId = new("00000000-0000-0000-2000-000000000003");
    private static readonly Guid SeqDefaultLocationId = new("00000000-0000-0000-2000-000000000010");
    private static readonly Guid SeqDefaultCleanupId = new("00000000-0000-0000-2000-000000000011");
    private static readonly Guid SuiteSystemDefaultId = new("00000000-0000-0000-2000-000000000100");

    private readonly INormalizationStore _store;
    private readonly ILogger<NormalizationSuiteSeedService> _logger;

    public NormalizationSuiteSeedService(INormalizationStore store, ILogger<NormalizationSuiteSeedService> logger)
    {
        _store = store;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        // --- Operations ---
        var copyLocation = new NormalizationOperationDefinition
        {
            Id = OpCopyLocationId,
            Name = "Copy Location Identifiers to Type",
            Description = "Copies each Location Identifier's System and Value fields into Location.Type as a CodeableConcept.",
            OperationType = "CopyLocation",
            ResourceTypes = ["Location"],
            IsSystem = true,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        var removeExtensions = new NormalizationOperationDefinition
        {
            Id = OpRemoveExtensionsId,
            Name = "Remove Common Extensions",
            Description = "Strips known non-essential extensions from all resources to reduce noise in downstream processing.",
            OperationType = "RemoveExtensions",
            ResourceTypes = ["Encounter", "Patient", "Condition", "MedicationRequest", "Observation"],
            ExtensionUrls =
            [
                "http://hl7.org/fhir/StructureDefinition/data-absent-reason"
            ],
            IsSystem = true,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        var copyIdentifierToType = new NormalizationOperationDefinition
        {
            Id = OpCopyIdentifierToTypeId,
            Name = "Copy Location Identifier Value to Type Code",
            Description = "Copies Location identifier.value to type[0].coding.code via CopyProperty.",
            OperationType = "CopyProperty",
            ResourceTypes = ["Location"],
            SourceFhirPath = "identifier.value",
            TargetFhirPath = "type[0].coding.code",
            IsSystem = true,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        await _store.UpsertOperationAsync(copyLocation, cancellationToken);
        await _store.UpsertOperationAsync(removeExtensions, cancellationToken);
        await _store.UpsertOperationAsync(copyIdentifierToType, cancellationToken);

        _logger.LogDebug("Seeded/refreshed system normalization operations.");

        // --- Sequences ---
        var locationSequence = new NormalizationSequenceDefinition
        {
            Id = SeqDefaultLocationId,
            Name = "Default Location Normalization",
            Description = "Applies CopyLocation and CopyProperty operations to Location resources.",
            Entries =
            [
                new NormalizationSequenceEntry { OperationId = OpCopyLocationId, Sequence = 1 },
                new NormalizationSequenceEntry { OperationId = OpCopyIdentifierToTypeId, Sequence = 2 }
            ],
            IsSystem = true,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        var cleanupSequence = new NormalizationSequenceDefinition
        {
            Id = SeqDefaultCleanupId,
            Name = "Default Cleanup",
            Description = "Removes non-essential extensions from clinical resources.",
            Entries =
            [
                new NormalizationSequenceEntry { OperationId = OpRemoveExtensionsId, Sequence = 1 }
            ],
            IsSystem = true,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        await _store.UpsertSequenceAsync(locationSequence, cancellationToken);
        await _store.UpsertSequenceAsync(cleanupSequence, cancellationToken);

        _logger.LogDebug("Seeded/refreshed system normalization sequences.");

        // --- Suite ---
        var defaultSuite = new NormalizationSuiteDefinition
        {
            Id = SuiteSystemDefaultId,
            Name = "System Default",
            Description = "Built-in normalization suite that applies location normalization and extension cleanup.",
            OperationIds = [],
            SequenceIds = [SeqDefaultLocationId, SeqDefaultCleanupId],
            IsSystem = true,
            IsDefault = true,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        await _store.UpsertSuiteAsync(defaultSuite, cancellationToken);
        _logger.LogInformation("Seeded/refreshed system default normalization suite: {Id}", SuiteSystemDefaultId);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
