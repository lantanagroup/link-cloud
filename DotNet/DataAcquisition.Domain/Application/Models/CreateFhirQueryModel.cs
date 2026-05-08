using LantanaGroup.Link.DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models.Enums;

namespace DataAcquisition.Domain.Application.Models;

public class CreateFhirQueryModel
{
    public required string FacilityId { get; set; }
    public bool IsReference { get; set; }
    public FhirQueryType QueryType { get; set; }
    public List<Hl7.Fhir.Model.ResourceType> ResourceTypes { get; set; } = new();
    public List<string> QueryParameters { get; set; } = new();
    public List<CreateResourceReferenceTypeModel> ResourceReferenceTypes { get; set; } = new();
    public int? Paged { get; set; }
    public long DataAcquisitionLogId { get; set; }
    public string? MeasureId { get; set; }
    public List<string> IdQueryParameterValues { get; set; } = new();
    public TimeFrame CensusTimeFrame { get; internal set; }
    public ListType CensusPatientStatus { get; internal set; }
    public string CensusListId { get; internal set; }
}
