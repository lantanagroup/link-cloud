using Hl7.Fhir.Model;
using LantanaGroup.Automation.Generation;
using LantanaGroup.Automation.Generation.Thetis;
using LantanaGroup.Link.MockFhirServer.Settings;
using Microsoft.Extensions.Options;
using System.Globalization;
using Task = System.Threading.Tasks.Task;

namespace LantanaGroup.Link.MockFhirServer.Services;

/// <summary>
/// Memory-efficient FHIR resource store. Shared infrastructure (Organization, Locations,
/// Devices, Practitioners, Medications) is cached once at startup because those IDs must
/// remain stable for the lifetime of the process. All patient-specific resources are
/// generated on demand per request and immediately discarded — never held in memory —
/// making this viable for large patient populations.
/// </summary>
public class PatientDataStore
{
    private readonly MockFhirServerSettings _settings;
    private readonly FhirBundleGenerator.SharedIds _sharedIds;
    private readonly List<string> _practitionerIds;
    private readonly List<string> _medicationIds;

    // Only shared (non-patient) resources are persisted; these are O(hundreds) in size.
    private readonly Dictionary<string, Resource> _sharedResourceById =
        new(StringComparer.OrdinalIgnoreCase);

    // Known patient IDs tracked for ID-based reads of patient-owned resources.
    private readonly HashSet<string> _knownPatientIds =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly List<string> _preGeneratedPatientIds = [];
    private readonly SemaphoreSlim _lock = new(1, 1);

    // Default config — cached so we can derive per-type filtered configs cheaply.
    private static readonly FhirGenerationConfig _defaultConfig = new();

    private static readonly PatientProfile MockProfile = new(new Dictionary<ProfiledMeasureType, MeasureEligibility>
    {
        [ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation] = MeasureEligibility.Qualifying
    });

    private static readonly IReadOnlyList<ProfiledMeasureType> MockMeasures =
        [ProfiledMeasureType.NhsnAcuteCareHospitalMonthlyInitialPopulation];

    private static IPatientEntryGenerator PatientGenerator => ThetisPatientEntryGenerator.Shared;

    public PatientDataStore(IOptions<MockFhirServerSettings> settings)
    {
        _settings = settings.Value;

        var (ids, sharedEntries, practitionerIds, medicationIds) =
            FhirBundleGenerator.BuildSharedResources();

        _sharedIds = ids;
        _practitionerIds = practitionerIds;
        _medicationIds = medicationIds;

        foreach (var entry in sharedEntries)
        {
            if (entry.Resource != null)
                _sharedResourceById[$"{entry.Resource.TypeName}/{entry.Resource.Id}"] = entry.Resource;
        }

        RegisterPreGeneratedPatientIds();
    }

    /// <summary>
    /// Returns the IDs of patients pre-registered at startup.
    /// DataAcquisition census configuration can be seeded with these IDs.
    /// </summary>
    public IReadOnlyList<string> PreGeneratedPatientIds => _preGeneratedPatientIds.AsReadOnly();

    /// <summary>
    /// Reads a single resource by type and ID. Shared resources are served from cache.
    /// Patient-owned resources are regenerated on demand and immediately discarded.
    /// </summary>
    public async Task<Resource?> GetByIdAsync(
        string resourceType,
        string id,
        CancellationToken cancellationToken = default)
    {
        if (_sharedResourceById.TryGetValue($"{resourceType}/{id}", out var shared))
            return shared;

        var patientId = resourceType.Equals("Patient", StringComparison.OrdinalIgnoreCase)
            ? id
            : ExtractPatientIdFromResourceId(id);

        if (patientId == null)
            return null;

        var entries = await GeneratePatientEntriesAsync(patientId, resourceType, cancellationToken);
        return entries
            .FirstOrDefault(e =>
                e.Resource != null &&
                e.Resource.TypeName.Equals(resourceType, StringComparison.OrdinalIgnoreCase) &&
                e.Resource.Id == id)
            ?.Resource;
    }

    /// <summary>
    /// Searches for resources of the given type. Patient-specific data is generated on
    /// demand and discarded after this call returns. Shared resources are served from cache.
    /// </summary>
    public async Task<List<Resource>> SearchAsync(
        string resourceType,
        string? patientId,
        string? idList,
        CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(idList))
            return await SearchByIdListAsync(resourceType, idList, cancellationToken);

        if (string.IsNullOrWhiteSpace(patientId))
            return [];

        await RegisterPatientAsync(patientId, cancellationToken);

        var entries = await GeneratePatientEntriesAsync(patientId, resourceType, cancellationToken);
        return entries
            .Where(e => e.Resource != null &&
                        e.Resource.TypeName.Equals(resourceType, StringComparison.OrdinalIgnoreCase))
            .Select(e => e.Resource!)
            .ToList();
    }

    /// <summary>
    /// Registers a patient ID so it can be located in future ID-based reads.
    /// No resources are generated or cached.
    /// </summary>
    public async Task EnsurePatientGeneratedAsync(
        string patientId,
        CancellationToken cancellationToken = default)
    {
        await RegisterPatientAsync(patientId, cancellationToken);
    }

    // ------------------------------------------------------------------ //
    //  Private helpers                                                     //
    // ------------------------------------------------------------------ //

    private async Task<List<Resource>> SearchByIdListAsync(
        string resourceType,
        string idList,
        CancellationToken cancellationToken)
    {
        var ids = idList.Split(',',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var results = new List<Resource>(ids.Length);
        var patientOwned = new List<string>();

        foreach (var id in ids)
        {
            if (_sharedResourceById.TryGetValue($"{resourceType}/{id}", out var shared))
                results.Add(shared);
            else
                patientOwned.Add(id);
        }

        if (patientOwned.Count == 0)
            return results;

        // Group non-shared IDs by patient so each patient is generated only once.
        var byPatient = patientOwned
            .GroupBy(ExtractPatientIdFromResourceId)
            .Where(g => g.Key != null);

        foreach (var group in byPatient)
        {
            var entries = await GeneratePatientEntriesAsync(group.Key!, resourceType, cancellationToken);
            var entryById = entries
                .Where(e => e.Resource != null)
                .ToDictionary(e => e.Resource!.Id!, e => e.Resource!,
                    StringComparer.OrdinalIgnoreCase);

            foreach (var id in group)
            {
                if (entryById.TryGetValue(id, out var r) &&
                    r.TypeName.Equals(resourceType, StringComparison.OrdinalIgnoreCase))
                {
                    results.Add(r);
                }
            }
        }

        return results;
    }

    private async Task RegisterPatientAsync(string patientId, CancellationToken ct)
    {
        await _lock.WaitAsync(ct);
        try
        {
            if (_knownPatientIds.Contains(patientId))
                return;
            _knownPatientIds.Add(patientId);
        }
        finally
        {
            _lock.Release();
        }
    }

    private void RegisterPreGeneratedPatientIds()
    {
        var baseSeed = _settings.GenerationSeed ?? 0;
        for (var i = 0; i < _settings.PreGeneratedPatientCount; i++)
        {
            var patientId = $"mock-patient-{baseSeed + i + 1:D4}";
            _knownPatientIds.Add(patientId);
            _preGeneratedPatientIds.Add(patientId);
        }
    }

    private async Task<List<Bundle.EntryComponent>> GeneratePatientEntriesAsync(
        string patientId,
        string? requestedResourceType = null,
        CancellationToken cancellationToken = default)
    {
        var config = requestedResourceType != null ? ConfigForType(requestedResourceType) : null;
        var seed = (int)(patientId.GetStableHash32() & 0x7FFFFFFFu);
        var request = new PatientEntryRequest
        {
            Profile = MockProfile,
            PatientIndex = 0,
            BaseSeed = seed,
            TotalResourcesPerPatient = _settings.ResourcesPerPatient,
            SharedPractitionerIds = _practitionerIds,
            SharedMedicationIds = _medicationIds,
            Measures = MockMeasures,
            ClinicalPeriodStart = ParseDate(_settings.ClinicalPeriodStart),
            ClinicalPeriodEnd = ParseDate(_settings.ClinicalPeriodEnd),
            Config = config,
            Ids = _sharedIds,
            PatientId = patientId
        };

        var entries = await PatientGenerator.GenerateAsync(request, cancellationToken);
        if (string.IsNullOrWhiteSpace(requestedResourceType))
            return entries;

        return entries
            .Where(e => e.Resource != null
                        && e.Resource.TypeName.Equals(requestedResourceType, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    /// <summary>
    /// Returns a <see cref="FhirGenerationConfig"/> whose distribution contains only the
    /// requested resource type, using its normal fraction from the default config.
    /// All other bulk resource types are skipped so Thetis only fans out the
    /// requested type. Anchor resources (Patient, Encounter, Condition, Device,
    /// CareTeam, CarePlan) are always generated regardless.
    /// </summary>
    private FhirGenerationConfig ConfigForType(string resourceType)
    {
        var distribution = new Dictionary<string, double>();
        if (_defaultConfig.ResourceDistribution.TryGetValue(resourceType, out var fraction))
            distribution[resourceType] = fraction;
        // If the type isn't in the distribution (e.g. Patient, Encounter — anchor types),
        // an empty distribution is fine: anchor resources are always produced.
        return new FhirGenerationConfig
        {
            ResourceDistribution = distribution
        };
    }

    /// <summary>
    /// Extracts the patient ID from a generated resource ID.
    /// Resource IDs follow the pattern "{patientId}-{TypeAbbrev}-{3-digit-index}",
    /// e.g. "mock-patient-0001-Obs-001" → "mock-patient-0001".
    /// </summary>
    private static string? ExtractPatientIdFromResourceId(string resourceId)
    {
        // Strip last segment (index digits) and second-to-last (type abbreviation).
        var lastDash = resourceId.LastIndexOf('-');
        if (lastDash <= 0) return null;

        var secondLastDash = resourceId.LastIndexOf('-', lastDash - 1);
        if (secondLastDash <= 0) return null;

        // Validate the last segment looks like a numeric index.
        var indexPart = resourceId.AsSpan(lastDash + 1);
        if (indexPart.IsEmpty || !indexPart.IsNumeric()) return null;

        return resourceId[..secondLastDash];
    }

    private static DateTime? ParseDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal, out var dto))
        {
            return dto.UtcDateTime;
        }

        return null;
    }
}

file static class SpanExtensions
{
    internal static bool IsNumeric(this ReadOnlySpan<char> span)
    {
        foreach (var c in span)
            if (!char.IsDigit(c)) return false;
        return true;
    }
}

