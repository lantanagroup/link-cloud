using DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Models;

public class CreateFhirListConfigurationModel
{
    public required string FacilityId { get; set; }
    public required string FhirBaseServerUrl { get; set; }
    public AuthenticationConfigurationModel? Authentication { get; set; }
    public List<EhrPatientList> EHRPatientLists { get; set; } = new();
    public DateTime? CreateDate { get; set; }
    public DateTime? ModifyDate { get; set; }
}