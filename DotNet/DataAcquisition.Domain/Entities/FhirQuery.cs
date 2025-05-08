using DataAcquisition.Domain.Entities;
using DataAcquisition.Domain.Models.Enums;
using Hl7.Fhir.Model;
using LantanaGroup.Link.Shared.Domain.Entities;
using System.ComponentModel.DataAnnotations.Schema;
using ResourceType = DataAcquisition.Domain.Models.Enums.ResourceType;

namespace LantanaGroup.Link.DataAcquisition.Domain.Entities;

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
    public DataAcquisitionLog DataAcquisitionLog { get; set; }
    public string DataAcquisitionLogId { get; set; }

}
