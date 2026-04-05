using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Models;

/// <summary>
/// Lightweight projection of a FhirQuery used by reference resource processing.
/// Contains only the fields needed to merge reference IDs — avoids loading
/// the full DataAcquisitionLog entity graph.
/// </summary>
public class ReferenceQueryLookupResult
{
    public Guid FhirQueryId { get; set; }
    public string FacilityId { get; set; } = string.Empty;
    public FhirQueryType QueryType { get; set; }
    public List<string> QueryParameters { get; set; } = [];

    /// <summary>
    /// Extracts the _id parameter values from <see cref="QueryParameters"/>,
    /// matching the logic in <c>FhirQuery.IdQueryParameterValues</c>.
    /// </summary>
    public IEnumerable<string> IdQueryParameterValues
    {
        get
        {
            const string prefix = "_id=";
            return (QueryParameters ?? [])
                .Where(p => p.StartsWith(prefix))
                .Select(p => p[prefix.Length..])
                .SelectMany(p => p.Split(','))
                .Where(id => id != "");
        }
    }
}
