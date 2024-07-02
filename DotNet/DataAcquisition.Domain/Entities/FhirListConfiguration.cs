using LantanaGroup.Link.DataAcquisition.Domain.Models;
using LantanaGroup.Link.Shared.Domain.Entities;
using System.ComponentModel.DataAnnotations.Schema;

namespace LantanaGroup.Link.DataAcquisition.Domain.Entities;

[Table("fhirListConfiguration")]
public class FhirListConfiguration : BaseEntityExtended
{
    public string FacilityId { get; set; }
    public string FhirBaseServerUrl { get; set; }
    public AuthenticationConfiguration? Authentication { get; set; }
    public List<EhrPatientList> EHRPatientLists { get; set; }
}
