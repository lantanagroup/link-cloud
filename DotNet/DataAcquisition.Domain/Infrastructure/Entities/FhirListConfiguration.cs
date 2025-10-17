using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models;
using System.ComponentModel.DataAnnotations.Schema;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;

[Table("fhirListConfiguration")]
public class FhirListConfiguration
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string FacilityId { get; set; }
    public string FhirBaseServerUrl { get; set; }
    public AuthenticationConfiguration? Authentication { get; set; }
    public List<EhrPatientList> EHRPatientLists { get; set; }
    public DateTime CreateDate { get; set; } = DateTime.UtcNow;
    public DateTime? ModifyDate { get; set; }

    public bool Validate()
    {
        if (string.IsNullOrWhiteSpace(FacilityId) || string.IsNullOrWhiteSpace(FhirBaseServerUrl))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(FhirBaseServerUrl) || !Uri.IsWellFormedUriString(FhirBaseServerUrl, UriKind.Absolute))
            return false;


        return true;
    }
}
