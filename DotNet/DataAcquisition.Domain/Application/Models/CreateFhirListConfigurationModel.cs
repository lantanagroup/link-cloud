using DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models;
using System.Runtime.Serialization;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Models;

[DataContract]
public class FhirListConfigurationModel
{
    [DataMember]
    public string? Id { get; set; }
    [DataMember]
    public string? FacilityId { get; set; }
    [DataMember]
    public string? FhirBaseServerUrl { get; set; }
    [DataMember]
    public AuthenticationConfigurationModel? Authentication { get; set; }
    [DataMember]
    public List<EhrPatientList> EHRPatientLists { get; set; } = new();
    [DataMember]
    public DateTime? CreateDate { get; set; }
    [DataMember]
    public DateTime? ModifyDate { get; set; }

    public FhirListConfiguration ToDomain()
    {
        return new FhirListConfiguration
        {
            FacilityId = this.FacilityId,
            FhirBaseServerUrl = this.FhirBaseServerUrl,
            Authentication = this.Authentication?.ToDomain(),
            EHRPatientLists = this.EHRPatientLists
        };
    }

    public static FhirListConfigurationModel? FromDomain(FhirListConfiguration? entity)
    {
        if (entity == null)
            return null;

        return new FhirListConfigurationModel
        {
            Id = entity.Id,
            FacilityId = entity.FacilityId,
            FhirBaseServerUrl = entity.FhirBaseServerUrl,
            Authentication = entity.Authentication != null ? AuthenticationConfigurationModel.FromDomain(entity.Authentication) : null,
            EHRPatientLists = entity.EHRPatientLists,
            CreateDate = entity.CreateDate,
            ModifyDate = entity.ModifyDate
        };
    }
}