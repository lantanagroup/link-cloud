using DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Models;

public class FhirListConfigurationModel
{

    public string? Id { get; set; }

    public string? FacilityId { get; set; }

    public string? FhirBaseServerUrl { get; set; }

    public AuthenticationConfigurationModel? Authentication { get; set; }

    public List<EhrPatientList> EHRPatientLists { get; set; } = new();

    public DateTime? CreateDate { get; set; }

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