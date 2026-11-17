using LantanaGroup.Link.Shared.Application.Models.Kafka;

namespace LantanaGroup.Link.Census.Application.Models.Payloads.Adt;

/// <summary>
/// Reads what an MRG segment says the patient or visit was known as before.
/// </summary>
public static class AdtMergeIdentifiers
{
    /// <summary>
    /// MRG-1, MRG-4 and MRG-2: the identifiers that can point at encounters of the prior patient.
    /// </summary>
    public static IReadOnlyCollection<string> PriorPatientIdentifiers(AdtMergeInformation? merge)
    {
        if (merge is null)
        {
            return [];
        }

        return Distinct(merge.PriorPatientIdentifierList.Select(x => x.Id)
            .Append(merge.PriorPatientId)
            .Append(merge.PriorAlternatePatientId));
    }

    /// <summary>
    /// MRG-5 and MRG-6: the visit number and alternate visit id the visit was known by before.
    /// </summary>
    public static IReadOnlyCollection<string> PriorVisitIdentifiers(AdtMergeInformation? merge) =>
        merge is null ? [] : Distinct([merge.PriorVisitNumber, merge.PriorAlternateVisitId]);

    private static List<string> Distinct(IEnumerable<string?> values) =>
        values
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!)
            .Distinct()
            .ToList();
}
