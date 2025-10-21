using LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition;
using RequestStatus = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.RequestStatus;
using QueryPhase = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.QueryPhase;
using FhirQueryType = LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition.FhirQueryType;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.Configuration;

public class ResourceReferenceTypeModel
{
    public Guid? Id { get; set; }
    public string FacilityId { get; set; }
    public QueryPhase QueryPhase { get; set; }
    public string? ResourceType { get; set; }
    public Guid? FhirQueryId { get; set; }
    public DateTime CreateDate { get; set; } = DateTime.UtcNow;
    public DateTime? ModifyDate { get; set; }
}
