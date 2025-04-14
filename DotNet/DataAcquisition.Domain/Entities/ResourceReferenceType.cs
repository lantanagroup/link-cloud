using DataAcquisition.Domain.Models.Enums;
using LantanaGroup.Link.DataAcquisition.Domain.Entities;
using LantanaGroup.Link.Shared.Domain.Entities;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataAcquisition.Domain.Entities;

[Table("ResourceReferenceType")]
public class ResourceReferenceType : BaseEntityExtended
{
    public string FacilityId { get; set; }
    public QueryPhase QueryPhase { get; set; }
    public string? ResourceType { get; set; }

    public string? FhirQueryId { get; set; }
    public FhirQuery? FhirQueryRef{ get; set; }
}
