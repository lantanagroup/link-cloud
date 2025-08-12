using AngleSharp.Dom;
using DataAcquisition.Domain.Infrastructure.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models;
using LantanaGroup.Link.Shared.Domain.Entities;
using System.ComponentModel.DataAnnotations.Schema;
using System.Runtime.Serialization;

namespace LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;

[DataContract]
[Table("fhirListConfiguration")]
public class FhirListConfiguration : BaseEntityExtended
{
    [DataMember]
    public string FacilityId { get; set; }
    [DataMember]
    public string FhirBaseServerUrl { get; set; }
    [DataMember]
    public AuthenticationConfiguration? Authentication { get; set; }
    [DataMember]
    public List<EhrPatientList> EHRPatientLists { get; set; }

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
