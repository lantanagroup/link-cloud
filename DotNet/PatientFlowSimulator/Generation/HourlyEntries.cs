using Hl7.Fhir.Model;

namespace LantanaGroup.Link.PatientFlowSimulator.Generation;

/// <summary>
/// The resources that go into one hour's file. A resource added twice in the same hour (an encounter
/// that is both carried over and discharged, say) keeps only its last version, because a transaction
/// may not contain the same resource twice.
/// </summary>
public sealed class HourlyEntries
{
    private readonly Dictionary<string, (int Rank, int Order, Resource Resource)> _resources = new(StringComparer.Ordinal);
    private int _nextOrder;

    /// <summary>
    /// Number of resources in the hour.
    /// </summary>
    public int Count => _resources.Count;

    /// <summary>
    /// Adds or replaces a resource. Lower ranks are uploaded first.
    /// </summary>
    public void Add(Resource resource)
    {
        var key = $"{resource.TypeName}/{resource.Id}";
        _resources[key] = (RankOf(resource), _nextOrder++, resource);
    }

    /// <summary>
    /// The hour's transaction bundle.
    /// </summary>
    public Bundle ToBundle(DateTimeOffset hour)
    {
        var entries = _resources.Values
            .OrderBy(v => v.Rank)
            .ThenBy(v => v.Order)
            .Select(v => FhirResourceFactory.PutEntry(v.Resource));

        return FhirResourceFactory.TransactionBundle(entries, hour);
    }

    private static int RankOf(Resource resource) => resource switch
    {
        Organization => 0,
        Location => 1,
        Patient => 2,
        Encounter => 3,
        Condition => 4,
        MedicationRequest => 5,
        MedicationAdministration => 6,
        Observation => 7,
        List => 9,
        _ => 8
    };
}
