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
    private static readonly Guid OpRemoveEncounterEpicExtensionsId = new("00000000-0000-0000-2000-000000000004");
    private static readonly Guid OpRemoveMeasureReportExtensionsId = new("00000000-0000-0000-2000-000000000005");
    private static readonly Guid OpRemoveObservationDatetimeExtensionId = new("00000000-0000-0000-2000-000000000006");
    private static readonly Guid OpRemovePatientMergeInstantExtensionId = new("00000000-0000-0000-2000-000000000007");
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

        var removeEncounterEpicExtensions = new NormalizationOperationDefinition
        {
            Id = OpRemoveEncounterEpicExtensionsId,
            Name = "Remove Encounter Epic Extensions",
            Description = "Removes Epic-specific Encounter extensions for accident-related context and Epic internal identifiers.",
            OperationType = "RemoveExtensions",
            ResourceTypes = ["Encounter"],
            ExtensionUrls =
            [
                "http://open.epic.com/FHIR/StructureDefinition/extension/accidentrelated",
                "http://open.epic.com/FHIR/StructureDefinition/extension/epic-id"
            ],
            IsSystem = true,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        var removeMeasureReportExtensions = new NormalizationOperationDefinition
        {
            Id = OpRemoveMeasureReportExtensionsId,
            Name = "Remove MeasureReport Supplemental Extensions",
            Description = "Removes MeasureReport population description, supplemental data element reference, and DEQM criteria reference extensions.",
            OperationType = "RemoveExtensions",
            ResourceTypes = ["MeasureReport"],
            ExtensionUrls =
            [
                "http://hl7.org/fhir/5.0/StructureDefinition/extension-MeasureReport.population.description",
                "http://hl7.org/fhir/5.0/StructureDefinition/extension-MeasureReport.supplementalDataElement.reference",
                "http://hl7.org/fhir/us/davinci-deqm/StructureDefinition/extension-criteriaReference"
            ],
            IsSystem = true,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        var removeObservationDatetimeExtension = new NormalizationOperationDefinition
        {
            Id = OpRemoveObservationDatetimeExtensionId,
            Name = "Remove Observation Datetime Extension",
            Description = "Removes the Epic Observation datetime extension when present on Observation resources.",
            OperationType = "RemoveExtensions",
            ResourceTypes = ["Observation"],
            ExtensionUrls =
            [
                "http://open.epic.com/FHIR/StructureDefinition/extension/observation-datetime"
            ],
            IsSystem = true,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        var removePatientMergeInstantExtension = new NormalizationOperationDefinition
        {
            Id = OpRemovePatientMergeInstantExtensionId,
            Name = "Remove Patient Merge/Unmerge Timestamp Extension",
            Description = "Removes the Epic patient merge/unmerge timestamp extension from Patient resources.",
            OperationType = "RemoveExtensions",
            ResourceTypes = ["Patient"],
            ExtensionUrls =
            [
                "https://open.epic.com/FHIR/StructureDefinition/extension/patient-merge-unmerge-instant"
            ],
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
            Description = "Copies the first Location identifier.value to type[0].coding.code via CopyProperty.",
            OperationType = "CopyProperty",
            ResourceTypes = ["Location"],
            SourceFhirPath = "identifier[0].value",
            TargetFhirPath = "type[0].coding.code",
            IsSystem = true,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        await _store.UpsertOperationAsync(copyLocation, cancellationToken);
        await _store.UpsertOperationAsync(removeExtensions, cancellationToken);
        await _store.UpsertOperationAsync(copyIdentifierToType, cancellationToken);
        await _store.UpsertOperationAsync(removeEncounterEpicExtensions, cancellationToken);
        await _store.UpsertOperationAsync(removeMeasureReportExtensions, cancellationToken);
        await _store.UpsertOperationAsync(removeObservationDatetimeExtension, cancellationToken);
        await _store.UpsertOperationAsync(removePatientMergeInstantExtension, cancellationToken);

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
            Description = "Removes common and vendor-specific non-essential extensions from acquired clinical resources.",
            Entries =
            [
                new NormalizationSequenceEntry { OperationId = OpRemoveExtensionsId, Sequence = 1 },
                new NormalizationSequenceEntry { OperationId = OpRemoveEncounterEpicExtensionsId, Sequence = 2 },
                new NormalizationSequenceEntry { OperationId = OpRemoveObservationDatetimeExtensionId, Sequence = 3 },
                new NormalizationSequenceEntry { OperationId = OpRemovePatientMergeInstantExtensionId, Sequence = 4 },
                new NormalizationSequenceEntry { OperationId = OpRemoveMeasureReportExtensionsId, Sequence = 5 }
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
            OperationIds = [OpRemoveMeasureReportExtensionsId],
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
