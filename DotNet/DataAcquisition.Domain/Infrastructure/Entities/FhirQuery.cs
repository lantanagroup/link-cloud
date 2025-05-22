using DataAcquisition.Domain.Infrastructure.Models.Enums;
using Hl7.Fhir.Model;
using LantanaGroup.Link.Shared.Domain.Entities;
using System.ComponentModel.DataAnnotations.Schema;
using ResourceType = DataAcquisition.Domain.Infrastructure.Models.Enums.ResourceType;

namespace DataAcquisition.Domain.Infrastructure.Entities;

[Table("FhirQuery")]
public class FhirQuery : BaseEntityExtended
{
    public string FacilityId { get; set; }
    public FhirQueryType QueryType { get; set; }
    public List<Hl7.Fhir.Model.ResourceType> ResourceTypes { get; set; }
    public List<string> QueryParameters { get; set; } = new List<string>();
    public List<ResourceReferenceType> ResourceReferenceTypes { get; set; }
    public int? Paged { get; set; }
    public string? MeasureId { get; set; }
    public bool? isReference { get; set; } = false;
    public DataAcquisitionLog DataAcquisitionLog { get; set; }
    public string DataAcquisitionLogId { get; set; }

}
