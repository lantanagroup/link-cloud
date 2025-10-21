using DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.Configuration;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Models
{
    public class UpdateFhirListConfigurationModel
    {
        public string? Id { get; set; }
        public string FacilityId { get; set; }
        public string FhirBaseServerUrl { get; set; }
        public AuthenticationConfigurationModel? Authentication { get; set; }
        public List<EhrPatientListModel> EHRPatientLists { get; set; }
    }
}
