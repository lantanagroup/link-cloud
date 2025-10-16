using DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Models;

public class FhirQueryConfigurationModel
{
    public string? Id { get; set; }

    public string? FacilityId { get; set; }

    public string? FhirServerBaseUrl { get; set; }

    public AuthenticationConfigurationModel? Authentication { get; set; }

    public int? MaxConcurrentRequests { get; set; } = 8;

    public TimeSpan? MinAcquisitionPullTime { get; set; }

    public TimeSpan? MaxAcquisitionPullTime { get; set; }

    public string? TimeZone { get; set; } = null;

    public DateTime? CreateDate { get; set; }

    public DateTime? ModifyDate { get; set; }

    public FhirQueryConfiguration ToDomain()
    {
        return new FhirQueryConfiguration
        {
            FacilityId = this.FacilityId,
            FhirServerBaseUrl = this.FhirServerBaseUrl,
            Authentication = this.Authentication?.ToDomain(),
            MaxConcurrentRequests = this.MaxConcurrentRequests,
            MinAcquisitionPullTime = this.MinAcquisitionPullTime,
            MaxAcquisitionPullTime = this.MaxAcquisitionPullTime,
            TimeZone = this.TimeZone
        };
    }

    public static FhirQueryConfigurationModel FromDomain(FhirQueryConfiguration? entity)
    {
        if (entity == null)
            return null;

        return new FhirQueryConfigurationModel
        {
            Id = entity.Id,
            FacilityId = entity.FacilityId,
            FhirServerBaseUrl = entity.FhirServerBaseUrl,
            Authentication = entity.Authentication != null ? AuthenticationConfigurationModel.FromDomain(entity.Authentication) : null,
            MaxConcurrentRequests = entity.MaxConcurrentRequests,
            MinAcquisitionPullTime = entity.MinAcquisitionPullTime,
            MaxAcquisitionPullTime = entity.MaxAcquisitionPullTime,
            TimeZone = entity.TimeZone,
            CreateDate = entity.CreateDate,
            ModifyDate = entity.ModifyDate
        };
    }
}