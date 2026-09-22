using System.Text.Json.Serialization;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Models.PatientsOfInterest;

namespace LantanaGroup.Link.Nhsn.App.Bff.Infrastructure.Link.Mappers;

// Data Acquisition's FHIR List configuration (six patient lists) as it appears on the wire, and its
// mapping to/from the frontend's CensusListKey strings.
//
// Declared locally because Create/Get/UpdateFhirListConfigurationAsync all return or take the
// non-generic LinkApiResponse/object — no shared DTO exists in LinkSdk for this shape yet.
internal sealed record PatientListConfigurationWire
{
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    [JsonPropertyName("facilityId")]
    public string? FacilityId { get; init; }

    [JsonPropertyName("fhirBaseServerUrl")]
    public string? FhirBaseServerUrl { get; init; }

    [JsonPropertyName("ehrPatientLists")]
    public List<EhrPatientListWire> EHRPatientLists { get; init; } = [];
}

internal sealed record EhrPatientListWire
{
    [JsonPropertyName("status")]
    public string? Status { get; init; }

    [JsonPropertyName("timeFrame")]
    public string? TimeFrame { get; init; }

    [JsonPropertyName("internalId")]
    public string? InternalId { get; init; }

    [JsonPropertyName("fhirId")]
    public string? FhirId { get; init; }

    // Only present on GET api/data/{facilityId}/fhirQueryList?includePatients=true.
    [JsonPropertyName("patients")]
    public List<EhrPatientListPatientWire>? Patients { get; init; }
}

internal sealed record EhrPatientListPatientWire
{
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    [JsonPropertyName("name")]
    public string? Name { get; init; }
}

// Outbound shape for POST/PUT api/data/fhirQueryList — both bind the same FhirListConfigurationModel
// server-side, so one payload type covers create and update.
internal sealed class PatientListConfigurationPayload
{
    public string? Id { get; init; }
    public required string FacilityId { get; init; }
    public required string FhirBaseServerUrl { get; init; }
    public List<EhrPatientListPayload> EHRPatientLists { get; init; } = [];
}

internal sealed class EhrPatientListPayload
{
    public required string Status { get; init; }
    public required string TimeFrame { get; init; }
    public required string FhirId { get; init; }
}

internal static class PatientListConfigurationMapper
{
    // (Status, TimeFrame) pairs, in the order the frontend's CensusListKey enumerates them.
    private static readonly IReadOnlyList<(string Key, string Status, string TimeFrame)> ListKeys =
    [
        ("admit-lt-24", "Admit", "LessThan24Hours"),
        ("admit-24-to-48", "Admit", "Between24To48Hours"),
        ("admit-gt-48", "Admit", "MoreThan48Hours"),
        ("discharge-lt-24", "Discharge", "LessThan24Hours"),
        ("discharge-24-to-48", "Discharge", "Between24To48Hours"),
        ("discharge-gt-48", "Discharge", "MoreThan48Hours")
    ];

    public static IReadOnlyDictionary<string, string> ToPatientListIds(PatientListConfigurationWire? source)
    {
        if (source is null)
        {
            return new Dictionary<string, string>();
        }

        var byStatusAndTimeFrame = source.EHRPatientLists.ToDictionary(
            list => (list.Status, list.TimeFrame),
            list => list.FhirId ?? string.Empty);

        var result = new Dictionary<string, string>();
        foreach (var (key, status, timeFrame) in ListKeys)
        {
            if (byStatusAndTimeFrame.TryGetValue((status, timeFrame), out var fhirId) && !string.IsNullOrWhiteSpace(fhirId))
            {
                result[key] = fhirId;
            }
        }

        return result;
    }

    // The configured list behind one of the frontend's CensusListKey strings, or null when the key
    // is unknown or that list is not configured.
    public static EhrPatientListWire? FindList(PatientListConfigurationWire? source, string listKey)
    {
        var entry = ListKeys.FirstOrDefault(candidate => candidate.Key == listKey);
        if (entry.Key is null || source is null)
        {
            return null;
        }

        return source.EHRPatientLists.FirstOrDefault(list => list.Status == entry.Status && list.TimeFrame == entry.TimeFrame);
    }

    public static IReadOnlyList<CensusListResult> ToCensusListResults(PatientListConfigurationWire? source)
    {
        return ListKeys.Select(entry =>
        {
            var patients = ToCensusPatients(FindList(source, entry.Key));

            return new CensusListResult
            {
                ListKey = entry.Key,
                PatientCount = patients.Length,
                Patients = patients
            };
        }).ToList();
    }

    public static CensusPatient[] ToCensusPatients(EhrPatientListWire? list)
    {
        return list?.Patients?
            .Where(patient => !string.IsNullOrWhiteSpace(patient.Id))
            .Select(patient => new CensusPatient { Id = patient.Id!, Name = patient.Name })
            .ToArray() ?? [];
    }

    // Every one of the six keys must carry a non-empty id — Data Acquisition rejects anything but
    // exactly six complete entries. The caller is expected to have validated this already; this
    // throws rather than silently omitting a list Data Acquisition would then reject anyway.
    public static List<EhrPatientListPayload> ToWireLists(IReadOnlyDictionary<string, string> patientListIds)
    {
        return ListKeys.Select(entry =>
        {
            if (!patientListIds.TryGetValue(entry.Key, out var fhirId) || string.IsNullOrWhiteSpace(fhirId))
            {
                throw new InvalidOperationException($"PatientListIds is missing a value for '{entry.Key}'.");
            }

            return new EhrPatientListPayload
            {
                Status = entry.Status,
                TimeFrame = entry.TimeFrame,
                FhirId = fhirId
            };
        }).ToList();
    }
}
