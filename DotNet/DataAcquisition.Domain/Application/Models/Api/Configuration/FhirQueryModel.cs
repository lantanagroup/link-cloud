using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.Configuration;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums;
using FhirQueryType = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.FhirQueryType;

namespace DataAcquisition.Domain.Application.Models;

public class FhirQueryModel
{
    public Guid? Id { get; set; }
    public string? FacilityId { get; set; }
    public bool? IsReference { get; set; }
    public FhirQueryType QueryType { get; set; }
    public IEnumerable<Hl7.Fhir.Model.ResourceType> ResourceTypes { get; set; } = new List<Hl7.Fhir.Model.ResourceType>();
    public List<string> QueryParameters { get; set; } = new();
    public List<ResourceReferenceTypeModel> ResourceReferenceTypes { get; set; } = new();
    public int? Paged { get; set; }
    public long DataAcquisitionLogId { get; set; }
    public string? MeasureId { get; set; }
    public IEnumerable<string> IdQueryParameterValues
    {
        get
        {
            const string prefix = "_id=";
            return (QueryParameters ?? [])
                .Where(p => p.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                .Select(p => p.Substring(prefix.Length))
                .SelectMany(p => p.Split(','))
                .Where(id => !string.IsNullOrWhiteSpace(id));
        }
        set
        {
            const string prefix = "_id=";
            var ids = (value ?? Enumerable.Empty<string>())
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .ToList();

            var withoutId = (QueryParameters ?? [])
                .Where(p => !p.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                .ToList();

            // Only re-append an "_id=..." entry when we actually have ids to write;
            // otherwise leave QueryParameters free of a stray empty "_id=" that would
            // otherwise trip the empty-_id skip path during execution.
            if (ids.Count > 0)
                withoutId.Add($"{prefix}{string.Join(',', ids)}");

            QueryParameters = withoutId;
        }
    }
    public TimeFrame? CensusTimeFrame { get; set; } = null;
    public ListType? CensusPatientStatus { get; set; } = null;
    public string? CensusListId { get; set; } = null;

    public string Query
    {
        get
        {
            if (ResourceTypes == null || !ResourceTypes.Any())
                return string.Empty;

            return QueryType switch
            {
                FhirQueryType.Search => $"{ResourceTypes.First()}?{string.Join("&", QueryParameters)}",
                FhirQueryType.SearchPost => $"{ResourceTypes.First()}/_search [{string.Join(",", QueryParameters)}]",
                FhirQueryType.Read => $"{ResourceTypes.First()}/{string.Join("&", QueryParameters)}",
                FhirQueryType.BulkDataRequest => "BulkDataRequest", // add logic when bulk fhir is implemented
                FhirQueryType.BulkDataPoll => string.Join("&", QueryParameters),
                _ => string.Empty
            };
        }
    }
}
